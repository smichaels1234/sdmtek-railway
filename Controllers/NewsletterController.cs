using System.Net.Mail;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SDMTech.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsletterController : ControllerBase
    {
        private readonly ILogger<NewsletterController> _logger;
        private readonly SDMTekContext _context;
        private readonly IEmailService _emailService;

        public NewsletterController(ILogger<NewsletterController> logger, SDMTekContext context, IEmailService emailService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
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

            var message = "<p>Thanks for subscribing to the SDMTek newsletter.</p>" +
                "<p>We will share updates on development, marketing, and technology trends.</p>" +
                "<p>Regards,<br/>SDMTek Team</p>";

            if (existingSubscriber is not null)
            {
                if (!existingSubscriber.IsActive)
                {
                    existingSubscriber.IsActive = true;
                    existingSubscriber.LastModified = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                var existingEmailSent = await _emailService.SendAsync(email, "Newsletter subscription confirmed", message);
                if (!existingEmailSent)
                {
                    return Ok(new
                    {
                        message = "You are already subscribed, but confirmation email could not be sent right now.",
                        emailSent = false,
                        alreadySubscribed = true
                    });
                }

                return Ok(new
                {
                    message = "You are already subscribed. Confirmation email was sent.",
                    emailSent = true,
                    alreadySubscribed = true
                });
            }

            var subscriber = new NewsletterSubscriber
            {
                Id = Guid.NewGuid(),
                Email = email,
                IsActive = true,
                SubscribedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _context.NewsletterSubscribers.Add(subscriber);
            await _context.SaveChangesAsync();

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

            return Ok(new { message = "Subscription successful.", emailSent = true });
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
