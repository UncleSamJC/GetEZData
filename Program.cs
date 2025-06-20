using Serilog;
using EzzLocGpsService.Services;
using Supabase;

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

// 注册服务
builder.Services.AddScoped<EzzLocService>();

// 注册后台服务
builder.Services.AddHostedService<EzzLocBackgroundService>();

// 添加健康检查
builder.Services.AddHealthChecks();

var app = builder.Build();

// 配置 HTTP 请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 添加健康检查端点
app.MapHealthChecks("/health");

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

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
