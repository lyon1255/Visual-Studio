using GnosisRealmCore.Data;
using GnosisRealmCore.Models;
using Microsoft.EntityFrameworkCore;

namespace GnosisRealmCore.Services;

public sealed class CharacterService : ICharacterService
{
    private readonly RealmDbContext _dbContext;

    public CharacterService(RealmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CharacterListItemResponse>> GetCharacterListAsync(string steamId, CancellationToken cancellationToken)
    {
        var characters = await _dbContext.Characters
            .AsNoTracking()
            .Where(x => x.SteamId == steamId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return characters.Select(MapListItem).ToList();
    }

    public async Task<CharacterDetailsResponse?> GetCharacterDetailsAsync(string steamId, int characterId, CancellationToken cancellationToken)
    {
        var character = await _dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Equipment)
            .Include(x => x.Hotbar)
            .Include(x => x.Quests)
            .Include(x => x.QuestHistory)
            .Include(x => x.Settings)
            .Include(x => x.Social)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == characterId && x.SteamId == steamId, cancellationToken);

        if (character is null)
        {
            return null;
        }

        var permission = await _dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SteamId == steamId, cancellationToken);

        return MapDetails(character, permission?.PermissionLevel ?? 0);
    }

    public async Task<CharacterDetailsResponse> CreateCharacterAsync(string steamId, CreateCharacterRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _dbContext.Characters.AnyAsync(x => x.Name == name, cancellationToken))
        {
            throw new InvalidOperationException("Character name is already in use.");
        }

        var entity = new Character
        {
            SteamId = steamId,
            Name = name,
            ClassType = request.ClassType,
            Level = 1,
            Experience = 0,
            Currency = 0,
            CurrentHp = 100,
            CurrentMp = 100,
            LastZone = string.IsNullOrWhiteSpace(request.StartingZone) ? "City" : request.StartingZone.Trim(),
            LastPosX = 0,
            LastPosY = 0,
            LastPosZ = 0,
            LastRotY = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Characters.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapDetails(entity, 0);
    }

    public async Task<bool> DeleteCharacterAsync(string steamId, int characterId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Characters.FirstOrDefaultAsync(x => x.Id == characterId && x.SteamId == steamId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.Characters.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveCharacterFromServerAsync(SaveCharacterRequest request, CancellationToken cancellationToken)
    {
        var snapshot = request.Character;
        var entity = await _dbContext.Characters
            .Include(x => x.Items)
            .Include(x => x.Equipment)
            .Include(x => x.Hotbar)
            .Include(x => x.Quests)
            .Include(x => x.QuestHistory)
            .Include(x => x.Settings)
            .Include(x => x.Social)
            .FirstOrDefaultAsync(x => x.Id == snapshot.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException($"Character {snapshot.Id} was not found.");
        }

        entity.Name = snapshot.Name;
        entity.ClassType = snapshot.ClassType;
        entity.Level = snapshot.Level;
        entity.Experience = snapshot.Experience;
        entity.Currency = snapshot.Currency;
        entity.LastZone = snapshot.LastZone;
        entity.LastPosX = snapshot.LastPosX;
        entity.LastPosY = snapshot.LastPosY;
        entity.LastPosZ = snapshot.LastPosZ;
        entity.LastRotY = snapshot.LastRotY;
        entity.CurrentHp = snapshot.CurrentHp;
        entity.CurrentMp = snapshot.CurrentMp;
        entity.IsOnline = snapshot.IsOnline;
        entity.LastLogout = snapshot.LastLogout;

        ReplaceCollection(_dbContext, entity.Items, snapshot.Items.Select(x => new CharacterItem
        {
            Id = x.Id,
            CharacterId = entity.Id,
            ContainerType = x.ContainerType,
            SlotIndex = x.SlotIndex,
            ItemId = x.ItemId,
            Amount = x.Amount,
            ItemLevel = x.ItemLevel,
            CurrentDurability = x.CurrentDurability,
            IsBound = x.IsBound,
            IsLocked = x.IsLocked,
            UpgradeLevel = x.UpgradeLevel,
            EnchantId = x.EnchantId,
            TransmogId = x.TransmogId,
            CraftedBy = x.CraftedBy
        }).ToList());

        ReplaceCollection(_dbContext, entity.Equipment, snapshot.Equipment.Select(x => new CharacterEquipment
        {
            Id = x.Id,
            CharacterId = entity.Id,
            SlotType = x.SlotType,
            ItemId = x.ItemId,
            Amount = x.Amount
        }).ToList());

        ReplaceCollection(_dbContext, entity.Hotbar, snapshot.Hotbar.Select(x => new CharacterHotbar
        {
            Id = x.Id,
            CharacterId = entity.Id,
            SlotIndex = x.SlotIndex,
            ShortcutType = x.ShortcutType,
            ShortcutId = x.ShortcutId
        }).ToList());

        ReplaceCollection(_dbContext, entity.Quests, snapshot.Quests.Select(x => new CharacterQuest
        {
            Id = x.Id,
            CharacterId = entity.Id,
            QuestId = x.QuestId,
            Progress = x.Progress,
            Status = x.Status,
            IsDaily = x.IsDaily
        }).ToList());

        ReplaceCollection(_dbContext, entity.QuestHistory, snapshot.QuestHistory.Select(x => new CharacterQuestHistory
        {
            Id = x.Id,
            CharacterId = entity.Id,
            QuestId = x.QuestId,
            CompletedAt = x.CompletedAt
        }).ToList());

        ReplaceCollection(_dbContext, entity.Settings, snapshot.Settings.Select(x => new CharacterSetting
        {
            Id = x.Id,
            CharacterId = entity.Id,
            SettingKey = x.SettingKey,
            SettingValue = x.SettingValue
        }).ToList());

        ReplaceCollection(_dbContext, entity.Social, snapshot.Social.Select(x => new CharacterSocial
        {
            Id = x.Id,
            CharacterId = entity.Id,
            TargetId = x.TargetId,
            RelationType = x.RelationType
        }).ToList());

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CharacterListItemResponse MapListItem(Character entity) => new()
    {
        Id = entity.Id,
        SteamId = entity.SteamId,
        Name = entity.Name,
        ClassType = entity.ClassType,
        Level = entity.Level,
        Experience = entity.Experience,
        Currency = entity.Currency,
        LastZone = entity.LastZone,
        LastPosX = entity.LastPosX,
        LastPosY = entity.LastPosY,
        LastPosZ = entity.LastPosZ,
        LastRotY = entity.LastRotY,
        CurrentHp = entity.CurrentHp,
        CurrentMp = entity.CurrentMp,
        IsOnline = entity.IsOnline,
        LastLogout = entity.LastLogout,
        CreatedAt = entity.CreatedAt
    };

    private static CharacterDetailsResponse MapDetails(Character entity, int permissionLevel) => new()
    {
        Id = entity.Id,
        SteamId = entity.SteamId,
        Name = entity.Name,
        ClassType = entity.ClassType,
        Level = entity.Level,
        Experience = entity.Experience,
        Currency = entity.Currency,
        LastZone = entity.LastZone,
        LastPosX = entity.LastPosX,
        LastPosY = entity.LastPosY,
        LastPosZ = entity.LastPosZ,
        LastRotY = entity.LastRotY,
        CurrentHp = entity.CurrentHp,
        CurrentMp = entity.CurrentMp,
        IsOnline = entity.IsOnline,
        LastLogout = entity.LastLogout,
        CreatedAt = entity.CreatedAt,
        PermissionLevel = permissionLevel,
        Items = entity.Items.Select(x => new CharacterItemDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            ContainerType = x.ContainerType,
            SlotIndex = x.SlotIndex,
            ItemId = x.ItemId,
            Amount = x.Amount,
            ItemLevel = x.ItemLevel,
            CurrentDurability = x.CurrentDurability,
            IsBound = x.IsBound,
            IsLocked = x.IsLocked,
            UpgradeLevel = x.UpgradeLevel,
            EnchantId = x.EnchantId,
            TransmogId = x.TransmogId,
            CraftedBy = x.CraftedBy
        }).ToList(),
        Equipment = entity.Equipment.Select(x => new CharacterEquipmentDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            SlotType = x.SlotType,
            ItemId = x.ItemId,
            Amount = x.Amount
        }).ToList(),
        Hotbar = entity.Hotbar.Select(x => new CharacterHotbarDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            SlotIndex = x.SlotIndex,
            ShortcutType = x.ShortcutType,
            ShortcutId = x.ShortcutId
        }).ToList(),
        Quests = entity.Quests.Select(x => new CharacterQuestDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            QuestId = x.QuestId,
            Progress = x.Progress,
            Status = x.Status,
            IsDaily = x.IsDaily
        }).ToList(),
        QuestHistory = entity.QuestHistory.Select(x => new CharacterQuestHistoryDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            QuestId = x.QuestId,
            CompletedAt = x.CompletedAt
        }).ToList(),
        Settings = entity.Settings.Select(x => new CharacterSettingDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            SettingKey = x.SettingKey,
            SettingValue = x.SettingValue
        }).ToList(),
        Social = entity.Social.Select(x => new CharacterSocialDto
        {
            Id = x.Id,
            CharacterId = x.CharacterId,
            TargetId = x.TargetId,
            RelationType = x.RelationType
        }).ToList()
    };

    private static void ReplaceCollection<TEntity>(RealmDbContext dbContext, ICollection<TEntity> current, List<TEntity> incoming)
        where TEntity : class
    {
        var existing = current.ToList();
        if (existing.Count > 0)
        {
            dbContext.RemoveRange(existing);
        }

        current.Clear();
        foreach (var item in incoming)
        {
            current.Add(item);
        }
    }
}
