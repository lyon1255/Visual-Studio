using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthDbContext _dbContext;
    private readonly ISteamTicketValidator _steamTicketValidator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthDbContext dbContext,
        ISteamTicketValidator steamTicketValidator,
        IJwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _steamTicketValidator = steamTicketValidator;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("steam")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<SteamLoginResponse>> LoginWithSteam([FromBody] SteamLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var steamId = request.SteamId.Trim();
        var ticket = request.Ticket.Trim();

        var validation = await _steamTicketValidator.ValidateAsync(steamId, ticket, cancellationToken);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Steam authentication rejected for SteamId {SteamId}. Reason: {Reason}", steamId, validation.Error);
            return Unauthorized(new { error = validation.Error ?? "Steam authentication failed." });
        }

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(x => x.SteamId == steamId, cancellationToken);
        if (account is null)
        {
            account = new Account
            {
                SteamId = steamId,
                CreatedAtUtc = DateTime.UtcNow,
                AccountType = "player"
            };

            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (account.IsBanned)
        {
            return Unauthorized(new { error = account.BanReason ?? "This account is banned." });
        }

        account.LastLoginAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SteamLoginResponse
        {
            AccessToken = _jwtTokenService.CreateAccessToken(account),
            ExpiresInSeconds = _jwtTokenService.GetAccessTokenLifetimeSeconds(),
            SteamId = account.SteamId,
            AccountId = account.Id
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthMeResponse>> Me(CancellationToken cancellationToken)
    {
        var steamId = User.FindFirst("sub")?.Value ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return Unauthorized(new { error = "Missing subject claim." });
        }

        var account = await _dbContext.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.SteamId == steamId, cancellationToken);
        if (account is null)
        {
            return Unauthorized(new { error = "Account was not found." });
        }

        return Ok(new AuthMeResponse
        {
            AccountId = account.Id,
            SteamId = account.SteamId,
            AccountType = account.AccountType,
            IsBanned = account.IsBanned
        });
    }
}
