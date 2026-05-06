using System.ComponentModel.DataAnnotations.Schema;

namespace GnosisRealmCore.Data
{
    [Table("realm_nodes")]
    public class RealmNode
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = string.Empty;
        [Column("api_url")] public string ApiUrl { get; set; } = string.Empty; // pl: http://192.168.1.50:5159
        [Column("api_key")] public string ApiKey { get; set; } = string.Empty;
        [Column("max_zones")] public int MaxZones { get; set; } = 10;
        [Column("active_zones")] public int ActiveZones { get; set; } = 0;
    }
}