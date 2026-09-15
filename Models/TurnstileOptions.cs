namespace backend.Models
{
    public class TurnstileOptions
    {
        public string? SecretKey { get; set; }
        public string[] AllowedHostnames { get; set; } = [];
    }
}
