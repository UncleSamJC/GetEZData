// Services/EzzLocService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using EzzLocGpsService.Models;
using Supabase.Postgrest;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EzzLocGpsService.Services
{
    public class EzzLocService
    {
        private readonly Supabase.Client _supabase;
        private readonly HttpClient _httpClient;
        private readonly ILogger<EzzLocService> _logger;

        public EzzLocService(Supabase.Client supabase, HttpClient httpClient, ILogger<EzzLocService> logger)
        {
            _supabase = supabase;
            _httpClient = httpClient;
            _logger = logger;
        }

        private async Task<string?> GetLatestTokenAsync()
        {
            try
            {
                var result = await _supabase
                    .From<EzzLocApiToken>()
                    .Select("*")
                    .Order("id", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();

                var token = result.Models.FirstOrDefault();
                
                if (token == null)
                {
                    _logger.LogWarning("数据库中没有找到活动的Token");
                    return null;
                }

                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取Token时发生错误");
                return null;
            }
        }

        public async Task FetchEzzLocDataAsync(long beginTime, long endTime)
        {
            string token = "";
            try
            {
                _logger.LogInformation("开始获取Token...");
                token = await GetLatestTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("未找到可用的Token");
                    return;
                }

                _logger.LogInformation("成功获取Token: {TokenPrefix}...", token[..Math.Min(token.Length, 6)]);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Token获取失败了...");
            }


            try
                {               
                // 构建请求体
                var request = new EzzLocRequest
                {
                    Cmd = "GetTrackDetail",
                    Token = token,
                    Language = 2,
                    Params = new EzzLocRequestParams
                    {
                        VehicleID = "1053633",
                        BeginTime = beginTime,
                        EndTime = endTime
                    }
                };

                // 序列化请求体
                var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // 创建 StringContent
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("正在请求 EzzLoc API，时间范围: {BeginTime} 到 {EndTime}", beginTime, endTime);
                _logger.LogDebug("请求内容: {RequestContent}", jsonContent);

                // 发送 POST 请求
                var response = await _httpClient.PostAsync("https://www.ezzloc.net/gpsapi", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("请求失败: {StatusCode}, 错误内容: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("收到响应: {Response}", responseContent);

                // 反序列化响应
                var ezzlocResponse = JsonSerializer.Deserialize<EzzLocResponse>(responseContent, 
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (ezzlocResponse?.Result != 1)
                {
                    _logger.LogError("API返回错误: {ResultNote}", ezzlocResponse?.ResultNote ?? "Unknown error");
                    return;
                }

                if (ezzlocResponse != null && ezzlocResponse.Detail?.Data != null)
                {
                    _logger.LogInformation("获取到 {Count} 条GPS数据点", ezzlocResponse.Detail.Data.Count);

                    // 转换并保存数据
                    var trackDataList = ezzlocResponse.Detail.Data.Select(point => new VehicleGpsTrackData
                    {
                        GlobalVehicleNo = point.VehicleID,
                        GpsTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(point.GpsTime).UtcDateTime,
                        GpsTimeUnix = point.GpsTime,
                        Lat = point.Lat,
                        Lon = point.Lon,
                        Direction = point.Direction,
                        Speed = point.Speed,
                        Odometer = point.Odometer,
                        LoStatus = point.LoStatus,
                        RawStatus = point.Status,
                        // 解析 Status 字符串获取 ACC 状态和电压
                        AccStatus = point.Status.Contains("ACC开"),
                        Voltage = ExtractVoltage(point.Status),
                        InsertedAt = DateTime.UtcNow
                    }).ToList();

                    await SaveDataToSupabaseAsync(trackDataList);
                    _logger.LogInformation("成功保存 {Count} 条GPS数据到数据库", trackDataList.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "请求 EzzLoc API 时发生错误");
            }
        }

        private double? ExtractVoltage(string status)
        {
            try
            {
                var voltageMatch = Regex.Match(status, @"(\d+\.?\d*)V");
                if (voltageMatch.Success && double.TryParse(voltageMatch.Groups[1].Value, out double voltage))
                {
                    return voltage;
                }
            }
            catch
            {
                // 如果解析失败，返回 null
            }
            return null;
        }

        private async Task SaveDataToSupabaseAsync(List<VehicleGpsTrackData> dataList)
        {
            try
            {
                // 批量插入数据，每批200条
                const int batchSize = 200;

                if (dataList.Count <= batchSize)
                {
                    // 如果数据少于等于200条，直接一次插入
                    _logger.LogInformation("Inserting {Count} records in single batch", dataList.Count);
                    var response = await _supabase
                        .From<VehicleGpsTrackData>()
                        .Insert(dataList);
                    _logger.LogInformation("Successfully inserted {Count} records", dataList.Count);
                }
                else
                {
                    // 如果数据超过200条，分批插入
                    _logger.LogInformation("Total {Count} records, will insert in batches of {BatchSize}", dataList.Count, batchSize);
                    
                    // 计算需要多少批次
                    var totalBatches = (int)Math.Ceiling(dataList.Count / (double)batchSize);
                    
                    for (int i = 0; i < totalBatches; i++)
                    {
                        try
                        {
                            var batch = dataList
                                .Skip(i * batchSize)
                                .Take(batchSize)
                                .ToList();

                            _logger.LogInformation("Inserting batch {CurrentBatch}/{TotalBatches} with {BatchCount} records", 
                                i + 1, 
                                totalBatches, 
                                batch.Count);

                            var response = await _supabase
                                .From<VehicleGpsTrackData>()
                                .Insert(batch);

                            _logger.LogInformation("Successfully inserted batch {CurrentBatch}/{TotalBatches}", 
                                i + 1, 
                                totalBatches);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error inserting batch {CurrentBatch}/{TotalBatches}: {Message}", 
                                i + 1, 
                                totalBatches, 
                                ex.Message);
                            // 继续处理下一批，而不是完全中断
                            continue;
                        }

                        // 每批之间稍微暂停一下，避免对数据库造成太大压力
                        if (i < totalBatches - 1)  // 如果不是最后一批
                        {
                            await Task.Delay(1000);  // 暂停1秒
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "保存数据到 Supabase 时发生错误");
                throw;
            }
        }

       }
}