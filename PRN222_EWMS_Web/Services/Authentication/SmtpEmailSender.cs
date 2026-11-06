using System.Net;
using System.Net.Mail;

namespace PRN222_EWMS_Web.Services.Authentication;

public class SmtpEmailSender(IConfiguration configuration)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Smtp:Host"])
        && int.TryParse(configuration["Smtp:Port"], out var port) && port > 0
        && !string.IsNullOrWhiteSpace(configuration["Smtp:Username"])
        && !string.IsNullOrWhiteSpace(configuration["Smtp:Password"])
        && !string.IsNullOrWhiteSpace(configuration["Smtp:From"]);

    public async Task SendPasswordResetAsync(string recipient, string link, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("SMTP is not configured.");
        using var message = new MailMessage(
            new MailAddress(configuration["Smtp:From"]!),
            new MailAddress(recipient))
        {
            Subject = "Đặt lại mật khẩu EWMS",
            Body = $"Bạn đã yêu cầu đặt lại mật khẩu EWMS. Mở liên kết sau trong vòng 1 phút:\n\n{link}\n\nNếu bạn không yêu cầu, hãy bỏ qua email này."
        };
        using var client = new SmtpClient(configuration["Smtp:Host"]!, int.Parse(configuration["Smtp:Port"]!))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"])
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
