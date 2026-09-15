namespace backend.Models
{
    public class SubscribeNewsletterRequest
    {
        public string? Email { get; set; }
        public string? CaptchaToken { get; set; }
    }
}
