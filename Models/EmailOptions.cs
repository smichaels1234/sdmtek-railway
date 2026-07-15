namespace backend.Models
{
    public class EmailOptions
    {
        public string ResendApiKey { get; set; } = string.Empty;
        public string SenderName { get; set; } = "SDMTek";
        public string SenderEmail { get; set; } = string.Empty;
    }
}
