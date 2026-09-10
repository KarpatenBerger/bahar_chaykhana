using System.Text.RegularExpressions;
using bahar_chaykhana.Data;
using bahar_chaykhana.Models;
using BCryptNet = BCrypt.Net.BCrypt;

namespace bahar_chaykhana.Endpoints;

public static class AuthEndpoints
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    // ДОБАВЛЕНО: корпоративный домен сотрудников чайханы
    private const string CorporateDomain = "@bahar.ru";

    private static bool IsCorporateEmail(string email) =>
        email.Trim().EndsWith(CorporateDomain, StringComparison.OrdinalIgnoreCase);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Регистрация (только гости — корпоративная почта запрещена)
        app.MapPost("/api/auth/register", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<RegisterRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            if (!EmailRegex.IsMatch(payload.Email))
                return Results.BadRequest(new { message = "Некорректный формат email" });

            // ДОБАВЛЕНО: корпоративная почта @bahar.ru выдаётся сотрудникам лично
            // и не может использоваться для самостоятельной регистрации гостевого аккаунта.
            if (IsCorporateEmail(payload.Email))
                return Results.BadRequest(new
                {
                    message = "Почта с доменом @bahar.ru не может быть использована для регистрации — это корпоративный адрес, который выдаётся сотрудникам лично."
                });

            if (payload.Password.Length < 6)
                return Results.BadRequest(new { message = "Пароль должен содержать не менее 6 символов" });

            var email = payload.Email.Trim().ToLowerInvariant();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT id FROM customers WHERE email = @email";
            checkCmd.Parameters.AddWithValue("@email", email);
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
            insertCmd.Parameters.AddWithValue("@email", email);
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
                role = "customer",
                customer.Id,
                customer.Name,
                customer.Email,
                customer.BonusBalance
            });
        });

        // Вход — общая форма для гостей и сотрудников.
        // Домен @bahar.ru направляет проверку в таблицу employees вместо customers.
        app.MapPost("/api/auth/login", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<LoginRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            var email = payload.Email.Trim().ToLowerInvariant();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            // ДОБАВЛЕНО: ветка входа сотрудника по корпоративной почте
            if (IsCorporateEmail(email))
            {
                await using var empCmd = connection.CreateCommand();
                empCmd.CommandText = "SELECT id, name, password_hash FROM employees WHERE email = @email";
                empCmd.Parameters.AddWithValue("@email", email);

                (int id, string name, string hash)? employee = null;
                await using (var reader = await empCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        employee = (reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
                }

                // Один и тот же ответ и на "email не найден", и на "неверный пароль" —
                // чтобы нельзя было через форму входа проверять, какие корпоративные
                // адреса существуют в системе.
                if (employee == null || !BCryptNet.Verify(payload.Password, employee.Value.hash))
                    return Results.Unauthorized();

                context.Response.Cookies.Append("bahar_employee_id", employee.Value.id.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromDays(1)
                });

                return Results.Ok(new
                {
                    role = "employee",
                    employee.Value.id,
                    employee.Value.name
                });
            }

            // Обычный вход гостя — без изменений в логике
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, phone, email, password_hash, bonus_balance, registered_at FROM customers WHERE email = @email";
            cmd.Parameters.AddWithValue("@email", email);

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
                role = "customer",
                customer.Id,
                customer.Name,
                customer.Email,
                customer.BonusBalance
            });
        });

        // Выход — снимаем оба возможных cookie на всякий случай (гость мог быть
        // залогинен как сотрудник и наоборот; лишний Delete на отсутствующем cookie безопасен)
        app.MapPost("/api/auth/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete("bahar_customer_id");
            context.Response.Cookies.Delete("bahar_employee_id");
            return Results.Ok();
        });
    }
}

public record RegisterRequest(string Name, string Phone, string Email, string Password);
public record LoginRequest(string Email, string Password);