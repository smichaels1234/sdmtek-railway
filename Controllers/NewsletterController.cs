using System.Net.Mail;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SDMTech.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsletterController : ControllerBase
    {
        private readonly ILogger<NewsletterController> _logger;
        private readonly SDMTekContext _context;
        private readonly IEmailService _emailService;
        private readonly NewsletterOptions _newsletterOptions;

        public NewsletterController(
            ILogger<NewsletterController> logger,
            SDMTekContext context,
            IEmailService emailService,
            IOptions<NewsletterOptions> newsletterOptions)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _newsletterOptions = newsletterOptions.Value;
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeNewsletterRequest request)
        {
            var email = request.Email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Email is required.");
            }

            if (!IsValidEmail(email))
            {
                return BadRequest("A valid email address is required.");
            }

            var existingSubscriber = await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.Email.ToLower() == email.ToLower());

            if (existingSubscriber is not null)
            {
                var wasInactive = !existingSubscriber.IsActive;
                if (!existingSubscriber.IsActive)
                {
                    existingSubscriber.IsActive = true;
                    existingSubscriber.LastModified = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                var existingMessage = BuildConfirmationEmail(existingSubscriber.UnsubscribeToken);
                var existingEmailSent = await _emailService.SendAsync(email, "Newsletter subscription confirmed", existingMessage);
                if (!existingEmailSent)
                {
                    return Ok(new
                    {
                        message = wasInactive
                            ? "You have been resubscribed, but confirmation email could not be sent right now."
                            : "You are already subscribed, but confirmation email could not be sent right now.",
                        emailSent = false,
                        alreadySubscribed = !wasInactive,
                        resubscribed = wasInactive
                    });
                }

                return Ok(new
                {
                    message = wasInactive
                        ? "You have been resubscribed. Confirmation email was sent."
                        : "You are already subscribed. Confirmation email was sent.",
                    emailSent = true,
                    alreadySubscribed = !wasInactive,
                    resubscribed = wasInactive
                });
            }

            var subscriber = new NewsletterSubscriber
            {
                Id = Guid.NewGuid(),
                Email = email,
                IsActive = true,
                SubscribedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                UnsubscribeToken = Guid.NewGuid()
            };

            _context.NewsletterSubscribers.Add(subscriber);
            await _context.SaveChangesAsync();

            var message = BuildConfirmationEmail(subscriber.UnsubscribeToken);
            var emailSent = await _emailService.SendAsync(email, "Newsletter subscription confirmed", message);

            _logger.LogInformation("New newsletter subscription received for email: {Email}", email);
            if (!emailSent)
            {
                return Ok(new
                {
                    message = "Subscription saved, but confirmation email could not be sent right now.",
                    emailSent = false
                });
            }

            return Ok(new { message = "Subscription successful. An Email Confirmation is on the way. Please check your inbox or junk folder.", emailSent = true });
        }

        [HttpGet("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromQuery] Guid token)
        {
            var subscriber = await _context.NewsletterSubscribers
                .FirstOrDefaultAsync(s => s.UnsubscribeToken == token);

            if (subscriber is null)
            {
                return Content("<p>This unsubscribe link is invalid or has already been used.</p>", "text/html");
            }

            if (subscriber.IsActive)
            {
                subscriber.IsActive = false;
                subscriber.LastModified = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Content("<p>You have been unsubscribed from the SDMTek newsletter.</p>", "text/html");
        }

        private string BuildConfirmationEmail(Guid unsubscribeToken)
        {
            return "<p>Thanks for subscribing to the SDMTek newsletter.</p>" +
                "<p>We will share updates on development, marketing, and technology trends.</p>" +
                "<p>Regards,<br/>SDMTek Team</p>" +
                BuildUnsubscribeFooter(unsubscribeToken);
        }

        private string BuildUnsubscribeFooter(Guid unsubscribeToken)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_newsletterOptions.PublicBaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _newsletterOptions.PublicBaseUrl;
            return NewsletterEmailBuilder.BuildUnsubscribeFooter(baseUrl, unsubscribeToken);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
