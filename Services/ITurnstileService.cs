namespace backend.Services
{
    public interface ITurnstileService
    {
        Task<bool> VerifyAsync(
            string token,
            string expectedAction,
            string? remoteIpAddress,
            CancellationToken cancellationToken = default);
    }
}
