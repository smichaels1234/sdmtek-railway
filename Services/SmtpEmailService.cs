using System.Net;
using System.Net.Mail;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(_options.SmtpHost) ||
                string.IsNullOrWhiteSpace(_options.SenderEmail))
            {
                _logger.LogWarning("Email settings are incomplete; skipping outgoing email to {ToEmail}.", toEmail);
                return false;
            }

            if (_options.SmtpHost.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(_options.Password) || _options.Password.Length < 16))
            {
                _logger.LogWarning(
                    "Gmail SMTP usually requires a 16-character App Password. Configure Google 2-Step Verification and use an App Password for account {SenderEmail}.",
                    _options.SenderEmail);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_options.SenderEmail, _options.SenderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            try
            {
                await client.SendMailAsync(message);
                return true;
            }
            catch (SmtpException ex)
            {
                if (_options.SmtpHost.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(ex,
                        "Gmail SMTP authentication failed for {SenderEmail}. Use a Gmail App Password (not your normal Gmail password).",
                        _options.SenderEmail);
                }

                _logger.LogError(ex, "Failed to send email to {ToEmail} using SMTP server {SmtpHost}.", toEmail, _options.SmtpHost);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {ToEmail}.", toEmail);
                return false;
            }
        }
    }
}
