using GnosisRealmCore.Data;
using GnosisRealmCore.Infrastructure;
using GnosisRealmCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnosisRealmCore.Controllers;

[ApiController]
[Route("api/internal/nodes")]
public sealed class NodesController : ControllerBase
{
    private readonly RealmDbContext _dbContext;
    private readonly IServiceRequestAuthenticator _serviceRequestAuthenticator;

    public NodesController(RealmDbContext dbContext, IServiceRequestAuthenticator serviceRequestAuthenticator)
    {
        _dbContext = dbContext;
        _serviceRequestAuthenticator = serviceRequestAuthenticator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterNodeRequest request, CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.NodeRegister, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var entity = await _dbContext.RealmNodes.FirstOrDefaultAsync(x => x.NodeId == request.NodeId, cancellationToken);
        if (entity is null)
        {
            entity = new RealmNode
            {
                NodeId = request.NodeId,
                Name = request.DisplayName,
                ApiUrl = request.ApiUrl,
                PublicIp = request.PublicIp,
                MaxZones = request.MaxZones,
                Status = "online",
                LastHeartbeatUtc = DateTime.UtcNow
            };

            _dbContext.RealmNodes.Add(entity);
        }
        else
        {
            entity.Name = request.DisplayName;
            entity.ApiUrl = request.ApiUrl;
            entity.PublicIp = request.PublicIp;
            entity.MaxZones = request.MaxZones;
            entity.Status = "online";
            entity.LastHeartbeatUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { registered = true });
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] NodeHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.NodeHeartbeat, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var entity = await _dbContext.RealmNodes.FirstOrDefaultAsync(x => x.NodeId == request.NodeId, cancellationToken);
        if (entity is null)
        {
            return NotFound(new { error = "Node is not registered." });
        }

        entity.Status = request.Status;
        entity.ActiveZones = request.ActiveZones;
        entity.LastHeartbeatUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { accepted = true });
    }

    [HttpGet("{nodeId}/commands/next")]
    public async Task<IActionResult> GetNextCommand(string nodeId, CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.NodeCommandsRead, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var command = await _dbContext.RealmNodeCommands
            .Where(x => x.NodeId == nodeId && x.Status == "pending")
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (command is null)
        {
            return NoContent();
        }

        command.Status = "claimed";
        command.ClaimedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new NodeCommandResponse
        {
            CommandId = command.Id,
            CommandType = command.CommandType,
            PayloadJson = command.PayloadJson
        });
    }

    [HttpPost("{nodeId}/commands/{commandId:long}/ack")]
    public async Task<IActionResult> Ack(string nodeId, long commandId, [FromBody] NodeCommandAckRequest request, CancellationToken cancellationToken)
    {
        if (!_serviceRequestAuthenticator.TryAuthenticate(Request, out var context, out var error))
        {
            return Unauthorized(new { error });
        }

        if (context is null || !context.Roles.Contains(ServiceRoles.NodeCommandsRead, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var command = await _dbContext.RealmNodeCommands
            .FirstOrDefaultAsync(x => x.Id == commandId && x.NodeId == nodeId, cancellationToken);

        if (command is null)
        {
            return NotFound(new { error = "Command was not found." });
        }

        command.Status = request.Success ? "completed" : "failed";
        command.CompletedAtUtc = DateTime.UtcNow;
        command.ErrorText = request.ErrorText;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { acknowledged = true });
    }
}
