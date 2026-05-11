using GnosisAuthServer.Data;
using GnosisAuthServer.Infrastructure;
using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Controllers;

[ApiController]
[Route("api/admin/accounts")]
public sealed class AdminAccountsController : ControllerBase
{
    private readonly IAdminRequestValidator _adminRequestValidator;
    private readonly IAccountAccessValidator _accountAccessValidator;
    private readonly AuthDbContext _dbContext;

    public AdminAccountsController(
        IAdminRequestValidator adminRequestValidator,
        IAccountAccessValidator accountAccessValidator,
        AuthDbContext dbContext)
    {
        _adminRequestValidator = adminRequestValidator;
        _accountAccessValidator = accountAccessValidator;
        _dbContext = dbContext;
    }

    [HttpPut("ban")]
    [EnableRateLimiting("admin-write")]
    public async Task<IActionResult> UpdateBanStatus([FromBody] AdminAccountBanRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _adminRequestValidator.AuthorizeAsync(Request, cancellationToken);
        if (!authResult.IsAuthorized)
        {
            return Unauthorized(new { error = authResult.Error });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var steamId = request.SteamId.Trim();
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(x => x.SteamId == steamId, cancellationToken);
        if (account is null)
        {
            return NotFound(new { error = "Account was not found." });
        }

        account.IsBanned = request.IsBanned;
        account.BanReason = request.IsBanned
            ? string.IsNullOrWhiteSpace(request.BanReason) ? "Banned by administrator." : request.BanReason.Trim()
            : null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _accountAccessValidator.Invalidate(account.SteamId);

        return Ok(new
        {
            account.Id,
            account.SteamId,
            account.IsBanned,
            account.BanReason
        });
    }
}
