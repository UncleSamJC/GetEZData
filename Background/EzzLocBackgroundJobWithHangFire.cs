using EzzLocGpsService.Services;
using Hangfire;

public class EzzLocBackgroundJobWithHangFire
{
    private readonly ILogger<EzzLocBackgroundJobWithHangFire> _logger;
    private readonly EzzLocService _ezzLocService;

        public EzzLocBackgroundJobWithHangFire(
        ILogger<EzzLocBackgroundJobWithHangFire> logger,
        EzzLocService ezzLocService)
    {
        _logger = logger;
        _ezzLocService = ezzLocService;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            _logger.LogInformation("Starting GPS data sync job");
            await _ezzLocService.SyncOnceAsync();
            _logger.LogInformation("Completed GPS data sync job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GPS data sync job");
            throw; // 让 Hangfire 知道任务失败，以便重试
        }
    }
}