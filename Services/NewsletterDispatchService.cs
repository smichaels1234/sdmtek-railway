using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services
{
    // Periodically checks for scheduled newsletter issues that are due and sends them to all active subscribers.
    public class NewsletterDispatchService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NewsletterDispatchService> _logger;

        public NewsletterDispatchService(IServiceScopeFactory scopeFactory, ILogger<NewsletterDispatchService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CheckInterval);

            do
            {
                try
                {
                    await DispatchDueIssuesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while dispatching scheduled newsletter issues.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task DispatchDueIssuesAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SDMTekContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<NewsletterOptions>>().Value;

            var now = DateTime.UtcNow;
            var dueIssues = await context.NewsletterIssues
                .Where(i => i.SentAt == null && i.ScheduledDate != null && i.ScheduledDate <= now)
                .OrderBy(i => i.ScheduledDate)
                .ToListAsync(stoppingToken);

            foreach (var issue in dueIssues)
            {
                var sentCount = await SendIssueAsync(issue, context, emailService, options, _logger);
                _logger.LogInformation("Sent newsletter issue {IssueId} to {SentCount} subscriber(s).", issue.Id, sentCount);
            }
        }

        // Sends the issue to every active subscriber and marks it as sent. Shared with the admin "send now" endpoint.
        public static async Task<int> SendIssueAsync(
            NewsletterIssue issue,
            SDMTekContext context,
            IEmailService emailService,
            NewsletterOptions options,
            ILogger logger)
        {
            var subscribers = await context.NewsletterSubscribers
                .Where(s => s.IsActive)
                .ToListAsync();

            var sentCount = 0;
            foreach (var subscriber in subscribers)
            {
                var baseUrl = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
                    ? "https://sdmtek.com"
                    : options.PublicBaseUrl;
                var body = issue.HtmlBody + NewsletterEmailBuilder.BuildUnsubscribeFooter(baseUrl, subscriber.UnsubscribeToken);

                var sent = await emailService.SendAsync(subscriber.Email, issue.Subject, body);
                if (sent)
                {
                    sentCount++;
                }
                else
                {
                    logger.LogWarning("Failed to send newsletter issue {IssueId} to {Email}.", issue.Id, subscriber.Email);
                }
            }

            issue.SentAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            return sentCount;
        }
    }
}
