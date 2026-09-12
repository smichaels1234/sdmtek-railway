namespace backend.Models
{
    // A single newsletter email to be sent out to all active subscribers.
    public class NewsletterIssue
    {
        public Guid Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // When null, the issue is a draft and will not be sent automatically.
        public DateTime? ScheduledDate { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
