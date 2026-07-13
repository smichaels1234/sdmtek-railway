using backend.Models;
using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public ContactController(
            ILogger<ContactController> logger,
            SDMTekContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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
            
            return CreatedAtAction(nameof(GetContact), new { id = contact.Id }, contact);
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