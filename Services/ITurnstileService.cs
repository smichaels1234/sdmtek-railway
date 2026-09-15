namespace backend.Services
{
    public interface ITurnstileService
    {
        bool IsConfigured { get; }

        Task<bool> VerifyAsync(
            string token,
            string expectedAction,
            string? remoteIpAddress,
            CancellationToken cancellationToken = default);
    }
}
