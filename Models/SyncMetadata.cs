using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EzzLocGpsService.Models
{
    [Table("sync_metadata")]
    public class SyncMetadata : BaseModel
    {
        [PrimaryKey("id")]
        public string Id { get; set; } = "gps_sync";

        [Column("last_synced_at")]
        public DateTime LastSyncedAt { get; set; }

        [Column("mode")]
        public string Mode { get; set; } = "catchup";

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}

