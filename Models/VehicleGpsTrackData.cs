using Supabase.Postgrest.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EzzLocGpsService.Models
{
    [Table("vehicle_gps_track_data")]
    public class VehicleGpsTrackData:BaseModel
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("vehicle_id")]
        [Required]
        public int VehicleId { get; set; }

        [Column("gps_time")]
        [Required]
        public DateTime GpsTime { get; set; }

        [Column("lat")]
        [Required]
        public double Lat { get; set; }

        [Column("lon")]
        [Required]
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
        [MaxLength(500)]
        public string? RawStatus { get; set; }

        [Column("inserted_at")]
        public DateTime? InsertedAt { get; set; } = DateTime.UtcNow;
    }
}