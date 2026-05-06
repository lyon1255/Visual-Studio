using System.ComponentModel.DataAnnotations;

namespace GnosisRealmCore.Models;

public sealed class RegisterNodeRequest
{
    [Required, MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string ApiUrl { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PublicIp { get; set; } = "127.0.0.1";

    [Range(1, 1024)]
    public int MaxZones { get; set; } = 10;
}

public sealed class NodeHeartbeatRequest
{
    [Required, MaxLength(64)]
    public string NodeId { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Status { get; set; } = "online";

    [Range(0, 1024)]
    public int ActiveZones { get; set; }
}

public sealed class NodeCommandResponse
{
    public long CommandId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public sealed class NodeCommandAckRequest
{
    public bool Success { get; set; }
    [MaxLength(1024)]
    public string? ErrorText { get; set; }
}
