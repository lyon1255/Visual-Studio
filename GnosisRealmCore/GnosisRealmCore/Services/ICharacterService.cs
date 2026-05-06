using GnosisRealmCore.Models;

namespace GnosisRealmCore.Services;

public interface ICharacterService
{
    Task<IReadOnlyList<CharacterListItemResponse>> GetCharacterListAsync(string steamId, CancellationToken cancellationToken);
    Task<CharacterDetailsResponse?> GetCharacterDetailsAsync(string steamId, int characterId, CancellationToken cancellationToken);
    Task<CharacterDetailsResponse> CreateCharacterAsync(string steamId, CreateCharacterRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteCharacterAsync(string steamId, int characterId, CancellationToken cancellationToken);
    Task SaveCharacterFromServerAsync(SaveCharacterRequest request, CancellationToken cancellationToken);
}
