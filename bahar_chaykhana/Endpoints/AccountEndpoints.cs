using bahar_chaykhana.Data;
using bahar_chaykhana.Models;

namespace bahar_chaykhana.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/account", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_customer_id", out var customerIdStr) ||
                !int.TryParse(customerIdStr, out var customerId))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var userCmd = connection.CreateCommand();
            userCmd.CommandText = "SELECT id, name, phone, email, bonus_balance, registered_at FROM customers WHERE id = @id";
            userCmd.Parameters.AddWithValue("@id", customerId);

            Customer? customer = null;
            await using (var userReader = await userCmd.ExecuteReaderAsync())
            {
                if (await userReader.ReadAsync())
                {
                    customer = new Customer
                    {
                        Id = userReader.GetInt32(0),
                        Name = userReader.GetString(1),
                        Phone = userReader.GetString(2),
                        Email = userReader.GetString(3),
                        BonusBalance = userReader.GetInt32(4),
                        RegisteredAt = userReader.GetDateTime(5)
                    };
                }
            }

            if (customer == null)
                return Results.Unauthorized();

            var reservations = new List<object>();
            await using var resCmd = connection.CreateCommand();
            // ИСПРАВЛЕНО: подтверждённая бронь ('подтверждено') — тоже активная и
            // предстоящая, раньше она пропадала из личного кабинета сразу после
            // подтверждения администратором. Плюс не показываем брони на прошедшие даты.
            resCmd.CommandText = @"
                SELECT id, reservation_date, reservation_time, guests, status
                FROM reservations
                WHERE customer_id = @id
                  AND status IN ('ожидает', 'подтверждено')
                  AND reservation_date >= CURRENT_DATE
                ORDER BY reservation_date, reservation_time";
            resCmd.Parameters.AddWithValue("@id", customerId);

            await using (var resReader = await resCmd.ExecuteReaderAsync())
            {
                while (await resReader.ReadAsync())
                {
                    // ИСПРАВЛЕНО: reservation_date/reservation_time читаются как
                    // DateOnly/TimeOnly, а не через GetDateTime (см. CancelEndpoints.cs).
                    var date = resReader.GetFieldValue<DateOnly>(1);
                    var time = resReader.GetFieldValue<TimeOnly>(2);

                    reservations.Add(new
                    {
                        id = resReader.GetInt32(0),
                        date = date.ToString("yyyy-MM-dd"),
                        time = time.ToString("HH:mm"),
                        guests = resReader.GetInt32(3),
                        status = resReader.GetString(4)
                    });
                }
            }

            // Последние 20 заказов гостя. Фильтр customer_id = @id гарантирует,
            // что гость видит только свои заказы.
            var orderRows = new List<OrderRow>();
            await using var ordCmd = connection.CreateCommand();
            ordCmd.CommandText = @"
                SELECT id, total, status, created_at, delivery_type, address, comment,
                       subtotal, delivery_cost, bonus_used
                FROM orders
                WHERE customer_id = @id
                ORDER BY created_at DESC
                LIMIT 20";
            ordCmd.Parameters.AddWithValue("@id", customerId);
            await using (var ordReader = await ordCmd.ExecuteReaderAsync())
            {
                while (await ordReader.ReadAsync())
                {
                    orderRows.Add(new OrderRow(
                        ordReader.GetInt32(0),
                        ordReader.GetDecimal(1),
                        ordReader.GetString(2),
                        ordReader.GetDateTime(3),
                        ordReader.GetString(4),
                        ordReader.IsDBNull(5) ? null : ordReader.GetString(5),
                        ordReader.IsDBNull(6) ? null : ordReader.GetString(6),
                        ordReader.GetDecimal(7),
                        ordReader.GetDecimal(8),
                        ordReader.GetInt32(9)));
                }
            }

            // Состав этих заказов одним запросом
            var itemsByOrder = new Dictionary<int, List<object>>();
            if (orderRows.Count > 0)
            {
                await using var itemsCmd = connection.CreateCommand();
                itemsCmd.CommandText = @"
                    SELECT oi.order_id, d.name, oi.quantity, oi.price_at_order
                    FROM order_items oi
                    JOIN dishes d ON d.id = oi.dish_id
                    WHERE oi.order_id = ANY(@ids)
                    ORDER BY oi.order_id, oi.id";
                itemsCmd.Parameters.AddWithValue("@ids", orderRows.Select(o => o.Id).ToArray());

                await using var itemsReader = await itemsCmd.ExecuteReaderAsync();
                while (await itemsReader.ReadAsync())
                {
                    var orderId = itemsReader.GetInt32(0);
                    if (!itemsByOrder.TryGetValue(orderId, out var list))
                        itemsByOrder[orderId] = list = new List<object>();
                    list.Add(new
                    {
                        name = itemsReader.GetString(1),
                        quantity = itemsReader.GetInt32(2),
                        price = itemsReader.GetDecimal(3)
                    });
                }
            }

            var orders = orderRows.Select(o => new
            {
                id = o.Id,
                total = o.Total,
                status = o.Status,
                createdAt = o.CreatedAt.ToString("o"),
                deliveryType = o.DeliveryType,
                address = o.Address,
                comment = o.Comment,
                subtotal = o.Subtotal,
                deliveryCost = o.DeliveryCost,
                bonusUsed = o.BonusUsed,
                items = itemsByOrder.TryGetValue(o.Id, out var its) ? its : new List<object>()
            }).ToList();

            return Results.Ok(new
            {
                customer.Id,
                customer.Name,
                customer.Phone,
                customer.Email,
                customer.BonusBalance,
                registeredAt = customer.RegisteredAt.ToString("o"),
                activeReservations = reservations,
                orders
            });
        });
    }

    private sealed record OrderRow(
        int Id, decimal Total, string Status, DateTime CreatedAt,
        string DeliveryType, string? Address, string? Comment,
        decimal Subtotal, decimal DeliveryCost, int BonusUsed);
}