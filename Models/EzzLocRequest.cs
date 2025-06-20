using System.Text.Json.Serialization;

namespace EzzLocGpsService.Models
{
    public class EzzLocRequest
    {
        [JsonPropertyName("cmd")]
        public string Cmd { get; set; } = "GetTrackDetail";

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public int Language { get; set; } = 2;

        [JsonPropertyName("params")]
        public EzzLocRequestParams Params { get; set; } = new();
    }

    public class EzzLocRequestParams
    {
        [JsonPropertyName("VehicleID")]
        public string VehicleID { get; set; } = "1053633";

        [JsonPropertyName("BeginTime")]
        public long BeginTime { get; set; }

        [JsonPropertyName("EndTime")]
        public long EndTime { get; set; }
    }
}