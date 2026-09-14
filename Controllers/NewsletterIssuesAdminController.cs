using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SDMTech.Controllers
{
    // Simple admin API for composing and scheduling newsletter issues.
    // Protected by a shared secret header instead of full auth since no auth system exists yet.
    [ApiController]
    [Route("api/newsletter/admin/issues")]
    public class NewsletterIssuesAdminController : ControllerBase
    {
        private readonly SDMTekContext _context;
        private readonly NewsletterOptions _options;
        private readonly IEmailService _emailService;
        private readonly ILogger<NewsletterIssuesAdminController> _logger;

        public NewsletterIssuesAdminController(
            SDMTekContext context,
            IOptions<NewsletterOptions> options,
            IEmailService emailService,
            ILogger<NewsletterIssuesAdminController> logger)
        {
            _context = context;
            _options = options.Value;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var issues = await _context.NewsletterIssues
                .OrderByDescending(i => i.CreatedDate)
                .ToListAsync();

            return Ok(issues);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNewsletterIssueRequest request)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.HtmlBody))
            {
                return BadRequest("Subject and HtmlBody are required.");
            }

            var issue = new NewsletterIssue
            {
                Id = Guid.NewGuid(),
                Subject = request.Subject.Trim(),
                HtmlBody = request.HtmlBody,
                CreatedDate = DateTime.UtcNow,
                ScheduledDate = request.ScheduledDate
            };

            _context.NewsletterIssues.Add(issue);
            await _context.SaveChangesAsync();

            return Ok(issue);
        }

        // Sends the issue immediately to all active subscribers, regardless of its ScheduledDate.
        [HttpPost("{id}/send-now")]
        public async Task<IActionResult> SendNow(Guid id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var issue = await _context.NewsletterIssues.FirstOrDefaultAsync(i => i.Id == id);
            if (issue is null)
            {
                return NotFound();
            }

            if (issue.SentAt is not null)
            {
                return Conflict("This issue has already been sent.");
            }

            var sentCount = await NewsletterDispatchService.SendIssueAsync(
                issue, _context, _emailService, _options, _logger);

            return Ok(new { message = $"Issue sent to {sentCount} subscriber(s).", sentCount });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!IsAuthorized())
            {
                return Unauthorized();
            }

            var issue = await _context.NewsletterIssues.FirstOrDefaultAsync(i => i.Id == id);
            if (issue is null)
            {
                return NotFound();
            }

            if (issue.SentAt is not null)
            {
                return Conflict("Sent newsletter issues cannot be deleted.");
            }

            _context.NewsletterIssues.Remove(issue);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool IsAuthorized()
        {
            if (string.IsNullOrWhiteSpace(_options.AdminApiKey))
            {
                return false;
            }

            return Request.Headers.TryGetValue("X-Admin-Key", out var provided)
                && provided.ToString() == _options.AdminApiKey;
        }
    }
}
