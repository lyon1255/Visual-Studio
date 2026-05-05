using GnosisAuthServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GnosisAuthServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MasterDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(MasterDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public class SteamAuthRequest
        {
            public string Ticket { get; set; } = string.Empty;
            public string SteamId { get; set; } = string.Empty;
        }

        [HttpPost("steam")]
        public async Task<IActionResult> AuthenticateWithSteam([FromBody] SteamAuthRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.SteamId) || string.IsNullOrEmpty(request.Ticket))
                    return BadRequest(new { error = "Steam ID or Ticket is missing." });

                var steamApiKey = _configuration["Steam:WebApiKey"];
                if (string.IsNullOrEmpty(steamApiKey) || steamApiKey == "IDE_JON_A_STEAM_WEB_API_KULCSOD")
                {
                    _logger.LogCritical("Steam API Key is missing in configuration!");
                    // TESZT MÓD: Kikommenteztük, hogy kulcs nélkül is be lehessen lépni
                    // return StatusCode(500, new { error = "Server configuration error." });
                }

                // Steam hívás (Élesben majd vedd ki a kommentből!)
                /* 
                using var httpClient = new HttpClient();
                var steamAuthUrl = $"https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key={steamApiKey}&appid=TBD_APP_ID&ticket={request.Ticket}";
                var response = await httpClient.GetAsync(steamAuthUrl);
                if (!response.IsSuccessStatusCode)
                    return Unauthorized(new { error = "Invalid Steam Ticket." });
                */

                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.SteamId == request.SteamId);

                if (account == null)
                {
                    account = new Account
                    {
                        SteamId = request.SteamId,
                        IsBanned = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Accounts.Add(account);
                    await _context.SaveChangesAsync();
                }

                if (account.IsBanned)
                    return Unauthorized(new { error = "This account is banned." });

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
                {
                    _logger.LogCritical("JWT Signing Key is missing or too short!");
                    return StatusCode(500);
                }

                var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, account.SteamId),
                        new Claim("Gnosis_Session", Guid.NewGuid().ToString())
                    }),
                    Expires = DateTime.UtcNow.AddMinutes(30),
                    Issuer = _configuration["Jwt:Issuer"],
                    Audience = _configuration["Jwt:Audience"],
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(keyBytes),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                string jwtString = tokenHandler.WriteToken(token);

                return Ok(new
                {
                    message = "Login successful",
                    steamId = account.SteamId,
                    jwt = jwtString,
                    expiresInSeconds = 1800
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed for SteamId: {SteamId}", request.SteamId);
                return StatusCode(500, new { error = "Auth Server Internal Error." });
            }
        }

        // EZ A VÉGPONT HIÁNYZOTT, AMI A 404-ET OKOZTA!
        [Authorize]
        [HttpGet("servers")]
        public async Task<IActionResult> GetServerList()
        {
            try
            {
                // Csak azokat a Master Realm-eket mutatjuk, amik online-ok (friss a heartbeatjük)
                var activeRealms = await _context.Realms
                    .Where(r => r.Status == 1 && r.LastHeartbeat >= DateTime.UtcNow.AddMinutes(-5))
                    .Select(s => new {
                        id = s.Id,
                        name = s.Name,
                        ipAddress = "", // Ezt a kliens úgyis lecseréli
                        apiUrl = s.ApiUrl,
                        gamePort = 7777,
                        status = s.Status,
                        currentPlayers = s.CurrentPlayers,
                        maxPlayers = s.MaxPlayers
                    })
                    .ToListAsync();

                return Ok(new { realms = activeRealms });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve server list.");
                return StatusCode(500, new { error = "Could not fetch server list." });
            }
        }
    }
}