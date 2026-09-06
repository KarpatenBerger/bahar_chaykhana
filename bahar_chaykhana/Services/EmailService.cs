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

    public async Task SendOrderStatusEmailAsync(string toEmail, string customerName, int orderId, string status, string? cancelUrl)
    {
        // ИСПРАВЛЕНО: customerName приходит от пользователя (форма заказа) и
        // подставлялся в HTML без экранирования — символы <, >, & могли сломать вёрстку письма.
        var safeName = WebUtility.HtmlEncode(customerName);
        var safeStatus = WebUtility.HtmlEncode(status);

        var subject = $"Заказ №{orderId} — статус изменён на «{status}»";
        var body = $@"
            <h2>Здравствуйте, {safeName}!</h2>
            <p>Статус вашего заказа №{orderId} изменён на <strong>{safeStatus}</strong>.</p>
            {(cancelUrl != null ? $@"<p>Если вы хотите отменить заказ, перейдите по ссылке:<br>
            <a href=""{cancelUrl}"">Отменить заказ</a></p>
            <p><em>Ссылка действительна, пока заказ не начал готовиться.</em></p>" : "")}
            <p>С уважением,<br>Чайхана Бахар</p>
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