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

            Customer customer = null;
            await using var userReader = await userCmd.ExecuteReaderAsync();
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

            if (customer == null)
                return Results.Unauthorized();

            var reservations = new List<object>();
            await using var resCmd = connection.CreateCommand();
            resCmd.CommandText = @"
                SELECT id, reservation_date, reservation_time, guests, status
                FROM reservations
                WHERE customer_id = @id AND status = 'ожидает'
                ORDER BY reservation_date, reservation_time";
            resCmd.Parameters.AddWithValue("@id", customerId);
            await using var resReader = await resCmd.ExecuteReaderAsync();
            while (await resReader.ReadAsync())
            {
                reservations.Add(new
                {
                    id = resReader.GetInt32(0),
                    date = resReader.GetDateTime(1).ToString("yyyy-MM-dd"),
                    time = resReader.GetDateTime(2).ToString("HH:mm"),
                    guests = resReader.GetInt32(3),
                    status = resReader.GetString(4)
                });
            }

            var orders = new List<object>();
            await using var ordCmd = connection.CreateCommand();
            ordCmd.CommandText = @"
                SELECT id, total, status, created_at
                FROM orders
                WHERE customer_id = @id
                ORDER BY created_at DESC
                LIMIT 20";
            ordCmd.Parameters.AddWithValue("@id", customerId);
            await using var ordReader = await ordCmd.ExecuteReaderAsync();
            while (await ordReader.ReadAsync())
            {
                orders.Add(new
                {
                    id = ordReader.GetInt32(0),
                    total = ordReader.GetDecimal(1),
                    status = ordReader.GetString(2),
                    createdAt = ordReader.GetDateTime(3).ToString("o")
                });
            }

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
}