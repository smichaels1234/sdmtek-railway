using backend.Models;
using backend.Services;
using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace SDMTech.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly ILogger<ContactController> _logger;
        private readonly SDMTekContext _context;
        private readonly IEmailService _emailService;
        private readonly ITurnstileService _turnstileService;

        public ContactController(
            ILogger<ContactController> logger,
            SDMTekContext context,
            IEmailService emailService,
            ITurnstileService turnstileService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _turnstileService = turnstileService;
        }

         [HttpGet]
        public async Task<ActionResult<IEnumerable<Contact>>> GetContacts()
        { 
            _logger.LogInformation("Getting all contacts");

            try
            {
                var contacts = await _context.Contacts.ToListAsync();
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get contacts from database.");
                return Problem(
                    title: "Unable to retrieve contacts.",
                    detail: "A server error occurred while loading contacts.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Contact>> GetContact(string id)
        {
            _logger.LogInformation("Getting contact with id: {Id}", id);
            
            if (!Guid.TryParse(id, out var contactId))
            {
                return BadRequest("Invalid contact ID format");
            }
            
            var contact = await _context.Contacts.FindAsync(contactId);
            
            if (contact == null)
            {
                return NotFound();
            }
            
            return Ok(contact);
        }

        [HttpPost]
        public async Task<ActionResult<Contact>> CreateContact([FromBody] CreateContactRequest request)
        {
            _logger.LogInformation("Creating new contact");

            if (string.IsNullOrWhiteSpace(request.CaptchaToken))
            {
                return BadRequest("Captcha token is required.");
            }

            if (!_turnstileService.IsConfigured)
            {
                return Problem(
                    title: "Verification is not configured.",
                    detail: "The server is missing its Cloudflare Turnstile configuration.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var isTurnstileValid = await _turnstileService.VerifyAsync(
                request.CaptchaToken,
                "contact",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);
            if (!isTurnstileValid)
            {
                return BadRequest("Verification failed. Please try again.");
            }

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Phone = request.Phone,
                Company = request.Company,
                Service = request.Service,
                Budget = request.Budget,
                Message = request.Message,
                TermsOfService = request.TermsOfService,
                CreatedDate = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(contact.Email))
            {
                var displayName = string.Join(" ", new[] { contact.FirstName, contact.LastName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = "there";
                }

                var message = $"<p>Hi {displayName},</p>" +
                    "<p>Thanks for reaching out to SDMTek. We received your message and will get back to you shortly.</p>" +
                    "<p>Regards,<br/>SDMTek Team</p>";

                var emailSent = await _emailService.SendAsync(contact.Email, "We received your message", message);
                if (!emailSent)
                {
                    _logger.LogWarning("Contact was saved but acknowledgement email could not be sent for contact id: {Id}", contact.Id);
                }
            }

            var internalMessage =
                "<p>A new contact submission was received:</p>" +
                "<ul>" +
                $"<li><strong>First Name:</strong> {Encode(contact.FirstName)}</li>" +
                $"<li><strong>Last Name:</strong> {Encode(contact.LastName)}</li>" +
                $"<li><strong>Email:</strong> {Encode(contact.Email)}</li>" +
                $"<li><strong>Phone:</strong> {Encode(contact.Phone)}</li>" +
                $"<li><strong>Company:</strong> {Encode(contact.Company)}</li>" +
                $"<li><strong>Service:</strong> {Encode(contact.Service)}</li>" +
                $"<li><strong>Budget:</strong> {Encode(contact.Budget)}</li>" +
                $"<li><strong>Message:</strong> {Encode(contact.Message)}</li>" +
                $"<li><strong>Terms Of Service:</strong> {contact.TermsOfService}</li>" +
                "</ul>";

            var internalEmailSent = await _emailService.SendAsync(
                "contact@sdmtek.com",
                "New Contact Form Submission",
                internalMessage);

            if (!internalEmailSent)
            {
                _logger.LogWarning("Contact was saved but internal notification email could not be sent for contact id: {Id}", contact.Id);
            }
            
            return CreatedAtAction(nameof(GetContact), new { id = contact.Id }, contact);
        }

        private static string Encode(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}