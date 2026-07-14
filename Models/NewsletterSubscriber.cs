namespace backend.Models
{
    public class NewsletterSubscriber
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime SubscribedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}
