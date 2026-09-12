namespace backend.Models
{
    public class CreateNewsletterIssueRequest
    {
        public string? Subject { get; set; }
        public string? HtmlBody { get; set; }
        public DateTime? ScheduledDate { get; set; }
    }
}
