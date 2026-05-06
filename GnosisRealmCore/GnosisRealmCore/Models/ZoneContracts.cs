using System.ComponentModel.DataAnnotations;

namespace GnosisRealmCore.Models;

public sealed class ZoneLookupResponse
{
    public string ZoneName { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class ZoneHeartbeatRequest
{
    [Required, MaxLength(64)]
    public string ZoneName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string IpAddress { get; set; } = "127.0.0.1";

    [Range(1, 65535)]
    public int Port { get; set; }

    [MaxLength(16)]
    public string Status { get; set; } = "online";

    [Range(0, int.MaxValue)]
    public int CurrentPlayers { get; set; }

    [Range(0, int.MaxValue)]
    public int MaxPlayers { get; set; }
}
