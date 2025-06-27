using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;  // 使用 Supabase 的特性


namespace EzzLocGpsService.Models
{
    [Table("vehicle_gps_track_data")]
    public class VehicleGpsTrackData:BaseModel
    {
        [PrimaryKey("id")]  // 改用 Supabase 的主键特性
        public long Id { get; set; }

        [Column("global_vehicle_no")]
        public int GlobalVehicleNo { get; set; }

        [Column("lat")]
        public double Lat { get; set; }

        [Column("lon")]
        public double Lon { get; set; }

        [Column("direction")]
        public int? Direction { get; set; }

        [Column("speed")]
        public double? Speed { get; set; }

        [Column("odometer")]
        public double? Odometer { get; set; }

        [Column("lo_status")]
        public int? LoStatus { get; set; }

        [Column("acc_status")]
        public bool? AccStatus { get; set; }

        [Column("voltage")]
        public double? Voltage { get; set; }

        [Column("raw_status")]
        public string? RawStatus { get; set; }

        [Column("inserted_at")]
        public DateTime? InsertedAt { get; set; } = DateTime.UtcNow;

        [Column("gps_time_utc")]
        public DateTime GpsTimeUtc { get; set; }

        [Column("gps_time_unix")]
        public long GpsTimeUnix { get; set; }



    }
}