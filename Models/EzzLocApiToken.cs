using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace EzzLocGpsService.Models
{
    [Table("ezzloc_api_tokens")]
    public class EzzLocApiToken : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
