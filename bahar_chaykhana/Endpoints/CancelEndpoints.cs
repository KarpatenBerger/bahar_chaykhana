using bahar_chaykhana.Data;

namespace bahar_chaykhana.Endpoints;

public static class CancelEndpoints
{
    public static void MapCancelEndpoints(this WebApplication app)
    {
        // Отмена заказа по токену
        app.MapGet("/api/orders/cancel/{token}", async (string token, DbConnectionFactory db) =>
        {
            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            // ИСПРАВЛЕНО: добавили customer_id, bonus_used, subtotal — нужны для отката бонусов
            cmd.CommandText = @"
                SELECT id, status, email, customer_id, bonus_used, subtotal
                FROM orders WHERE cancel_token = @token";
            cmd.Parameters.AddWithValue("@token", token);

            int? orderId = null;
            string? status = null;
            string? email = null;
            int? customerId = null;
            int bonusUsed = 0;
            decimal subtotal = 0;

            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    orderId = reader.GetInt32(0);
                    status = reader.GetString(1);
                    email = reader.IsDBNull(2) ? null : reader.GetString(2);
                    customerId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                    bonusUsed = reader.GetInt32(4);
                    subtotal = reader.GetDecimal(5);
                }
            }

            if (orderId == null)
                return Results.NotFound(new { message = "Заказ не найден или ссылка недействительна" });

            // Проверка: отменить можно только в статусах "новый" и "принят"
            if (status != "новый" && status != "принят")
                return Results.BadRequest(new { message = "Заказ уже готовится и не может быть отменён" });

            // ИСПРАВЛЕНО: вся операция в одной транзакции — статус + откат бонусов
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = "UPDATE orders SET status = 'отменён' WHERE id = @id";
                updateCmd.Parameters.AddWithValue("@id", orderId.Value);
                await updateCmd.ExecuteNonQueryAsync();
            }

            // ИСПРАВЛЕНО: откатываем бонусы, если заказ был у зарегистрированного гостя.
            // Возвращаем списанные (bonus_used) и отзываем начисленные (2.5% от subtotal),
            // при этом баланс не должен уйти в минус (GREATEST(...,0)).
            if (customerId.HasValue)
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

            return Results.Ok(new { message = $"Заказ №{orderId} отменён" });
        });

        // Отмена брони по токену
        app.MapGet("/api/reservations/cancel/{token}", async (string token, DbConnectionFactory db) =>
        {
            await using var connection = db.CreateConnection();
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, reservation_date, reservation_time, status, email 
                FROM reservations WHERE cancel_token = @token";
            cmd.Parameters.AddWithValue("@token", token);

            int? reservationId = null;
            DateTime? resDateTime = null;
            string? status = null;
            string? email = null;

            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    reservationId = reader.GetInt32(0);

                    // ИСПРАВЛЕНО: reservation_date — DATE, reservation_time — TIME.
                    // Npgsql мапит их на DateOnly/TimeOnly, а не на DateTime —
                    // GetDateTime() на этих колонках бросал бы InvalidCastException.
                    var datePart = reader.GetFieldValue<DateOnly>(1);
                    var timePart = reader.GetFieldValue<TimeOnly>(2);
                    resDateTime = datePart.ToDateTime(timePart);

                    status = reader.GetString(3);
                    email = reader.IsDBNull(4) ? null : reader.GetString(4);
                }
            }

            if (reservationId == null)
                return Results.NotFound(new { message = "Бронирование не найдено или ссылка недействительна" });

            // ИСПРАВЛЕНО: раньше проверки статуса не было вообще — можно было
            // "отменить" уже отклонённую или уже отменённую бронь повторно.
            if (status != "ожидает" && status != "подтверждено")
                return Results.BadRequest(new { message = "Бронирование уже обработано и не может быть отменено" });

            // Проверка: отменить можно не позднее чем за 30 минут до времени
            if (resDateTime.HasValue)
            {
                var now = DateTime.Now;
                var minutesUntil = (resDateTime.Value - now).TotalMinutes;

                if (minutesUntil < 30)
                    return Results.BadRequest(new { message = "Бронирование нельзя отменить менее чем за 30 минут до назначенного времени" });
            }

            // ИСПРАВЛЕНО: используем отдельный статус 'отменено' для самоотмены гостем,
            // чтобы отличать её от 'отклонено' (решение администратора) в интерфейсах.
            // Требует миграции database/migration_add_reservation_cancelled_status.sql
            await using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE reservations SET status = 'отменено' WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@id", reservationId.Value);
            await updateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { message = $"Бронирование №{reservationId} отменено" });
        });
    }
}