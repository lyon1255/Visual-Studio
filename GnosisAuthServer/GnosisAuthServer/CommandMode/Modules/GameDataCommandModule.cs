using GnosisAuthServer.Models;
using GnosisAuthServer.Services;
using System.Text.Json;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class GameDataCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "gamedata";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            PrintDetailedHelp();
            return 1;
        }

        var gameDataService = context.Services.GetRequiredService<IGameDataService>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "version":
                {
                    var version = await gameDataService.GetCurrentVersionAsync(CancellationToken.None);
                    Console.WriteLine(JsonSerializer.Serialize(version, CommandExecutionContext.JsonOptions));
                    return 0;
                }

            case "export":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command gamedata export <file>");
                        return 1;
                    }

                    var snapshot = await gameDataService.GetCurrentSnapshotAsync(CancellationToken.None);
                    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(snapshot, CommandExecutionContext.JsonOptions));
                    Console.WriteLine($"Exported GameData snapshot to '{args[1]}'.");
                    return 0;
                }

            case "validate":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command gamedata validate <file>");
                        return 1;
                    }

                    var request = await ReadGameDataFileAsync(args[1]);
                    Console.WriteLine($"Valid GameData file. VersionTag='{request.VersionTag}', Items={request.Items.Count}, Entities={request.Entities.Count}, Quests={request.Quests.Count}, Spells={request.Spells.Count}, Auras={request.Auras.Count}");
                    return 0;
                }

            case "replace":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command gamedata replace <file>");
                        return 1;
                    }

                    var request = await ReadGameDataFileAsync(args[1]);
                    var replaced = await gameDataService.ReplaceSnapshotAsync(request, CancellationToken.None);
                    Console.WriteLine($"Replaced GameData snapshot. New version={replaced.VersionNumber}, tag='{replaced.VersionTag}'.");
                    return 0;
                }

            default:
                PrintDetailedHelp();
                return 1;
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("  gamedata version|export|validate|replace");
    }

    private static async Task<ReplaceGlobalGameDataRequest> ReadGameDataFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File was not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var request = JsonSerializer.Deserialize<ReplaceGlobalGameDataRequest>(json, CommandExecutionContext.JsonOptions);
        if (request is null)
        {
            throw new InvalidOperationException("GameData file could not be deserialized.");
        }

        return request;
    }

    private static void PrintDetailedHelp()
    {
        Console.WriteLine("  gamedata version");
        Console.WriteLine("  gamedata export <file>");
        Console.WriteLine("  gamedata validate <file>");
        Console.WriteLine("  gamedata replace <file>");
    }
}