using System.Text.Json.Serialization;

namespace EzzLocGpsService.Models
{
    public class EzzLocResponse
    {
        [JsonPropertyName("cmd")]
        public string Cmd { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public int Result { get; set; }

        [JsonPropertyName("resultNote")]
        public string ResultNote { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public EzzLocDetail Detail { get; set; } = new();
    }

    public class EzzLocDetail
    {
        [JsonPropertyName("data")]
        public List<GpsDataPoint> Data { get; set; } = new();
    }

    public class GpsDataPoint
    {
        [JsonPropertyName("VehicleID")]
        public int VehicleID { get; set; }

        [JsonPropertyName("GpsTime")]
        public long GpsTime { get; set; }

        [JsonPropertyName("Direction")]
        public int Direction { get; set; }

        [JsonPropertyName("Lat")]
        public double Lat { get; set; }

        [JsonPropertyName("Lon")]
        public double Lon { get; set; }

        [JsonPropertyName("Speed")]
        public double Speed { get; set; }

        [JsonPropertyName("Odometer")]
        public double Odometer { get; set; }

        [JsonPropertyName("LoStatus")]
        public int LoStatus { get; set; }

        [JsonPropertyName("Status")]
        public string Status { get; set; } = string.Empty;
    }
}
