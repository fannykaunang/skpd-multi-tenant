using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using skpd_multi_tenant_api.Options;

namespace skpd_multi_tenant_api.Services;

public interface IEmailService
{
    Task SendOtpAsync(string toEmail, string otpCode, CancellationToken ct = default);
}

public sealed class EmailService(IOptions<SmtpOptions> smtpOptions, ILogger<EmailService> logger) : IEmailService
{
    private readonly SmtpOptions _smtp = smtpOptions.Value;

    public async Task SendOtpAsync(string toEmail, string otpCode, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SKPD Kabupaten Merauke", _smtp.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Kode Verifikasi Login — SKPD Kabupaten Merauke";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $"""
            <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h2 style="margin: 0 0 8px; color: #1a1a1a;">Kode Verifikasi Login</h2>
                <p style="color: #555; margin: 0 0 24px;">Gunakan kode berikut untuk menyelesaikan proses login Anda:</p>
                <div style="background: #f4f4f5; border-radius: 8px; padding: 20px; text-align: center; margin-bottom: 24px;">
                    <span style="font-size: 32px; font-weight: 700; letter-spacing: 6px; color: #1a1a1a;">{otpCode}</span>
                </div>
                <p style="color: #888; font-size: 13px; margin: 0;">Kode ini berlaku selama <strong>5 menit</strong>. Jangan bagikan kode ini kepada siapapun.</p>
                <hr style="border: none; border-top: 1px solid #e4e4e7; margin: 24px 0;" />
                <p style="color: #aaa; font-size: 12px; margin: 0;">SKPD Kabupaten Merauke — Sistem Informasi Terintegrasi</p>
            </div>
            """,
            TextBody = $"Kode verifikasi login Anda: {otpCode}\n\nKode ini berlaku selama 5 menit. Jangan bagikan kode ini kepada siapapun."
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            var secureOption = _smtp.Secure
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(_smtp.Host, _smtp.Port, secureOption, ct);
            await client.AuthenticateAsync(_smtp.User, _smtp.Pass, ct);
            await client.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gagal mengirim OTP ke {Email}", toEmail);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, ct);
        }
    }
}
