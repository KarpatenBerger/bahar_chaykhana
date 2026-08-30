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
            cmd.CommandText = @"
                SELECT id, status, email FROM orders WHERE cancel_token = @token";
            cmd.Parameters.AddWithValue("@token", token);

            int? orderId = null;
            string? status = null;
            string? email = null;

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                orderId = reader.GetInt32(0);
                status = reader.GetString(1);
                email = reader.IsDBNull(2) ? null : reader.GetString(2);
            }

            if (orderId == null)
                return Results.NotFound(new { message = "Заказ не найден или ссылка недействительна" });

            // Проверка: отменить можно только в статусах "новый" и "принят"
            if (status != "новый" && status != "принят")
                return Results.BadRequest(new { message = "Заказ уже готовится и не может быть отменён" });

            await using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE orders SET status = 'отменён' WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@id", orderId.Value);
            await updateCmd.ExecuteNonQueryAsync();

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

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                reservationId = reader.GetInt32(0);
                // Читаем дату и время как DateTime и объединяем
                var datePart = reader.GetDateTime(1).Date;
                var timePart = reader.GetDateTime(2).TimeOfDay;
                resDateTime = datePart + timePart;
                status = reader.GetString(3);
                email = reader.IsDBNull(4) ? null : reader.GetString(4);
            }

            if (reservationId == null)
                return Results.NotFound(new { message = "Бронирование не найдено или ссылка недействительна" });

            // Проверка: отменить можно не позднее чем за 30 минут до времени
            if (resDateTime.HasValue)
            {
                var now = DateTime.Now;
                var minutesUntil = (resDateTime.Value - now).TotalMinutes;

                if (minutesUntil < 30)
                    return Results.BadRequest(new { message = "Бронирование нельзя отменить менее чем за 30 минут до назначенного времени" });
            }

            await using var updateCmd = connection.CreateCommand();
            updateCmd.CommandText = "UPDATE reservations SET status = 'отклонено' WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@id", reservationId.Value);
            await updateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { message = $"Бронирование №{reservationId} отменено" });
        });
    }
}