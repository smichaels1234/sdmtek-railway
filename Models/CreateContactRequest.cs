namespace backend.Models
{
    public class CreateContactRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public string? Service { get; set; }
        public string? Budget { get; set; }
        public string? Message { get; set; }
        public bool TermsOfService { get; set; }
        public string? CaptchaToken { get; set; }
    }
}