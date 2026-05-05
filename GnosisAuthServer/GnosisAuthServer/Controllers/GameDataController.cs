using GnosisAuthServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameDataController : ControllerBase
    {
        private readonly MasterDbContext _context;
        private readonly IConfiguration _configuration;

        public GameDataController(MasterDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // --- KÖZÖS DTO (Unity JsonUtility 100% kompatibilis) ---
        // Sima mezők (Fields), semmi Property ({ get; set; })!
        public class SyncEntryDto
        {
            public string assetId = string.Empty;
            public string classType = string.Empty;
            public string jsonData = string.Empty;
        }

        public class GameDataSyncPayload
        {
            public List<SyncEntryDto> items = new();
            public List<SyncEntryDto> entities = new();
            public List<SyncEntryDto> quests = new();
            public List<SyncEntryDto> spells = new();
            public List<SyncEntryDto> auras = new();
        }
        // --------------------------------------------------------

        private bool IsEditorAuthorized()
        {
            if (!Request.Headers.TryGetValue("X-Editor-Key", out var key)) return false;
            return key.ToString() == _configuration["EditorSettings:SecretKey"];
        }

        private async Task<bool> IsValidRealmServer()
        {
            if (!Request.Headers.TryGetValue("X-Server-Admin-Key", out var key)) return false;
            string incomingKey = key.ToString();

            bool isRegistered = await _context.Realms.AnyAsync(r => r.ApiKey == incomingKey);
            if (isRegistered) return true;

            return incomingKey == _configuration["ServerSetup:InitialKey"];
        }

        // --- VÉGPONTOK ---

        [HttpGet("download")]
        public async Task<IActionResult> DownloadGameDataServer()
        {
            if (!await IsValidRealmServer() && !IsEditorAuthorized())
                return Unauthorized(new { error = "Access denied (Server or Editor key missing)." });

            // Betöltés az adatbázisból (PascalCase -> camelCase váltás)
            var payload = new GameDataSyncPayload
            {
                items = await _context.GameItems.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                entities = await _context.GameEntities.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                quests = await _context.GameQuests.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                spells = await _context.GameSpells.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                auras = await _context.GameAuras.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync()
            };

            return Ok(payload);
        }

        [Authorize] // Játékosoknak marad a JWT!
        [HttpGet("download_client")]
        [EnableRateLimiting("ClientDataSync")]
        public async Task<IActionResult> DownloadGameDataClient()
        {
            // Teljesen ugyanaz a kód, mint a szerver letöltésnél
            var payload = new GameDataSyncPayload
            {
                items = await _context.GameItems.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                entities = await _context.GameEntities.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                quests = await _context.GameQuests.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                spells = await _context.GameSpells.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync(),
                auras = await _context.GameAuras.Select(x => new SyncEntryDto { assetId = x.AssetId, classType = x.ClassType, jsonData = x.JsonData }).ToListAsync()
            };
            return Ok(payload);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadGameData([FromBody] GameDataSyncPayload payload)
        {
            if (!IsEditorAuthorized()) return Unauthorized(new { error = "Editor access denied." });

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Régi adatok törlése
                _context.GameItems.RemoveRange(_context.GameItems);
                _context.GameEntities.RemoveRange(_context.GameEntities);
                _context.GameQuests.RemoveRange(_context.GameQuests);
                _context.GameSpells.RemoveRange(_context.GameSpells);
                _context.GameAuras.RemoveRange(_context.GameAuras);
                await _context.SaveChangesAsync();

                // Új adatok beszúrása (camelCase -> PascalCase visszaváltás)
                _context.GameItems.AddRange(payload.items.Select(x => new DbItem { AssetId = x.assetId, ClassType = x.classType, JsonData = x.jsonData }));
                _context.GameEntities.AddRange(payload.entities.Select(x => new DbEntity { AssetId = x.assetId, ClassType = x.classType, JsonData = x.jsonData }));
                _context.GameQuests.AddRange(payload.quests.Select(x => new DbQuest { AssetId = x.assetId, ClassType = x.classType, JsonData = x.jsonData }));
                _context.GameSpells.AddRange(payload.spells.Select(x => new DbSpell { AssetId = x.assetId, ClassType = x.classType, JsonData = x.jsonData }));
                _context.GameAuras.AddRange(payload.auras.Select(x => new DbAura { AssetId = x.assetId, ClassType = x.classType, JsonData = x.jsonData }));

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Editor sync successful!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GameData Sync failed: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}