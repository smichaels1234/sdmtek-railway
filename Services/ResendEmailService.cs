using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services
{
    public class ResendEmailService : IEmailService
    {
        private const string ResendEndpoint = "https://api.resend.com/emails";

        private readonly EmailOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResendEmailService> _logger;

        public ResendEmailService(
            IOptions<EmailOptions> options,
            IHttpClientFactory httpClientFactory,
            ILogger<ResendEmailService> logger)
        {
            _options = options.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(_options.ResendApiKey) ||
                string.IsNullOrWhiteSpace(_options.SenderEmail))
            {
                _logger.LogWarning("Resend settings are incomplete; skipping outgoing email to {ToEmail}.", toEmail);
                return false;
            }

            var fromAddress = string.IsNullOrWhiteSpace(_options.SenderName)
                ? _options.SenderEmail
                : $"{_options.SenderName} <{_options.SenderEmail}>";

            var payload = new
            {
                from = fromAddress,
                to = new[] { toEmail },
                subject,
                html = htmlBody
            };

            try
            {
                using var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, ResendEndpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ResendApiKey);

                using var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to send email via Resend. StatusCode={StatusCode}, ToEmail={ToEmail}, ResponseBody={ResponseBody}",
                    (int)response.StatusCode,
                    toEmail,
                    body);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email via Resend to {ToEmail}.", toEmail);
                return false;
            }
        }
    }
}
