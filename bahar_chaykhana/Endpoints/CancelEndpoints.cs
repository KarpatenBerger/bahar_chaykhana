using System.Net;
using bahar_chaykhana.Data;

namespace bahar_chaykhana.Endpoints;

public static class CancelEndpoints
{
    public static void MapCancelEndpoints(this WebApplication app)
    {
        // ===== ЗАКАЗ =====
        // GET ничего не меняет: только показывает страницу с кнопкой подтверждения.
        // Это важно: почтовые сервисы (mail.ru и др.) сами открывают ссылки из писем
        // для проверки, и отмена по GET срабатывала бы без участия гостя.
        app.MapGet("/api/orders/cancel/{token}", (string token) =>
            ConfirmPage(
                "Отмена заказа",
                "Вы действительно хотите отменить заказ?",
                $"/api/orders/cancel/{Uri.EscapeDataString(token)}"));

        // Отмена выполняется только по нажатию кнопки (POST)
        app.MapPost("/api/orders/cancel/{token}", async (string token, DbConnectionFactory db) =>
        {
            var (ok, message) = await CancelOrderAsync(token, db);
            return ResultPage("Отмена заказа", message, ok);
        });

        // ===== БРОНЬ =====
        app.MapGet("/api/reservations/cancel/{token}", (string token) =>
            ConfirmPage(
                "Отмена бронирования",
                "Вы действительно хотите отменить бронирование?",
                $"/api/reservations/cancel/{Uri.EscapeDataString(token)}"));

        app.MapPost("/api/reservations/cancel/{token}", async (string token, DbConnectionFactory db) =>
        {
            var (ok, message) = await CancelReservationAsync(token, db);
            return ResultPage("Отмена бронирования", message, ok);
        });
    }

    // ---------- Логика отмены заказа (как была, но возвращает текст, а не JSON) ----------
    private static async Task<(bool ok, string message)> CancelOrderAsync(string token, DbConnectionFactory db)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, status, email, customer_id, bonus_used, subtotal
            FROM orders WHERE cancel_token = @token";
        cmd.Parameters.AddWithValue("@token", token);

        int? orderId = null;
        string? status = null;
        int? customerId = null;
        int bonusUsed = 0;
        decimal subtotal = 0;

        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                orderId = reader.GetInt32(0);
                status = reader.GetString(1);
                customerId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                bonusUsed = reader.GetInt32(4);
                subtotal = reader.GetDecimal(5);
            }
        }

        if (orderId == null)
            return (false, "Заказ не найден или ссылка недействительна");

        // Раньше любой статус, кроме «новый»/«принят», давал «уже готовится» —
        // в том числе повторный переход по ссылке уже отменённого заказа.
        if (status == "отменён")
            return (false, "Заказ уже отменён");

        if (status != "новый" && status != "принят")
            return (false, "Заказ уже готовится и не может быть отменён");

        await using var transaction = await connection.BeginTransactionAsync();

        await using (var updateCmd = connection.CreateCommand())
        {
            updateCmd.Transaction = transaction;
            updateCmd.CommandText = "UPDATE orders SET status = 'отменён' WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@id", orderId.Value);
            await updateCmd.ExecuteNonQueryAsync();
        }

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
        return (true, $"Заказ №{orderId} отменён");
    }

    // ---------- Логика отмены брони ----------
    private static async Task<(bool ok, string message)> CancelReservationAsync(string token, DbConnectionFactory db)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, reservation_date, reservation_time, status
            FROM reservations WHERE cancel_token = @token";
        cmd.Parameters.AddWithValue("@token", token);

        int? reservationId = null;
        DateTime? resDateTime = null;
        string? status = null;

        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                reservationId = reader.GetInt32(0);
                var datePart = reader.GetFieldValue<DateOnly>(1);
                var timePart = reader.GetFieldValue<TimeOnly>(2);
                resDateTime = datePart.ToDateTime(timePart);
                status = reader.GetString(3);
            }
        }

        if (reservationId == null)
            return (false, "Бронирование не найдено или ссылка недействительна");

        if (status == "отменено")
            return (false, "Бронирование уже отменено");

        if (status != "ожидает" && status != "подтверждено")
            return (false, "Бронирование уже обработано и не может быть отменено");

        if (resDateTime.HasValue && (resDateTime.Value - DateTime.Now).TotalMinutes < 30)
            return (false, "Бронирование нельзя отменить менее чем за 30 минут до назначенного времени");

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE reservations SET status = 'отменено' WHERE id = @id";
        updateCmd.Parameters.AddWithValue("@id", reservationId.Value);
        await updateCmd.ExecuteNonQueryAsync();

        return (true, $"Бронирование №{reservationId} отменено");
    }

    // ---------- Простые HTML-страницы вместо JSON ----------
    private static IResult ConfirmPage(string title, string question, string postUrl) =>
        Html(title, $@"
            <p>{WebUtility.HtmlEncode(question)}</p>
            <form method=""post"" action=""{WebUtility.HtmlEncode(postUrl)}"">
                <button type=""submit"">Да, отменить</button>
            </form>
            <p><a href=""/"">Вернуться на сайт</a></p>");

    private static IResult ResultPage(string title, string message, bool ok) =>
        Html(title, $@"
            <p class=""{(ok ? "ok" : "err")}"">{WebUtility.HtmlEncode(message)}</p>
            <p><a href=""/"">Вернуться на сайт</a></p>");

    private static IResult Html(string title, string inner) =>
        Results.Content($@"<!DOCTYPE html>
<html lang=""ru"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<meta name=""robots"" content=""noindex"">
<title>{WebUtility.HtmlEncode(title)} — Чайхана Бахар</title>
<style>
  body {{ font-family: system-ui, sans-serif; background:#f3eee6; color:#2b2118; margin:0;
         min-height:100vh; display:flex; align-items:center; justify-content:center; }}
  .card {{ background:#fff; border-radius:16px; padding:32px; max-width:420px; margin:16px;
          box-shadow:0 4px 20px rgba(0,0,0,.08); text-align:center; }}
  h1 {{ font-size:22px; margin:0 0 16px; }}
  button {{ background:#a8432a; color:#fff; border:0; border-radius:10px; padding:12px 24px;
           font-size:16px; cursor:pointer; }}
  a {{ color:#a8432a; }}
  .ok {{ color:#2e7d32; font-weight:600; }}
  .err {{ color:#b3261e; font-weight:600; }}
</style>
</head>
<body>
  <div class=""card"">
    <h1>{WebUtility.HtmlEncode(title)}</h1>
    {inner}
  </div>
</body>
</html>", "text/html; charset=utf-8");
}