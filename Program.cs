using Serilog;
using EzzLocGpsService.Services;
using Supabase;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加服务到容器
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册 HttpClient
builder.Services.AddHttpClient();

// 注册 Supabase 客户端
builder.Services.AddSingleton<Supabase.Client>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var url = configuration["Supabase:Url"];
    var key = configuration["Supabase:Key"];

    var options = new SupabaseOptions
    {
        AutoConnectRealtime = true
    };

    var client = new Supabase.Client(url, key, options);
    client.InitializeAsync().Wait();
    return client;
});

// 添加 Hangfire 服务
builder.Services.AddHangfire(configuration => 
    configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                 .UseSimpleAssemblyNameTypeSerializer()
                 .UseRecommendedSerializerSettings()
                 .UseSqlServerStorage(
                     builder.Configuration.GetConnectionString("HangfireSqlServerConnection"),
                     new SqlServerStorageOptions
                     {
                         SchemaName = "dbo",
                         CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                         SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                         QueuePollInterval = TimeSpan.FromSeconds(15),
                         UseRecommendedIsolationLevel = true,
                         DisableGlobalLocks = true
                     })
);

// 添加 Hangfire 服务器
builder.Services.AddHangfireServer();

// 注册服务
builder.Services.AddScoped<EzzLocService>();

// 注册后台任务服务
builder.Services.AddScoped<EzzLocBackgroundJobWithHangFire>();
builder.Services.AddScoped<EzzLocGpsService.Background.TripSegmentJob>();


// 添加健康检查
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.UseAuthorization();
app.UseStaticFiles();

// 配置 Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllConnectionsFilter() }
});



// 添加健康检查端点
app.MapHealthChecks("/health");



// 配置定时任务
RecurringJob.AddOrUpdate<EzzLocBackgroundJobWithHangFire>(
    "sync-gps-data",
    job => job.ExecuteAsync(),
    "*/3 * * * *"  // Cron 表达式：每3分钟执行一次
);

RecurringJob.AddOrUpdate<EzzLocGpsService.Background.TripSegmentJob>(
    "process-trip-segments",
    job => job.ExecuteAsync(),
    "0 1 * * *" // Cron 表达式：每天凌晨1点执行
);


try
{
    Log.Information("启动应用程序...");
    app.Run();
}
catch (Exception ex)
{
   Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}

public class AllowAllConnectionsFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

