using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

//此表用于存储行程分割后的数据
namespace EzzLocGpsService.Models
{
    [Table("trip_segments")]
    public class TripSegment : BaseModel
    {
        [PrimaryKey("id", false)] // false because it's an auto-incrementing identity column
        public int Id { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("global_vehicle_no")]
        public int GlobalVehicleNo { get; set; }

        [Column("start_time")]
        public DateTimeOffset? StartTime { get; set; }

        [Column("end_time")]
        public DateTimeOffset? EndTime { get; set; }

        [Column("trip_date")]
        public DateTime? TripDate { get; set; }

        [Column("day_segment_number")]
        public short? DaySegmentNumber { get; set; }

        [Column("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [Column("distance_meters")]
        public int? DistanceMeters { get; set; }

        [Column("start_coordinates_row_id")]
        public long StartCoordinatesRowId { get; set; }

        [Column("end_coordinates_row_id")]
        public long EndCoordinatesRowId { get; set; }

    }
} 