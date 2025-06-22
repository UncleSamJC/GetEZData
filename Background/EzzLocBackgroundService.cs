using EzzLocGpsService.Models;
using EzzLocGpsService.Services;

public class EzzLocBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EzzLocBackgroundService> _logger;
     private readonly Supabase.Client _supabaseClient; 

    public EzzLocBackgroundService(IServiceProvider serviceProvider, ILogger<EzzLocBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting GPS data sync cycle...");
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var service = scope.ServiceProvider.GetRequiredService<EzzLocService>();
                    
                    try
                    {
                        await service.SyncOnceAsync();
                        _logger.LogInformation("Successfully completed GPS data sync one cycle");
                    }
                    catch (Exception serviceEx)
                    {
                        _logger.LogError(serviceEx, "Error during GPS data sync: {Message}, StackTrace: {StackTrace}", 
                            serviceEx.Message, 
                            serviceEx.StackTrace);
                    }
                } // scope 会在这里自动释放
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in background service: {Message}", ex.Message);
            }

            try
            {
                _logger.LogDebug("Waiting for {Minutes} minutes before next sync cycle", 1);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background service is stopping...");
                break;
            }
        }
    }
}
