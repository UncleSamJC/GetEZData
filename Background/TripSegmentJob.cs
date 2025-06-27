using EzzLocGpsService.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EzzLocGpsService.Background
{
    //此功能用于将行程分割成多个段，每个段包含一个起点和终点，以及一个时间范围
    //每天凌晨1点执行一次，由hangFire调度
    //1. 获取所有行程的GPS数据
    //2.找到所有的车辆的编号，存入一个数组
    //3. 遍历数组，获取每个车辆的GPS数据
    //4. 将GPS数据分割成多个段
    //5. 将分割后的trip segment数据保存到数据库
    public class TripSegmentJob
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly ILogger<TripSegmentJob> _logger;

        public TripSegmentJob(Supabase.Client supabaseClient, ILogger<TripSegmentJob> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        /// <summary>
        /// 由Hangfire调度的入口方法。
        /// 该作业在每天凌晨运行，处理前一天的数据。
        /// </summary>
        public async Task ExecuteAsync()
        {
            var dateToProcess = DateTime.UtcNow.Date.AddDays(-1);
            _logger.LogInformation("启动 TripSegmentJob，处理日期: {Date}", dateToProcess.ToString("yyyy-MM-dd"));

            try
            {
                // 步骤 2: 找到所有车辆的编号
                var vehicleNumbers = await GetDistinctVehicleNumbersAsync(dateToProcess);
                _logger.LogInformation("发现 {Count} 辆车需要处理。", vehicleNumbers.Count);

                // 步骤 3: 遍历数组，获取每个车辆的GPS数据并处理
                foreach (var vehicleNo in vehicleNumbers)
                {
                    await ProcessVehicleAsync(vehicleNo, dateToProcess);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 {Date} 的行程分段时发生错误。", dateToProcess.ToString("yyyy-MM-dd"));
            }

            _logger.LogInformation("完成 TripSegmentJob，处理日期: {Date}", dateToProcess.ToString("yyyy-MM-dd"));
        }

        private async Task<List<int>> GetDistinctVehicleNumbersAsync(DateTime date)
        {
            var startDate = date.ToString("o");
            var endDate = date.AddDays(1).ToString("o");

            var response = await _supabaseClient.From<VehicleGpsTrackData>()
                .Select(x => new object[] { x.GlobalVehicleNo })
                .Filter("gps_time_utc", Supabase.Postgrest.Constants.Operator.GreaterThan, startDate)
                .Filter("gps_time_utc", Supabase.Postgrest.Constants.Operator.LessThan, endDate)
                .Get();
            
            if (response.Models == null) return new List<int>();

            return response.Models.Select(x => x.GlobalVehicleNo).Distinct().ToList();
        }
        
        private async Task ProcessVehicleAsync(int vehicleNo, DateTime date)
        {
            _logger.LogInformation("正在处理车辆: {VehicleNo}，日期: {Date}", vehicleNo, date.ToString("yyyy-MM-dd"));

            // 步骤 1 & 4: 获取单个车辆的GPS数据并进行分割
            var startDate = date.ToString("o");
            var endDate = date.AddDays(1).ToString("o");

            var response = await _supabaseClient.From<VehicleGpsTrackData>()
                .Where(x => x.GlobalVehicleNo == vehicleNo)
                .Filter("gps_time_utc", Supabase.Postgrest.Constants.Operator.GreaterThan, startDate)
                .Filter("gps_time_utc", Supabase.Postgrest.Constants.Operator.LessThan, endDate)
                .Order(x => x.GpsTimeUtc, Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var gpsPoints = response.Models;

            if (gpsPoints == null || !gpsPoints.Any())
            {
                _logger.LogWarning("车辆 {VehicleNo} 在 {Date} 没有任何GPS数据。", vehicleNo, date.ToString("yyyy-MM-dd"));
                return;
            }

            var segments = CreateSegmentsFromGpsData(gpsPoints, vehicleNo, date);
            
            _logger.LogInformation("为车辆 {VehicleNo} 创建了 {Count} 个行程段。", vehicleNo, segments.Count);

            // 步骤 5: 将分割后的trip segment数据保存到数据库
            if (segments.Any())
            {
                try
                {
                    await _supabaseClient.From<TripSegment>().Insert(segments);
                     _logger.LogInformation("成功为车辆 {VehicleNo} 保存了 {Count} 个行程段。", vehicleNo, segments.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "为车辆 {VehicleNo} 保存行程段时出错。", vehicleNo);
                }
            }
        }

        private List<TripSegment> CreateSegmentsFromGpsData(List<VehicleGpsTrackData> gpsPoints, int vehicleNo, DateTime date)
        {
            var segments = new List<TripSegment>();
            TripSegment currentSegment = null;
            VehicleGpsTrackData lastPoint = null;
            double currentSegmentDistance = 0;

            foreach (var point in gpsPoints)
            {
                // 行程开始：ACC 从关到开
                if (point.AccStatus == true && currentSegment == null)
                {
                    currentSegment = new TripSegment
                    {
                        GlobalVehicleNo = vehicleNo,
                        TripDate = date,
                        StartTime = point.GpsTimeUtc,
                        StartCoordinatesRowId = point.Id
                    };
                    currentSegmentDistance = 0;
                }
                // 行程结束：ACC 从开到关
                else if (point.AccStatus == false && currentSegment != null)
                {
                    currentSegment.EndTime = point.GpsTimeUtc;
                    currentSegment.EndCoordinatesRowId = point.Id;
                    currentSegment.DurationSeconds = (int)(currentSegment.EndTime.Value - currentSegment.StartTime.Value).TotalSeconds;
                    currentSegment.DistanceMeters = (int)Math.Round(currentSegmentDistance);
                    currentSegment.DaySegmentNumber = (short)(segments.Count + 1);
                    segments.Add(currentSegment);
                    currentSegment = null;
                }

                // 如果行程正在进行中，累加距离
                if (currentSegment != null && lastPoint != null && lastPoint.AccStatus == true)
                {
                    currentSegmentDistance += CalculateHaversineDistance(lastPoint.Lat, lastPoint.Lon, point.Lat, point.Lon);
                }
                
                lastPoint = point;
            }

            // 处理当天结束时行程还未结束的情况
            if (currentSegment != null && lastPoint != null)
            {
                currentSegment.EndTime = lastPoint.GpsTimeUtc;
                currentSegment.EndCoordinatesRowId = lastPoint.Id;
                currentSegment.DurationSeconds = (int)(currentSegment.EndTime.Value - currentSegment.StartTime.Value).TotalSeconds;
                currentSegment.DistanceMeters = (int)Math.Round(currentSegmentDistance);
                currentSegment.DaySegmentNumber = (short)(segments.Count + 1);
                segments.Add(currentSegment);
            }

            return segments;
        }

        /// <summary>
        /// 使用Haversine公式计算两个GPS坐标之间的距离（单位：米）。
        /// </summary>
        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371e3; // 地球半径（米）
            var phi1 = lat1 * Math.PI / 180;
            var phi2 = lat2 * Math.PI / 180;
            var deltaPhi = (lat2 - lat1) * Math.PI / 180;
            var deltaLambda = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                    Math.Cos(phi1) * Math.Cos(phi2) *
                    Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }
    }
}
