using EzzLocGpsService.Services;

public class EzzLocBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EzzLocBackgroundService> _logger;

    public EzzLocBackgroundService(IServiceProvider serviceProvider, ILogger<EzzLocBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<EzzLocService>();

            try
            {
                await service.SyncOnceAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during EzzLoc sync.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // 可以调整时间间隔
        }
    }
}
