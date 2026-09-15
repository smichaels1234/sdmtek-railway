using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Models;
using Microsoft.Extensions.Options;

namespace backend.Services
{
    public class TurnstileService : ITurnstileService
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TurnstileService> _logger;
        private readonly TurnstileOptions _options;

        public TurnstileService(
            IHttpClientFactory httpClientFactory,
            ILogger<TurnstileService> logger,
            IOptions<TurnstileOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_options.SecretKey) && _options.AllowedHostnames.Length > 0;

        public async Task<bool> VerifyAsync(
            string token,
            string expectedAction,
            string? remoteIpAddress,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.LogError("Cloudflare Turnstile is not fully configured.");
                return false;
            }

            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = _options.SecretKey!,
                    ["response"] = token,
                    ["remoteip"] = remoteIpAddress ?? string.Empty
                });
                using var response = await _httpClientFactory.CreateClient()
                    .PostAsync(VerifyUrl, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Turnstile verification returned status code {StatusCode}.", response.StatusCode);
                    return false;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var verification = await JsonSerializer.DeserializeAsync<TurnstileVerificationResponse>(
                    responseStream,
                    cancellationToken: cancellationToken);

                var isValid = verification?.Success == true &&
                    verification.Action == expectedAction &&
                    !string.IsNullOrWhiteSpace(verification.Hostname) &&
                    _options.AllowedHostnames.Contains(verification.Hostname, StringComparer.OrdinalIgnoreCase);

                if (!isValid)
                {
                    _logger.LogWarning("Turnstile validation failed. Error codes: {ErrorCodes}; action: {Action}; hostname: {Hostname}",
                        verification?.ErrorCodes is { Length: > 0 }
                            ? string.Join(",", verification.ErrorCodes)
                            : "none",
                        verification?.Action ?? "none",
                        verification?.Hostname ?? "none");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turnstile verification failed due to an exception.");
                return false;
            }
        }

        private sealed class TurnstileVerificationResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }

            [JsonPropertyName("action")]
            public string? Action { get; set; }

            [JsonPropertyName("hostname")]
            public string? Hostname { get; set; }
        }
    }
}
