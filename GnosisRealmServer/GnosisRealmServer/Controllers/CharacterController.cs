using GnosisRealmCore.Data;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GnosisRealmCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CharacterController : ControllerBase
    {
        private readonly RealmDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly LoggerService _logger;

        public CharacterController(RealmDbContext context, IConfiguration configuration, LoggerService loggerService)
        {
            _context = context;
            _configuration = configuration;
            _logger = loggerService;
        }

        private bool IsServerKeyValid()
        {
            if (!Request.Headers.TryGetValue("X-Server-Admin-Key", out var apiKey)) return false;
            var serverKey = _configuration.GetValue<string>("ServerSettings:ApiKey");
            return !string.IsNullOrEmpty(serverKey) && serverKey == apiKey.ToString();
        }

        public class CharacterCreateRequest { public string Name { get; set; } = string.Empty; public int ClassType { get; set; } }

        // GET: /api/character/list
        [EnableRateLimiting("GeneralClient")]
        [HttpGet("list")]
        public async Task<IActionResult> GetMyCharacters()
        {
            var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(steamId)) return Unauthorized();

            try
            {
                var characters = await _context.Characters.Where(c => c.SteamId == steamId).Include(c => c.Items).ToListAsync();
                var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.SteamId == steamId);
                int level = permission?.PermissionLevel ?? 0;

                return Ok(new { permissionLevel = level, characters });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"CRITICAL ERROR in GetMyCharacters (Steam: {steamId}): {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database error");
            }
        }

        // GET: /api/character/{charId}/details
        [EnableRateLimiting("GeneralClient")]
        [HttpGet("{charId}/details")]
        public async Task<IActionResult> GetCharacterDetails(int charId)
        {
            var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(steamId)) return Unauthorized();

            try
            {
                var character = await _context.Characters
                    .Include(c => c.Items).Include(c => c.Hotbar).Include(c => c.Quests)
                    .Include(c => c.QuestHistory).Include(c => c.Settings).Include(c => c.Guild)
                    .FirstOrDefaultAsync(c => c.Id == charId && c.SteamId == steamId);

                if (character == null) return NotFound();

                foreach (var q in character.Quests)
                {
                    if (!string.IsNullOrEmpty(q.ProgressString))
                    {
                        q.GoalProgress = q.ProgressString.Split(',').Select(s => int.TryParse(s, out int val) ? val : 0).ToArray();
                    }
                }
                character.CompletedQuestHistoryIds = character.QuestHistory.Select(h => h.QuestId).ToList();

                var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.SteamId == steamId);
                character.PermissionLevel = permission?.PermissionLevel ?? 0;

                return Ok(character);
            }
            catch (Exception ex)
            {
                _logger.LogSync($"CRITICAL ERROR in GetCharacterDetails for CharID {charId} (Steam: {steamId}): {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database unavailable.");
            }
        }

        // POST: /api/character/create
        [EnableRateLimiting("HeavyAction")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateCharacter([FromBody] CharacterCreateRequest request)
        {
            var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(steamId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name required.");

            try
            {
                if (await _context.Characters.AnyAsync(c => c.Name == request.Name)) return BadRequest("Name taken!");

                var newChar = new Character
                {
                    SteamId = steamId,
                    Name = request.Name,
                    ClassType = request.ClassType,
                    CreatedAt = DateTime.UtcNow,
                    Level = 1,
                    Experience = 0,
                    CurrentHp = 99999f,
                    CurrentMp = 99999f,
                    LastZone = "TutorialZone",
                    LastPosX = 0f,
                    LastPosY = 10f,
                    LastPosZ = 0f
                };

                _context.Characters.Add(newChar);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Created", character = newChar });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"CRITICAL ERROR in CreateCharacter for CharID (Steam: {steamId}): {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database error.");
            }
        }

        // DELETE: /api/character/{id}
        [EnableRateLimiting("HeavyAction")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCharacter(int id)
        {
            var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(steamId)) return Unauthorized();

            try
            {
                var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == id && c.SteamId == steamId);
                if (character == null) return NotFound();

                _context.Characters.Remove(character);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"CRITICAL ERROR in DeleteCharacter (Steam: {steamId}): {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database error.");
            }
        }

        // POST: /api/character/save
        [HttpPost("save")]
        [AllowAnonymous] // Mert a játékszerver küldi (API kulccsal), nem a játékos (JWT-vel)!
        public async Task<IActionResult> SaveCharacter([FromBody] Character data)
        {
            if (data == null) return BadRequest("Data is null");

            if (!IsServerKeyValid())
            {
                _logger.LogSync("Auth FAILED: Invalid or missing API Key for character save.", GnosisLogLevel.Warning);
                return Unauthorized("Invalid API Key.");
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                var character = await _context.Characters
                    .Include(c => c.Items).Include(c => c.Hotbar).Include(c => c.Quests).Include(c => c.QuestHistory)
                    .Include(c => c.Settings).Include(c => c.Social)
                    .FirstOrDefaultAsync(c => c.Id == data.Id);

                if (character == null)
                {
                    _logger.LogSync($"NOT FOUND: Could not find character with ID {data.Id}", GnosisLogLevel.Error);
                    return NotFound();
                }

                UpdateCharacterStats(character, data);
                SyncItems(character, data);
                SyncHotbar(character, data);
                SyncQuests(character, data);
                SyncSettings(character, data);
                SyncSocial(character, data);
                SyncQuestHistory(character, data);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Sync complete" });
            }
            catch (Exception ex)
            {
                _logger.LogSync($"CRITICAL SAVE FAILURE FOR CHARACTER {data?.Name} (Steam: {data?.SteamId}): {ex.Message}", GnosisLogLevel.Error);
                if (ex.InnerException != null) _logger.LogSync($"INNER EXCEPTION: {ex.InnerException.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database save error.");
            }
        }

        private void UpdateCharacterStats(Character dbChar, Character data)
        {
            dbChar.LastPosX = data.LastPosX; dbChar.LastPosY = data.LastPosY; dbChar.LastPosZ = data.LastPosZ;
            dbChar.LastRotY = data.LastRotY; dbChar.LastZone = data.LastZone;
            dbChar.CurrentHp = data.CurrentHp; dbChar.CurrentMp = data.CurrentMp; dbChar.Currency = data.Currency;
            dbChar.Level = data.Level; dbChar.Experience = data.Experience;
            dbChar.LastLogout = DateTime.UtcNow; dbChar.IsOnline = 0;
        }

        private void SyncItems(Character dbChar, Character data)
        {
            _context.Items.RemoveRange(dbChar.Items);
            if (data.Items == null) return;
            foreach (var item in data.Items)
                dbChar.Items.Add(new CharacterItem
                {
                    CharacterId = dbChar.Id,
                    ContainerType = item.ContainerType,
                    SlotIndex = item.SlotIndex,
                    ItemId = item.ItemId,
                    Amount = item.Amount,
                    CurrentDurability = item.CurrentDurability,
                    IsBound = item.IsBound,
                    IsLocked = item.IsLocked,
                    UpgradeLevel = item.UpgradeLevel,
                    EnchantId = item.EnchantId,
                    TransmogId = item.TransmogId,
                    CraftedBy = item.CraftedBy
                });
        }

        private void SyncHotbar(Character dbChar, Character data)
        {
            _context.Hotbars.RemoveRange(dbChar.Hotbar);
            if (data.Hotbar == null) return;
            foreach (var hb in data.Hotbar.Where(h => h.Type != 0 && !string.IsNullOrEmpty(h.ShortcutId)))
                dbChar.Hotbar.Add(new CharacterHotbar { CharacterId = dbChar.Id, SlotIndex = hb.SlotIndex, Type = hb.Type, ShortcutId = hb.ShortcutId });
        }

        private void SyncQuests(Character dbChar, Character data)
        {
            _context.Quests.RemoveRange(dbChar.Quests);
            if (data.Quests == null) return;
            var uniqueQuests = data.Quests.GroupBy(q => q.QuestId).Select(g => g.First());
            foreach (var q in uniqueQuests)
                dbChar.Quests.Add(new CharacterQuest { CharacterId = dbChar.Id, QuestId = q.QuestId, Status = q.Status, ProgressString = string.Join(",", q.GoalProgress ?? new int[0]) });
        }

        private void SyncSettings(Character dbChar, Character data)
        {
            _context.Settings.RemoveRange(dbChar.Settings);
            if (data.Settings == null) return;
            foreach (var s in data.Settings)
                dbChar.Settings.Add(new CharacterSetting { CharacterId = dbChar.Id, SettingKey = s.SettingKey, SettingValue = s.SettingValue });
        }

        private void SyncSocial(Character dbChar, Character data)
        {
            _context.Social.RemoveRange(dbChar.Social);
            if (data.Social == null) return;
            foreach (var soc in data.Social)
                dbChar.Social.Add(new CharacterSocial { CharacterId = dbChar.Id, TargetId = soc.TargetId, RelationType = soc.RelationType });
        }

        private void SyncQuestHistory(Character dbChar, Character data)
        {
            if (data.CompletedQuestHistoryIds == null) return;
            foreach (var qId in data.CompletedQuestHistoryIds)
            {
                if (!dbChar.QuestHistory.Any(h => h.QuestId == qId))
                    dbChar.QuestHistory.Add(new CharacterQuestHistory { CharacterId = dbChar.Id, QuestId = qId });
            }
        }
    }
}