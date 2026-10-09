using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MimeKit;

namespace bahar_chaykhana.Services;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _useSsl;

    public EmailService(IConfiguration configuration)
    {
        var smtp = configuration.GetSection("Smtp");
        _smtpHost = smtp["Host"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(smtp["Port"] ?? "587");
        _smtpUser = smtp["User"] ?? "";
        _smtpPassword = smtp["Password"] ?? "";
        _fromEmail = smtp["FromEmail"] ?? _smtpUser;
        _fromName = smtp["FromName"] ?? "Чайхана Бахар";
        _useSsl = bool.Parse(smtp["UseSsl"] ?? "true");
    }

    public async Task SendOrderStatusEmailAsync(string toEmail, OrderDetails order, string status, string? cancelUrl)
    {
        // Всё, что приходит от гостя (имя, адрес, комментарий) и из БД, экранируем:
        // иначе символы <, >, & могут сломать вёрстку письма.
        string H(string? v) => WebUtility.HtmlEncode(v ?? "");
        string Money(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture) + " ₽";

        var subject = $"Заказ №{order.Id} — статус изменён на «{status}»";

        var itemsRows = string.Join("", order.Items.Select(i => $@"
            <tr>
                <td style=""padding:6px 8px;border-bottom:1px solid #eee;"">{H(i.Name)}</td>
                <td style=""padding:6px 8px;border-bottom:1px solid #eee;text-align:center;"">{i.Quantity}</td>
                <td style=""padding:6px 8px;border-bottom:1px solid #eee;text-align:right;"">{Money(i.Price)}</td>
                <td style=""padding:6px 8px;border-bottom:1px solid #eee;text-align:right;"">{Money(i.LineTotal)}</td>
            </tr>"));

        var isDelivery = order.DeliveryType == "delivery";
        var addressLine = isDelivery && !string.IsNullOrWhiteSpace(order.Address)
            ? $"<p><strong>Адрес доставки:</strong> {H(order.Address)}</p>" : "";
        var commentLine = !string.IsNullOrWhiteSpace(order.Comment)
            ? $"<p><strong>Комментарий к заказу:</strong> {H(order.Comment)}</p>" : "";
        var deliveryRow = isDelivery
            ? $"<tr><td colspan=\"3\" style=\"padding:4px 8px;text-align:right;\">Доставка:</td><td style=\"padding:4px 8px;text-align:right;\">{Money(order.DeliveryCost)}</td></tr>" : "";
        var bonusRow = order.BonusUsed > 0
            ? $"<tr><td colspan=\"3\" style=\"padding:4px 8px;text-align:right;\">Оплачено бонусами:</td><td style=\"padding:4px 8px;text-align:right;\">−{Money(order.BonusUsed)}</td></tr>" : "";

        var body = $@"
            <div style=""font-family:Arial,sans-serif;color:#2b2118;max-width:560px;"">
            <h2>Здравствуйте, {H(order.CustomerName)}!</h2>
            <p>Статус вашего заказа №{order.Id} изменён на <strong>{H(status)}</strong>.</p>

            <p><strong>Дата и время заказа:</strong> {order.CreatedAt:dd.MM.yyyy HH:mm}<br>
            <strong>Способ получения:</strong> {(isDelivery ? "доставка" : "самовывоз")}</p>
            {addressLine}

            <table style=""border-collapse:collapse;width:100%;font-size:14px;"">
                <thead>
                    <tr style=""background:#f3eee6;"">
                        <th style=""padding:6px 8px;text-align:left;"">Блюдо</th>
                        <th style=""padding:6px 8px;"">Кол-во</th>
                        <th style=""padding:6px 8px;text-align:right;"">Цена</th>
                        <th style=""padding:6px 8px;text-align:right;"">Сумма</th>
                    </tr>
                </thead>
                <tbody>{itemsRows}</tbody>
                <tfoot>
                    <tr><td colspan=""3"" style=""padding:4px 8px;text-align:right;"">Сумма блюд:</td><td style=""padding:4px 8px;text-align:right;"">{Money(order.Subtotal)}</td></tr>
                    {deliveryRow}
                    {bonusRow}
                    <tr><td colspan=""3"" style=""padding:8px;text-align:right;""><strong>Итого к оплате:</strong></td><td style=""padding:8px;text-align:right;""><strong>{Money(order.Total)}</strong></td></tr>
                </tfoot>
            </table>

            {commentLine}
            {(cancelUrl != null ? $@"<p>Если вы хотите отменить заказ, перейдите по ссылке:<br>
            <a href=""{cancelUrl}"">Отменить заказ</a></p>
            <p><em>Ссылка действительна, пока заказ не начал готовиться.</em></p>" : "")}
            <p>С уважением,<br>Чайхана Бахар</p>
            </div>
        ";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendReservationStatusEmailAsync(string toEmail, string customerName, int reservationId, string status, string? cancelUrl)
    {
        var safeName = WebUtility.HtmlEncode(customerName);
        var safeStatus = WebUtility.HtmlEncode(status.ToLower());

        var subject = $"Бронирование №{reservationId} — {status}";
        var body = $@"
            <h2>Здравствуйте, {safeName}!</h2>
            <p>Ваше бронирование №{reservationId} {safeStatus}.</p>
            {(cancelUrl != null ? $@"<p>Если вы хотите отменить бронь, перейдите по ссылке:<br>
            <a href=""{cancelUrl}"">Отменить бронирование</a></p>
            <p><em>Ссылка действительна не позднее чем за 30 минут до назначенного времени.</em></p>" : "")}
            <p>С уважением,<br>Чайхана Бахар</p>
        ";

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpHost, _smtpPort, _useSsl);
            await client.AuthenticateAsync(_smtpUser, _smtpPassword);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}