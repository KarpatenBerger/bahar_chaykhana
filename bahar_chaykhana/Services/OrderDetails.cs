using Npgsql;

namespace bahar_chaykhana.Services;

public record OrderItemInfo(string Name, int Quantity, decimal Price)
{
    public decimal LineTotal => Price * Quantity;
}

public record OrderDetails(
    int Id,
    string CustomerName,
    string Phone,
    string? Email,
    string DeliveryType,
    string? Address,
    string? Comment,
    string Status,
    decimal Subtotal,
    decimal DeliveryCost,
    int BonusUsed,
    decimal Total,
    DateTime CreatedAt,
    List<OrderItemInfo> Items);

public static class OrderDetailsLoader
{
    // Загружает один заказ вместе с позициями (для письма гостю)
    public static async Task<OrderDetails?> LoadAsync(NpgsqlConnection connection, int orderId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, customer_name, phone, email, delivery_type, address, comment,
                   status, subtotal, delivery_cost, bonus_used, total, created_at
            FROM orders WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", orderId);

        OrderDetails? order = null;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                order = new OrderDetails(
                    r.GetInt32(0), r.GetString(1), r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5),
                    r.IsDBNull(6) ? null : r.GetString(6),
                    r.GetString(7), r.GetDecimal(8), r.GetDecimal(9),
                    r.GetInt32(10), r.GetDecimal(11), r.GetDateTime(12),
                    new List<OrderItemInfo>());
            }
        }

        if (order == null) return null;

        await using var itemsCmd = connection.CreateCommand();
        itemsCmd.CommandText = @"
            SELECT d.name, oi.quantity, oi.price_at_order
            FROM order_items oi
            JOIN dishes d ON d.id = oi.dish_id
            WHERE oi.order_id = @id
            ORDER BY oi.id";
        itemsCmd.Parameters.AddWithValue("@id", orderId);
        await using var ir = await itemsCmd.ExecuteReaderAsync();
        while (await ir.ReadAsync())
            order.Items.Add(new OrderItemInfo(ir.GetString(0), ir.GetInt32(1), ir.GetDecimal(2)));

        return order;
    }
}