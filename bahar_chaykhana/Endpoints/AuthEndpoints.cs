using System.Text.RegularExpressions;
using bahar_chaykhana.Data;
using bahar_chaykhana.Models;
using BCryptNet = BCrypt.Net.BCrypt;

namespace bahar_chaykhana.Endpoints;

public static class AuthEndpoints
{
    // ИСПРАВЛЕНО: простая, но серверная проверка формата email —
    // раньше единственная проверка была через <input type="email"> на клиенте
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Регистрация
        app.MapPost("/api/auth/register", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<RegisterRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            // ИСПРАВЛЕНО: длина пароля и формат email раньше проверялись только в HTML-форме
            if (!EmailRegex.IsMatch(payload.Email))
                return Results.BadRequest(new { message = "Некорректный формат email" });
            if (payload.Password.Length < 6)
                return Results.BadRequest(new { message = "Пароль должен содержать не менее 6 символов" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT id FROM customers WHERE email = @email";
            checkCmd.Parameters.AddWithValue("@email", payload.Email);
            if (await checkCmd.ExecuteScalarAsync() != null)
                return Results.Conflict(new { message = "Пользователь с таким email уже зарегистрирован" });

            var passwordHash = BCryptNet.HashPassword(payload.Password);

            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO customers (name, phone, email, password_hash, bonus_balance)
                VALUES (@name, @phone, @email, @hash, 0)
                RETURNING id, name, email, bonus_balance, registered_at";
            insertCmd.Parameters.AddWithValue("@name", payload.Name ?? "");
            insertCmd.Parameters.AddWithValue("@phone", payload.Phone ?? "");
            insertCmd.Parameters.AddWithValue("@email", payload.Email);
            insertCmd.Parameters.AddWithValue("@hash", passwordHash);

            Customer? customer = null;
            await using (var reader = await insertCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    customer = new Customer
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Email = reader.GetString(2),
                        BonusBalance = reader.GetInt32(3),
                        RegisteredAt = reader.GetDateTime(4)
                    };
                }
            }

            if (customer == null)
                return Results.Problem("Не удалось создать пользователя");

            context.Response.Cookies.Append("bahar_customer_id", customer.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(30)
            });

            return Results.Ok(new
            {
                customer.Id,
                customer.Name,
                customer.Email,
                customer.BonusBalance
            });
        });

        // Вход
        app.MapPost("/api/auth/login", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<LoginRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, phone, email, password_hash, bonus_balance, registered_at FROM customers WHERE email = @email";
            cmd.Parameters.AddWithValue("@email", payload.Email);

            Customer? customer = null;
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    customer = new Customer
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Phone = reader.GetString(2),
                        Email = reader.GetString(3),
                        PasswordHash = reader.GetString(4),
                        BonusBalance = reader.GetInt32(5),
                        RegisteredAt = reader.GetDateTime(6)
                    };
                }
            }

            if (customer == null || !BCryptNet.Verify(payload.Password, customer.PasswordHash))
                return Results.Unauthorized();

            context.Response.Cookies.Append("bahar_customer_id", customer.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(30)
            });

            return Results.Ok(new
            {
                customer.Id,
                customer.Name,
                customer.Email,
                customer.BonusBalance
            });
        });

        // Выход
        app.MapPost("/api/auth/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete("bahar_customer_id");
            return Results.Ok();
        });
    }
}

public record RegisterRequest(string Name, string Phone, string Email, string Password);
public record LoginRequest(string Email, string Password);