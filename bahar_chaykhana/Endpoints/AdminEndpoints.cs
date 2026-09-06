using bahar_chaykhana.Data;
using bahar_chaykhana.Services;
using BCryptNet = BCrypt.Net.BCrypt;

namespace bahar_chaykhana.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        // Вход сотрудника
        app.MapPost("/api/admin/login", async (HttpContext context, DbConnectionFactory db) =>
        {
            var payload = await context.Request.ReadFromJsonAsync<AdminLoginRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Login) || string.IsNullOrWhiteSpace(payload.Password))
                return Results.BadRequest(new { message = "Не заполнены обязательные поля" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, login, password_hash, name FROM employees WHERE login = @login";
            cmd.Parameters.AddWithValue("@login", payload.Login);

            (int id, string name, string hash)? employee = null;
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                    employee = (reader.GetInt32(0), reader.GetString(3), reader.GetString(2));
            }

            if (employee == null || !BCryptNet.Verify(payload.Password, employee.Value.hash))
                return Results.Unauthorized();

            context.Response.Cookies.Append("bahar_employee_id", employee.Value.id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(1)
            });

            return Results.Ok(new { employee.Value.id, employee.Value.name });
        });

        // Выход
        app.MapPost("/api/admin/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete("bahar_employee_id");
            return Results.Ok();
        });

        // Список заказов
        app.MapGet("/api/admin/orders", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, customer_name, phone, delivery_type, subtotal, delivery_cost, bonus_used, total, status, created_at
                FROM orders
                ORDER BY created_at DESC";

            var orders = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orders.Add(new
                {
                    id = reader.GetInt32(0),
                    customerName = reader.GetString(1),
                    phone = reader.GetString(2),
                    deliveryType = reader.GetString(3),
                    subtotal = reader.GetDecimal(4),
                    deliveryCost = reader.GetDecimal(5),
                    bonusUsed = reader.GetInt32(6),
                    total = reader.GetDecimal(7),
                    status = reader.GetString(8),
                    createdAt = reader.GetDateTime(9).ToString("o")
                });
            }

            return Results.Ok(orders);
        });

        // Смена статуса заказа
        app.MapPatch("/api/admin/orders/{id:int}", async (HttpContext context, int id, DbConnectionFactory db, EmailService emailService) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<ChangeStatusRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Status))
                return Results.BadRequest(new { message = "Не указан статус" });

            // ИСПРАВЛЕНО: добавили 'отменён' в список допустимых статусов —
            // сотрудник тоже должен иметь возможность отменить заказ вручную.
            var allowed = new[] { "новый", "принят", "готовится", "готов", "выдан", "отменён" };
            if (!allowed.Contains(payload.Status))
                return Results.BadRequest(new { message = "Недопустимый статус" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            // ИСПРАВЛЕНО: сначала читаем текущее состояние заказа — нужно и для
            // защиты от "реанимации" отменённого заказа, и для отката бонусов.
            string? currentStatus = null;
            int? customerId = null;
            int bonusUsed = 0;
            decimal subtotal = 0;

            await using (var infoCmd = connection.CreateCommand())
            {
                infoCmd.CommandText = "SELECT status, customer_id, bonus_used, subtotal FROM orders WHERE id = @id";
                infoCmd.Parameters.AddWithValue("@id", id);
                await using var infoReader = await infoCmd.ExecuteReaderAsync();
                if (await infoReader.ReadAsync())
                {
                    currentStatus = infoReader.GetString(0);
                    customerId = infoReader.IsDBNull(1) ? null : infoReader.GetInt32(1);
                    bonusUsed = infoReader.GetInt32(2);
                    subtotal = infoReader.GetDecimal(3);
                }
            }

            if (currentStatus == null)
                return Results.NotFound(new { message = "Заказ не найден" });

            // ИСПРАВЛЕНО: отменённый заказ — терминальное состояние, менять статус нельзя
            if (currentStatus == "отменён")
                return Results.BadRequest(new { message = "Заказ уже отменён, изменить статус нельзя" });

            await using var transaction = await connection.BeginTransactionAsync();

            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE orders SET status = @status WHERE id = @id";
                cmd.Parameters.AddWithValue("@status", payload.Status);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            // ИСПРАВЛЕНО: при отмене заказа сотрудником тоже откатываем бонусы
            // (та же логика, что и при самоотмене гостем в CancelEndpoints.cs)
            if (payload.Status == "отменён" && customerId.HasValue)
            {
                var earnedBonus = (int)Math.Floor(subtotal * 0.025m);

                await using var bonusCmd = connection.CreateCommand();
                bonusCmd.Transaction = transaction;
                bonusCmd.CommandText = @"
                    UPDATE customers
                    SET bonus_balance = GREATEST(bonus_balance + @used - @earned, 0)
                    WHERE id = @id";
                bonusCmd.Parameters.AddWithValue("@used", bonusUsed);
                bonusCmd.Parameters.AddWithValue("@earned", earnedBonus);
                bonusCmd.Parameters.AddWithValue("@id", customerId.Value);
                await bonusCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            // Отправляем email только если гость указал email и статус один из требуемых
            if (payload.Status == "принят" || payload.Status == "готов" || payload.Status == "выдан")
            {
                await using var infoCmd = connection.CreateCommand();
                infoCmd.CommandText = "SELECT customer_name, email, cancel_token FROM orders WHERE id = @id";
                infoCmd.Parameters.AddWithValue("@id", id);

                string? customerName = null;
                string? email = null;
                string? cancelToken = null;

                await using (var infoReader = await infoCmd.ExecuteReaderAsync())
                {
                    if (await infoReader.ReadAsync())
                    {
                        customerName = infoReader.GetString(0);
                        email = infoReader.IsDBNull(1) ? null : infoReader.GetString(1);
                        cancelToken = infoReader.IsDBNull(2) ? null : infoReader.GetString(2);
                    }
                }

                if (!string.IsNullOrEmpty(email))
                {
                    string? cancelUrl = null;
                    if (payload.Status == "принят" && cancelToken != null)
                        cancelUrl = $"{context.Request.Scheme}://{context.Request.Host}/api/orders/cancel/{cancelToken}";

                    try
                    {
                        await emailService.SendOrderStatusEmailAsync(email, customerName ?? "Гость", id, payload.Status, cancelUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка отправки email: {ex.Message}");
                    }
                }
            }

            return Results.Ok();
        });

        // Список бронирований
        app.MapGet("/api/admin/reservations", async (HttpContext context, DbConnectionFactory db) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, customer_name, phone, reservation_date, reservation_time, guests, status, created_at
                FROM reservations
                ORDER BY reservation_date DESC, reservation_time DESC";

            var reservations = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // ИСПРАВЛЕНО: было reader.GetDateTime(3)/(4) на колонках DATE/TIME —
                // валилось с InvalidCastException при первой же брони в таблице.
                var date = reader.GetFieldValue<DateOnly>(3);
                var time = reader.GetFieldValue<TimeOnly>(4);

                reservations.Add(new
                {
                    id = reader.GetInt32(0),
                    customerName = reader.GetString(1),
                    phone = reader.GetString(2),
                    date = date.ToString("yyyy-MM-dd"),
                    time = time.ToString("HH:mm"),
                    guests = reader.GetInt32(5),
                    status = reader.GetString(6),
                    createdAt = reader.GetDateTime(7).ToString("o")
                });
            }

            return Results.Ok(reservations);
        });

        // Смена статуса брони
        app.MapPatch("/api/admin/reservations/{id:int}", async (HttpContext context, int id, DbConnectionFactory db, EmailService emailService) =>
        {
            if (!context.Request.Cookies.TryGetValue("bahar_employee_id", out _))
                return Results.Unauthorized();

            var payload = await context.Request.ReadFromJsonAsync<ChangeStatusRequest>();
            if (payload == null || string.IsNullOrWhiteSpace(payload.Status))
                return Results.BadRequest(new { message = "Не указан статус" });

            var allowed = new[] { "ожидает", "подтверждено", "отклонено" };
            if (!allowed.Contains(payload.Status))
                return Results.BadRequest(new { message = "Недопустимый статус" });

            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            // ИСПРАВЛЕНО: не даём переоткрыть бронь, которую гость уже отменил сам
            // ('отменено') — это должно оставаться терминальным состоянием.
            await using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT status FROM reservations WHERE id = @id";
                checkCmd.Parameters.AddWithValue("@id", id);
                var currentStatus = (string?)await checkCmd.ExecuteScalarAsync();

                if (currentStatus == null)
                    return Results.NotFound(new { message = "Бронирование не найдено" });

                if (currentStatus == "отменено")
                    return Results.BadRequest(new { message = "Бронь отменена гостем, изменить статус нельзя" });
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE reservations SET status = @status WHERE id = @id";
            cmd.Parameters.AddWithValue("@status", payload.Status);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();

            // Отправляем email при подтверждении/отклонении брони
            if (payload.Status == "подтверждено" || payload.Status == "отклонено")
            {
                await using var infoCmd = connection.CreateCommand();
                infoCmd.CommandText = "SELECT customer_name, email, cancel_token FROM reservations WHERE id = @id";
                infoCmd.Parameters.AddWithValue("@id", id);

                string? customerName = null;
                string? email = null;
                string? cancelToken = null;

                await using (var infoReader = await infoCmd.ExecuteReaderAsync())
                {
                    if (await infoReader.ReadAsync())
                    {
                        customerName = infoReader.GetString(0);
                        email = infoReader.IsDBNull(1) ? null : infoReader.GetString(1);
                        cancelToken = infoReader.IsDBNull(2) ? null : infoReader.GetString(2);
                    }
                }

                if (!string.IsNullOrEmpty(email))
                {
                    string? cancelUrl = null;
                    if (payload.Status == "подтверждено" && cancelToken != null)
                        cancelUrl = $"{context.Request.Scheme}://{context.Request.Host}/api/reservations/cancel/{cancelToken}";

                    try
                    {
                        await emailService.SendReservationStatusEmailAsync(email, customerName ?? "Гость", id, payload.Status, cancelUrl);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка отправки email: {ex.Message}");
                    }
                }
            }

            return Results.Ok();
        });
    }
}

public record AdminLoginRequest(string Login, string Password);
public record ChangeStatusRequest(string Status);