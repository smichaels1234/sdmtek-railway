using backend.Models;
using backend.Services;
using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SDMTech.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private const string RecaptchaVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";
        private readonly ILogger<ContactController> _logger;
        private readonly SDMTekContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public ContactController(
            ILogger<ContactController> logger,
            SDMTekContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IEmailService emailService)
        {
            _logger = logger;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _emailService = emailService;
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

            var isCaptchaValid = await VerifyCaptchaAsync(request.CaptchaToken);
            if (!isCaptchaValid)
            {
                return BadRequest("Captcha validation failed.");
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

        private async Task<bool> VerifyCaptchaAsync(string captchaToken)
        {
            var secretKey = _configuration["Captcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogError("Captcha secret key is not configured.");
                return false;
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = secretKey,
                    ["response"] = captchaToken,
                    ["remoteip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
                });

                var response = await client.PostAsync(RecaptchaVerifyUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Captcha verification API returned status code {StatusCode}", response.StatusCode);
                    return false;
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync();
                var captchaResponse = await JsonSerializer.DeserializeAsync<RecaptchaVerifyResponse>(
                    responseStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (captchaResponse?.Success != true)
                {
                    _logger.LogWarning(
                        "Captcha validation failed. Error codes: {ErrorCodes}",
                        captchaResponse?.ErrorCodes is { Length: > 0 }
                            ? string.Join(",", captchaResponse.ErrorCodes)
                            : "none");
                }

                return captchaResponse?.Success ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Captcha verification failed due to an exception.");
                return false;
            }
        }

        private sealed class RecaptchaVerifyResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}