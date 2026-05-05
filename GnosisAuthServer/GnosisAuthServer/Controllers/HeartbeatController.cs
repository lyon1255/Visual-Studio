using GnosisAuthServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HeartbeatController : ControllerBase
    {
        private readonly MasterDbContext _context;

        public HeartbeatController(MasterDbContext context)
        {
            _context = context;
        }

        public class RealmHeartbeatPayload
        {
            public string Name { get; set; } = string.Empty;
            public string Region { get; set; } = string.Empty;
            public string RealmApiUrl { get; set; } = string.Empty;
            public int CurrentPlayers { get; set; }
            public int MaxPlayers { get; set; }
            public int Status { get; set; }
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] RealmHeartbeatPayload request)
        {
            // A MasterHub küldi a saját api_key-ét a fejlécben
            if (!Request.Headers.TryGetValue("X-Server-Admin-Key", out var extractedKey))
                return Unauthorized();

            string apiKey = extractedKey.ToString();

            try
            {
                // A Realm azonosítása a MasterHub egyedi API kulcsa alapján történik
                var realm = await _context.Realms.FirstOrDefaultAsync(r => r.ApiKey == apiKey);

                if (realm == null)
                {
                    // Új Master VPS (Realm) regisztrációja az első induláskor
                    realm = new Realm
                    {
                        ApiKey = apiKey,
                        Name = request.Name,
                        Region = request.Region,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Realms.Add(realm);
                }

                // Adatok frissítése az összesített adatokkal
                realm.Name = request.Name; // Ha esetleg átnevezték
                realm.Region = request.Region;
                realm.ApiUrl = request.RealmApiUrl;
                realm.CurrentPlayers = request.CurrentPlayers;
                realm.MaxPlayers = request.MaxPlayers;
                realm.Status = request.Status;
                realm.LastHeartbeat = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception) { return StatusCode(500); }
        }
    }
}