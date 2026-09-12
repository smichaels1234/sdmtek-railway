namespace backend.Services
{
    // Shared HTML helpers for outgoing newsletter/subscription emails.
    public static class NewsletterEmailBuilder
    {
        public static string BuildUnsubscribeFooter(string baseUrl, Guid unsubscribeToken)
        {
            var unsubscribeUrl = $"{baseUrl.TrimEnd('/')}/api/newsletter/unsubscribe?token={unsubscribeToken}";
            return $"<hr/><p style=\"font-size:12px;color:#666\">Don't want these emails? <a href=\"{unsubscribeUrl}\">Unsubscribe</a></p>";
        }
    }
}
