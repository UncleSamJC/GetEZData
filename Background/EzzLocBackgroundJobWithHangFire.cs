using EzzLocGpsService.Services;
using Hangfire;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using EzzLocGpsService.Models;


public class EzzLocBackgroundJobWithHangFire
{
    private readonly ILogger<EzzLocBackgroundJobWithHangFire> _logger;
    private readonly EzzLocService _ezzLocService;
    private readonly Supabase.Client _supabase;
    private const int REALTIME_SYNC_INTERVAL_MINUTES = 10;
    private const int CATCHUP_BATCH_MINUTES = 60; // 追赶模式下每次同步60分钟
    private const int DAYS_TO_KEEP = 3;

    public EzzLocBackgroundJobWithHangFire(
        ILogger<EzzLocBackgroundJobWithHangFire> logger,
        EzzLocService ezzLocService,
        Supabase.Client supabase)
    {
        _logger = logger;
        _ezzLocService = ezzLocService;
        _supabase = supabase;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            // Step 1: Load sync_metadata
            var metadata = await GetOrCreateSyncMetadata();
            var now = DateTime.UtcNow;

            var from = metadata.LastSyncedAt;
            var to = now;

            // Step 2: 根据模式确定同步的结束时间 'to'
            if (metadata.Mode == "catchup")
            {
                // 追赶模式下，每次最多同步 CATCHUP_BATCH_MINUTES 分钟
                to = from.AddMinutes(CATCHUP_BATCH_MINUTES);
                if (to >= now)
                {
                    // 如果追赶窗口已经超过或等于当前时间，说明即将追上
                    to = now;
                    metadata.Mode = "realtime"; // 准备切换到实时模式
                }
            }
            else // realtime 模式
            {
                // 实时模式下，同步到当前时间
                // 同时确保 from 时间不会太早，避免重复同步大量数据
                var potentialFrom = now.AddMinutes(-REALTIME_SYNC_INTERVAL_MINUTES * 2); // 例如，最多往前追溯2个周期
                if (from < potentialFrom)
                {
                    from = potentialFrom;
                }
                to = now;
            }

            _logger.LogInformation(
                "开始同步数据 Mode: {Mode}, From: {From}, To: {To}", 
                metadata.Mode, 
                from.ToString("yyyy-MM-dd HH:mm:ss"), 
                to.ToString("yyyy-MM-dd HH:mm:ss")
            );

            // Step 3: 同步数据
            var fromMs = new DateTimeOffset(from).ToUnixTimeMilliseconds();
            var toMs = new DateTimeOffset(to).ToUnixTimeMilliseconds();
            await _ezzLocService.FetchEzzLocDataAsync(fromMs, toMs);

            // Step 4: 更新sync_metadata
            await UpdateSyncMetadata(to, metadata.Mode);

            _logger.LogInformation(
                "同步完成 Mode: {Mode}, LastSyncedAt: {LastSyncedAt}", 
                metadata.Mode, 
                to.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行同步GPS数据时任务发生错误");
            throw;
        }
    }

    private async Task<SyncMetadata> GetOrCreateSyncMetadata()
    {
        try
        {
            var result = await _supabase
                .From<SyncMetadata>()
                .Select("*")
                .Single();

            if (result != null)
            {
                // 如果模式是实时，但上次同步时间过旧，切换回追赶模式
                if (result.Mode == "realtime" && result.LastSyncedAt < DateTime.UtcNow.AddMinutes(-REALTIME_SYNC_INTERVAL_MINUTES * 3))
                {
                    _logger.LogWarning("实时模式下同步中断过久，切换回追赶模式。LastSyncedAt: {LastSyncedAt}", result.LastSyncedAt);
                    result.Mode = "catchup";
                }
                return result;
            }

            // 如果没有记录，创建新记录，从3天前开始同步
            var initialMetadata = new SyncMetadata
            {
                LastSyncedAt = DateTime.UtcNow.AddDays(-DAYS_TO_KEEP),
                Mode = "catchup",
                UpdatedAt = DateTime.UtcNow
            };

            await _supabase
                .From<SyncMetadata>()
                .Insert(initialMetadata);

            return initialMetadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取或创建同步元数据时发生错误");
            throw;
        }
    }

    private async Task UpdateSyncMetadata(DateTime lastSyncedAt, string mode)
    {
        try
        {
            var updateData = new SyncMetadata
            {
                Id = "gps_sync",
                LastSyncedAt = lastSyncedAt,
                Mode = mode,
                UpdatedAt = DateTime.UtcNow
            };

            await _supabase
                .From<SyncMetadata>()
                .Update(updateData);

            _logger.LogInformation(
                "更新同步元数据 LastSyncedAt: {LastSyncedAt}, Mode: {Mode}", 
                lastSyncedAt.ToString("yyyy-MM-dd HH:mm:ss"), 
                mode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新同步元数据时发生错误");
            throw;
        }
    }
}


    
