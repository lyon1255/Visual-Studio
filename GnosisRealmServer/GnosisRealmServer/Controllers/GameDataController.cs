using GnosisRealmCore.Data;
using GnosisRealmCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GnosisRealmCore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GameDataController : ControllerBase
    {
        private readonly RealmDbContext _context;
        private readonly LoggerService _logger;

        public GameDataController(RealmDbContext context, LoggerService logger)
        {
            _context = context;
            _logger = logger;
        }

        public class ClassDataDto
        {
            public int ClassType { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public float BaseMaxHealth { get; set; }
            public float BaseMaxMana { get; set; }
            public float BaseAD { get; set; }
            public float BaseAP { get; set; }
            public float BaseDefense { get; set; }
            public float BaseAttackSpeed { get; set; }
            public float BaseCritChance { get; set; }
            public float HpPerLevel { get; set; }
            public float ManaPerLevel { get; set; }
            public float AdPerLevel { get; set; }
            public float ApPerLevel { get; set; }
        }

        public class ServerRatesDto
        {
            public float XpMultiplier { get; set; }
            public float DropRateMultiplier { get; set; }
            public float GoldMultiplier { get; set; }
        }

        public class GameDataResponse
        {
            public ServerRatesDto Rates { get; set; } = new();
            public List<ClassDataDto> Classes { get; set; } = new();
        }

        // GET: /api/gamedata
        [AllowAnonymous]
        [EnableRateLimiting("GeneralClient")]
        [HttpGet]
        public async Task<IActionResult> GetGameData()
        {
            try
            {
                var rateDb = await _context.ServerRates.FirstOrDefaultAsync();
                var classesDb = await _context.ClassStats.ToListAsync();

                var response = new GameDataResponse
                {
                    Rates = rateDb != null ? new ServerRatesDto
                    {
                        XpMultiplier = rateDb.XpMultiplier,
                        DropRateMultiplier = rateDb.DropRateMultiplier,
                        GoldMultiplier = rateDb.GoldMultiplier
                    } : new ServerRatesDto { XpMultiplier = 1f, DropRateMultiplier = 1f, GoldMultiplier = 1f },

                    Classes = classesDb.Select(c => new ClassDataDto
                    {
                        ClassType = c.ClassType,
                        ClassName = c.ClassName,
                        BaseMaxHealth = c.BaseMaxHealth,
                        BaseMaxMana = c.BaseMaxMana,
                        BaseAD = c.BaseAD,
                        BaseAP = c.BaseAP,
                        BaseDefense = c.BaseDefense,
                        BaseAttackSpeed = c.BaseAttackSpeed,
                        BaseCritChance = c.BaseCritChance,
                        HpPerLevel = c.HpPerLevel,
                        ManaPerLevel = c.ManaPerLevel,
                        AdPerLevel = c.AdPerLevel,
                        ApPerLevel = c.ApPerLevel
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogSync($"Error fetching Game Data: {ex.Message}", GnosisLogLevel.Error);
                return StatusCode(503, "Database error.");
            }
        }
    }
}