// Services/EzzLocService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using EzzLocGpsService.Models;

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

        public async Task SyncOnceAsync()
        {
            try 
            {
                _logger.LogInformation("开始获取Token...");
                var token = await GetLatestTokenAsync();
                
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("未找到可用的Token");
                    return;
                }
                
                _logger.LogInformation("成功获取Token: {TokenPrefix}...", token.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行同步时发生错误");
            }
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
          

        private async Task<VehicleGpsTrackData?> FetchEzzLocDataAsync(string token)
        {
            var url = $"https://www.ezzloc.net/gpsapi?vehicleid=1053633&token={token}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<VehicleGpsTrackData>(json);
            }

            _logger.LogError($"Fetch failed: {response.StatusCode}");
            return null;
        }

        private async Task SaveDataToSupabaseAsync(VehicleGpsTrackData data)
        {
            var response = await _supabase.From<VehicleGpsTrackData>().Insert(data);
            _logger.LogInformation("Data saved to Supabase.");
        }
    }

   
}