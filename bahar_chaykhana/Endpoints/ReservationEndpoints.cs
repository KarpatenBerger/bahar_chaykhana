using bahar_chaykhana.Data;

namespace bahar_chaykhana.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/reservations", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<CreateReservationRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.CustomerName) || string.IsNullOrWhiteSpace(payload.Phone))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });
            if (payload.Guests <= 0)
                return Results.BadRequest(new { message = "Неверное количество гостей" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            int? customerId = null;
            if (context.Request.Cookies.TryGetValue("bahar_customer_id", out var cidStr) && int.TryParse(cidStr, out var cid))
                customerId = cid;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO reservations (customer_id, customer_name, phone, email, reservation_date, reservation_time, guests, comment, status)
                VALUES (@customerId, @name, @phone, @email, @date, @time, @guests, @comment, 'ожидает')
                RETURNING id";
            cmd.Parameters.AddWithValue("@customerId", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@name", payload.CustomerName);
            cmd.Parameters.AddWithValue("@phone", payload.Phone);
            cmd.Parameters.AddWithValue("@email", (object)payload.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@date", DateOnly.Parse(payload.Date));
            cmd.Parameters.AddWithValue("@time", TimeOnly.Parse(payload.Time));
            cmd.Parameters.AddWithValue("@guests", payload.Guests);
            cmd.Parameters.AddWithValue("@comment", (object)payload.Comment ?? DBNull.Value);

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return Results.Ok(new { id });
        });
    }
}

public record CreateReservationRequest(
    string Date,
    string Time,
    int Guests,
    string CustomerName,
    string Phone,
    string? Email,
    string? Comment
);