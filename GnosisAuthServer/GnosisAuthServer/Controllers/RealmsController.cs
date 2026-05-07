using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GnosisAuthServer.Controllers;

[ApiController]
public sealed class RealmsController : ControllerBase
{
    private readonly IRealmRegistryService _realmRegistryService;

    public RealmsController(IRealmRegistryService realmRegistryService)
    {
        _realmRegistryService = realmRegistryService;
    }

    [Authorize]
    [HttpGet("api/realms")]
    [HttpGet("api/auth/servers")]
    [EnableRateLimiting("realm-list")]
    public async Task<ActionResult<IReadOnlyList<RealmListItemResponse>>> GetRealms(CancellationToken cancellationToken)
    {
        var realms = await _realmRegistryService.GetPublicRealmsAsync(cancellationToken);

        var response = realms.Select(x => new RealmListItemResponse
        {
            RealmId = x.RealmId,
            DisplayName = x.DisplayName,
            Region = x.Region,
            RealmType = x.RealmType,
            Kind = x.Kind,
            PublicBaseUrl = x.PublicBaseUrl,
            Status = x.Status,
            CurrentPlayers = x.CurrentPlayers,
            MaxPlayers = x.MaxPlayers,
            HealthyZoneCount = x.HealthyZoneCount,
            LastHeartbeatAtUtc = x.LastHeartbeatAt
        }).ToList();

        return Ok(response);
    }
}