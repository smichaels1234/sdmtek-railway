namespace backend.Models
{
    public class EmailOptions
    {
        public string ResendApiKey { get; set; } = string.Empty;
        public string SenderName { get; set; } = "SDMTek";
        public string SenderEmail { get; set; } = string.Empty;
    }

    public class NewsletterOptions
    {
        // Base URL (e.g. https://sdmtek.com) used to build unsubscribe links when there is no HTTP request in scope (background sends).
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string AdminApiKey { get; set; } = string.Empty;
    }
}
