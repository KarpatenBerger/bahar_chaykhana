using bahar_chaykhana.Data;

namespace bahar_chaykhana.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapPost("/api/orders", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<CreateOrderRequest>();
            if (payload == null || payload.Items == null || payload.Items.Count == 0)
                return Results.BadRequest(new { message = "Корзина пуста" });
            if (string.IsNullOrWhiteSpace(payload.Phone) || string.IsNullOrWhiteSpace(payload.CustomerName))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });
            if (payload.DeliveryType != "pickup" && payload.DeliveryType != "delivery")
                return Results.BadRequest(new { message = "Неверный тип доставки" });
            if (payload.DeliveryType == "delivery" && string.IsNullOrWhiteSpace(payload.Address))
                return Results.BadRequest(new { message = "Укажите адрес доставки" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            int? customerId = null;
            if (context.Request.Cookies.TryGetValue("bahar_customer_id", out var cidStr) && int.TryParse(cidStr, out var cid))
                customerId = cid;

            // Получаем цены блюд
            var dishIds = payload.Items.Select(i => i.DishId).ToList();
            var dishPrices = new Dictionary<int, decimal>();

            await using var dishCmd = connection.CreateCommand();
            dishCmd.CommandText = "SELECT id, price FROM dishes WHERE id = ANY(@ids) AND is_available = TRUE";
            dishCmd.Parameters.AddWithValue("@ids", dishIds.ToArray());
            await using var dishReader = await dishCmd.ExecuteReaderAsync();
            while (await dishReader.ReadAsync())
                dishPrices[dishReader.GetInt32(0)] = dishReader.GetDecimal(1);

            foreach (var item in payload.Items)
            {
                if (!dishPrices.TryGetValue(item.DishId, out _))
                    return Results.BadRequest(new { message = $"Блюдо с id={item.DishId} недоступно" });
            }

            decimal subtotal = payload.Items.Sum(i => dishPrices[i.DishId] * i.Quantity);
            decimal deliveryCost = payload.DeliveryType == "delivery" ? 300m : 0m;

            int bonusUsed = 0;
            if (payload.UseBonus && customerId.HasValue)
            {
                await using var bonusCmd = connection.CreateCommand();
                bonusCmd.CommandText = "SELECT bonus_balance FROM customers WHERE id = @id";
                bonusCmd.Parameters.AddWithValue("@id", customerId.Value);
                var balance = Convert.ToInt32(await bonusCmd.ExecuteScalarAsync());
                var maxDiscount = (int)Math.Floor((subtotal + deliveryCost) * 0.9m);
                bonusUsed = Math.Min(balance, maxDiscount);
            }

            var total = subtotal + deliveryCost - bonusUsed;

            await using var orderCmd = connection.CreateCommand();
            orderCmd.CommandText = @"
                INSERT INTO orders (customer_id, customer_name, phone, email, delivery_type, address, comment, status, subtotal, delivery_cost, bonus_used, total)
                VALUES (@customerId, @name, @phone, @email, @deliveryType, @address, @comment, 'новый', @subtotal, @deliveryCost, @bonusUsed, @total)
                RETURNING id";
            orderCmd.Parameters.AddWithValue("@customerId", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
            orderCmd.Parameters.AddWithValue("@name", payload.CustomerName);
            orderCmd.Parameters.AddWithValue("@phone", payload.Phone);
            orderCmd.Parameters.AddWithValue("@email", (object)payload.Email ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@deliveryType", payload.DeliveryType);
            orderCmd.Parameters.AddWithValue("@address", (object)payload.Address ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@comment", (object)payload.Comment ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@subtotal", subtotal);
            orderCmd.Parameters.AddWithValue("@deliveryCost", deliveryCost);
            orderCmd.Parameters.AddWithValue("@bonusUsed", bonusUsed);
            orderCmd.Parameters.AddWithValue("@total", total);

            var orderId = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());

            await using var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = "INSERT INTO order_items (order_id, dish_id, quantity, price_at_order) VALUES (@orderId, @dishId, @qty, @price)";
            itemCmd.Parameters.AddWithValue("@orderId", orderId);
            var dishIdParam = itemCmd.Parameters.Add("@dishId", NpgsqlTypes.NpgsqlDbType.Integer);
            var qtyParam = itemCmd.Parameters.Add("@qty", NpgsqlTypes.NpgsqlDbType.Integer);
            var priceParam = itemCmd.Parameters.Add("@price", NpgsqlTypes.NpgsqlDbType.Numeric);

            foreach (var item in payload.Items)
            {
                dishIdParam.Value = item.DishId;
                qtyParam.Value = item.Quantity;
                priceParam.Value = dishPrices[item.DishId];
                await itemCmd.ExecuteNonQueryAsync();
            }

            // Начисляем 2.5% бонусов
            if (customerId.HasValue)
            {
                var earnedBonus = (int)Math.Floor(subtotal * 0.025m);
                if (earnedBonus > 0)
                {
                    await using var bonusCmd = connection.CreateCommand();
                    bonusCmd.CommandText = "UPDATE customers SET bonus_balance = bonus_balance + @bonus WHERE id = @id";
                    bonusCmd.Parameters.AddWithValue("@bonus", earnedBonus);
                    bonusCmd.Parameters.AddWithValue("@id", customerId.Value);
                    await bonusCmd.ExecuteNonQueryAsync();
                }
            }

            return Results.Ok(new { id = orderId, total });
        });
    }
}

public record CreateOrderRequest(
    List<OrderItemRequest> Items,
    string DeliveryType,
    string? Address,
    string Phone,
    string? Email,
    string CustomerName,
    string? Comment,
    bool UseBonus
);

public record OrderItemRequest(int DishId, int Quantity);