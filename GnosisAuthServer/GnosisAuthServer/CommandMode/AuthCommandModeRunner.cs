using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using GnosisAuthServer.Security;
using GnosisAuthServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;

namespace GnosisAuthServer.CommandMode;

public static class AuthCommandModeRunner
{
    public static async Task<int> TryRunAsync(WebApplication app, string[] args)
    {
        if (args.Length == 0)
        {
            return -1;
        }

        var root = args[0].Trim().ToLowerInvariant();
        if (root is not ("command" or "cmd"))
        {
            return -1;
        }

        var commandArgs = args.Skip(1).ToArray();
        if (commandArgs.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        try
        {
            return await ExecuteAsync(app, services, commandArgs);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(WebApplication app, IServiceProvider services, string[] args)
    {
        var category = args[0].Trim().ToLowerInvariant();

        return category switch
        {
            "help" => ExecuteHelp(),
            "version" => ExecuteVersion(),
            "doctor" => await ExecuteDoctorAsync(app, services),
            "db" => await ExecuteDbAsync(services, args.Skip(1).ToArray()),
            "jwt" => ExecuteJwt(services, args.Skip(1).ToArray()),
            "environment" => ExecuteEnvironment(app),
            "realms" => await ExecuteRealmsAsync(services, args.Skip(1).ToArray()),
            "gamedata" => await ExecuteGameDataAsync(services, args.Skip(1).ToArray()),
            "schema" => await ExecuteSchemaAsync(services, args.Skip(1).ToArray()),
            _ => UnknownCommand(category)
        };
    }

    private static int ExecuteHelp()
    {
        PrintHelp();
        return 0;
    }

    private static int ExecuteVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"GnosisAuthServer {version}");
        return 0;
    }

    private static async Task<int> ExecuteDoctorAsync(WebApplication app, IServiceProvider services)
    {
        var dbContext = services.GetRequiredService<AuthDbContext>();
        var jwtOptions = services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var schemaOptions = services.GetRequiredService<IOptions<SchemaDeliveryOptions>>().Value;

        Console.WriteLine("Doctor report");
        Console.WriteLine("-------------");
        Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"ContentRoot: {app.Environment.ContentRootPath}");
        Console.WriteLine($"JWT private key: {jwtOptions.PrivateKeyPemPath}");
        Console.WriteLine($"JWT public key: {jwtOptions.PublicKeyPemPath}");
        Console.WriteLine($"Schema enabled: {schemaOptions.Enabled}");
        Console.WriteLine($"Schema directory: {schemaOptions.DirectoryPath}");

        var dbOk = await dbContext.Database.CanConnectAsync();
        Console.WriteLine($"Database: {(dbOk ? "OK" : "FAILED")}");

        Console.WriteLine($"JWT private key exists: {File.Exists(jwtOptions.PrivateKeyPemPath)}");
        Console.WriteLine($"JWT public key exists: {File.Exists(jwtOptions.PublicKeyPemPath)}");

        var schemaDirectory = Path.IsPathRooted(schemaOptions.DirectoryPath)
            ? schemaOptions.DirectoryPath
            : Path.Combine(app.Environment.ContentRootPath, schemaOptions.DirectoryPath);

        Console.WriteLine($"Schema directory exists: {Directory.Exists(schemaDirectory)}");

        return dbOk ? 0 : 1;
    }

    private static async Task<int> ExecuteDbAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command db <ping|realms-count|accounts-count>");
            return 1;
        }

        var dbContext = services.GetRequiredService<AuthDbContext>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "ping":
            {
                var ok = await dbContext.Database.CanConnectAsync();
                Console.WriteLine(ok ? "OK" : "FAILED");
                return ok ? 0 : 1;
            }
            case "realms-count":
                Console.WriteLine(await dbContext.Realms.CountAsync());
                return 0;
            case "accounts-count":
                Console.WriteLine(await dbContext.Accounts.CountAsync());
                return 0;
            default:
                Console.Error.WriteLine("Usage: command db <ping|realms-count|accounts-count>");
                return 1;
        }
    }

    private static int ExecuteJwt(IServiceProvider services, string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "check", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: command jwt check");
            return 1;
        }

        var keyProvider = services.GetRequiredService<IRsaKeyProvider>();
        _ = keyProvider.GetSigningCredentials();
        _ = keyProvider.GetValidationKey();
        Console.WriteLine("JWT key provider OK");
        return 0;
    }

    private static int ExecuteEnvironment(WebApplication app)
    {
        Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"ApplicationName: {app.Environment.ApplicationName}");
        Console.WriteLine($"ContentRootPath: {app.Environment.ContentRootPath}");
        return 0;
    }

    private static async Task<int> ExecuteRealmsAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintRealmHelp();
            return 1;
        }

        var realmService = services.GetRequiredService<IRealmRegistryService>();
        var dbContext = services.GetRequiredService<AuthDbContext>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
            {
                var all = await realmService.GetAllRealmsAsync();
                foreach (var realm in all)
                {
                    Console.WriteLine($"{realm.RealmId} | {realm.DisplayName} | official={realm.IsOfficial} | listed={realm.IsListed} | enabled={realm.Enabled} | status={realm.Status} | players={realm.CurrentPlayers}/{realm.MaxPlayers}");
                }
                return 0;
            }
            case "show":
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: command realms show <realmId>");
                    return 1;
                }

                var realm = await dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == args[1]);
                if (realm is null)
                {
                    Console.Error.WriteLine($"Realm '{args[1]}' was not found.");
                    return 1;
                }

                Console.WriteLine(JsonSerializer.Serialize(realm, JsonOptions));
                return 0;
            }
            case "stats":
            {
                var all = await dbContext.Realms.AsNoTracking().ToListAsync();
                Console.WriteLine($"Total: {all.Count}");
                Console.WriteLine($"Official: {all.Count(x => x.IsOfficial)}");
                Console.WriteLine($"Community: {all.Count(x => !x.IsOfficial)}");
                Console.WriteLine($"Enabled: {all.Count(x => x.Enabled)}");
                Console.WriteLine($"Listed: {all.Count(x => x.IsListed)}");
                Console.WriteLine($"Online: {all.Count(x => x.Status == "online")}");
                Console.WriteLine($"Offline: {all.Count(x => x.Status == "offline")}");
                Console.WriteLine($"Degraded: {all.Count(x => x.Status == "degraded")}");
                return 0;
            }
            case "create":
            {
                var request = BuildAdminRealmRequest(args.Skip(1).ToArray(), requireRealmId: true);
                var created = await realmService.CreateRealmAsync(request);
                Console.WriteLine($"Created realm '{created.RealmId}'.");
                return 0;
            }
            case "update":
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: command realms update <realmId> [options]");
                    return 1;
                }

                var realmId = args[1];
                var existing = await dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == realmId);
                if (existing is null)
                {
                    Console.Error.WriteLine($"Realm '{realmId}' was not found.");
                    return 1;
                }

                var request = BuildAdminRealmRequestFromExisting(existing, args.Skip(2).ToArray());
                var updated = await realmService.UpdateRealmAsync(realmId, request);
                if (updated is null)
                {
                    Console.Error.WriteLine($"Realm '{realmId}' was not found.");
                    return 1;
                }

                Console.WriteLine($"Updated realm '{realmId}'.");
                return 0;
            }
            case "set-official":
            case "set-listed":
            case "set-enabled":
            case "quarantine":
            case "restore":
            {
                return await ExecuteRealmMutationAsync(realmService, dbContext, action, args);
            }
            default:
                PrintRealmHelp();
                return 1;
        }
    }

    private static async Task<int> ExecuteRealmMutationAsync(
        IRealmRegistryService realmService,
        AuthDbContext dbContext,
        string action,
        string[] args)
    {
        if ((action is "set-official" or "set-listed" or "set-enabled") && args.Length < 3)
        {
            Console.Error.WriteLine($"Usage: command realms {action} <realmId> <true|false>");
            return 1;
        }

        if ((action is "quarantine" or "restore") && args.Length < 2)
        {
            Console.Error.WriteLine($"Usage: command realms {action} <realmId>");
            return 1;
        }

        var realmId = args[1];
        var existing = await dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == realmId);
        if (existing is null)
        {
            Console.Error.WriteLine($"Realm '{realmId}' was not found.");
            return 1;
        }

        var request = new AdminRealmUpsertRequest
        {
            RealmId = existing.RealmId,
            DisplayName = existing.DisplayName,
            Region = existing.Region,
            Kind = existing.Kind,
            PublicBaseUrl = existing.PublicBaseUrl,
            MaxPlayers = existing.MaxPlayers,
            IsListed = existing.IsListed,
            IsOfficial = existing.IsOfficial,
            Enabled = existing.Enabled
        };

        switch (action)
        {
            case "set-official":
                request.IsOfficial = ParseBool(args[2], "is_official");
                break;
            case "set-listed":
                request.IsListed = ParseBool(args[2], "is_listed");
                break;
            case "set-enabled":
                request.Enabled = ParseBool(args[2], "enabled");
                break;
            case "quarantine":
                request.IsListed = false;
                request.Enabled = false;
                break;
            case "restore":
                request.IsListed = true;
                request.Enabled = true;
                break;
        }

        var updated = await realmService.UpdateRealmAsync(realmId, request);
        if (updated is null)
        {
            Console.Error.WriteLine($"Realm '{realmId}' was not found.");
            return 1;
        }

        Console.WriteLine($"Updated realm '{realmId}' via '{action}'.");
        return 0;
    }

    private static async Task<int> ExecuteGameDataAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintGameDataHelp();
            return 1;
        }

        var gameDataService = services.GetRequiredService<IGameDataService>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "version":
            {
                var version = await gameDataService.GetCurrentVersionAsync(CancellationToken.None);
                Console.WriteLine(JsonSerializer.Serialize(version, JsonOptions));
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
                await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(snapshot, JsonOptions));
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
                PrintGameDataHelp();
                return 1;
        }
    }

    private static async Task<int> ExecuteSchemaAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintSchemaHelp();
            return 1;
        }

        var schemaService = services.GetRequiredService<ISchemaCatalogService>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
            {
                var manifest = await schemaService.GetManifestAsync();
                foreach (var migration in manifest.Migrations)
                {
                    Console.WriteLine($"{migration.Id} | destructive={migration.IsDestructive} | checksum={migration.ChecksumSha256}");
                }
                return 0;
            }
            case "show":
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: command schema show <migrationId>");
                    return 1;
                }

                var migration = await schemaService.GetMigrationAsync(args[1]);
                if (migration is null)
                {
                    Console.Error.WriteLine($"Migration '{args[1]}' was not found.");
                    return 1;
                }

                Console.WriteLine(JsonSerializer.Serialize(migration, JsonOptions));
                return 0;
            }
            case "manifest":
            {
                var manifest = await schemaService.GetManifestAsync();
                Console.WriteLine(JsonSerializer.Serialize(manifest, JsonOptions));
                return 0;
            }
            case "validate":
            {
                var manifest = await schemaService.GetManifestAsync();
                Console.WriteLine($"Schema valid. Migrations={manifest.MigrationCount}, Latest='{manifest.LatestMigrationId}'.");
                return 0;
            }
            default:
                PrintSchemaHelp();
                return 1;
        }
    }

    private static AdminRealmUpsertRequest BuildAdminRealmRequest(string[] args, bool requireRealmId)
    {
        var realmId = GetOption(args, "--realm-id");
        var displayName = GetOption(args, "--display-name");
        var region = GetOption(args, "--region");
        var publicBaseUrl = GetOption(args, "--url");
        var kind = GetOption(args, "--kind") ?? "realmcore";
        var maxPlayersRaw = GetOption(args, "--max-players") ?? "0";
        var isOfficialRaw = GetOption(args, "--official") ?? "false";
        var isListedRaw = GetOption(args, "--listed") ?? "true";
        var enabledRaw = GetOption(args, "--enabled") ?? "true";

        if (requireRealmId && string.IsNullOrWhiteSpace(realmId))
        {
            throw new InvalidOperationException("Missing required option '--realm-id'.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Missing required option '--display-name'.");
        }

        if (string.IsNullOrWhiteSpace(region))
        {
            throw new InvalidOperationException("Missing required option '--region'.");
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            throw new InvalidOperationException("Missing required option '--url'.");
        }

        if (!int.TryParse(maxPlayersRaw, out var maxPlayers) || maxPlayers < 0)
        {
            throw new InvalidOperationException("Option '--max-players' must be a non-negative integer.");
        }

        return new AdminRealmUpsertRequest
        {
            RealmId = realmId ?? string.Empty,
            DisplayName = displayName,
            Region = region,
            PublicBaseUrl = publicBaseUrl,
            Kind = kind,
            MaxPlayers = maxPlayers,
            IsOfficial = ParseBool(isOfficialRaw, "official"),
            IsListed = ParseBool(isListedRaw, "listed"),
            Enabled = ParseBool(enabledRaw, "enabled")
        };
    }

    private static AdminRealmUpsertRequest BuildAdminRealmRequestFromExisting(Realm existing, string[] args)
    {
        var request = new AdminRealmUpsertRequest
        {
            RealmId = existing.RealmId,
            DisplayName = existing.DisplayName,
            Region = existing.Region,
            PublicBaseUrl = existing.PublicBaseUrl,
            Kind = existing.Kind,
            MaxPlayers = existing.MaxPlayers,
            IsOfficial = existing.IsOfficial,
            IsListed = existing.IsListed,
            Enabled = existing.Enabled
        };

        var displayName = GetOption(args, "--display-name");
        var region = GetOption(args, "--region");
        var publicBaseUrl = GetOption(args, "--url");
        var kind = GetOption(args, "--kind");
        var maxPlayersRaw = GetOption(args, "--max-players");
        var isOfficialRaw = GetOption(args, "--official");
        var isListedRaw = GetOption(args, "--listed");
        var enabledRaw = GetOption(args, "--enabled");

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            request.DisplayName = displayName;
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            request.Region = region;
        }

        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            request.PublicBaseUrl = publicBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            request.Kind = kind;
        }

        if (!string.IsNullOrWhiteSpace(maxPlayersRaw))
        {
            if (!int.TryParse(maxPlayersRaw, out var maxPlayers) || maxPlayers < 0)
            {
                throw new InvalidOperationException("Option '--max-players' must be a non-negative integer.");
            }

            request.MaxPlayers = maxPlayers;
        }

        if (!string.IsNullOrWhiteSpace(isOfficialRaw))
        {
            request.IsOfficial = ParseBool(isOfficialRaw, "official");
        }

        if (!string.IsNullOrWhiteSpace(isListedRaw))
        {
            request.IsListed = ParseBool(isListedRaw, "listed");
        }

        if (!string.IsNullOrWhiteSpace(enabledRaw))
        {
            request.Enabled = ParseBool(enabledRaw, "enabled");
        }

        return request;
    }

    private static async Task<ReplaceGlobalGameDataRequest> ReadGameDataFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File was not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var request = JsonSerializer.Deserialize<ReplaceGlobalGameDataRequest>(json, JsonOptions);
        if (request is null)
        {
            throw new InvalidOperationException("GameData file could not be deserialized.");
        }

        return request;
    }

    private static string? GetOption(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool ParseBool(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Option '{optionName}' must be 'true' or 'false'.");
    }

    private static int UnknownCommand(string category)
    {
        Console.Error.WriteLine($"Unknown command category '{category}'.");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GnosisAuthServer command mode");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  GnosisAuthServer command <category> <action> [options]");
        Console.WriteLine("  GnosisAuthServer cmd <category> <action> [options]");
        Console.WriteLine();
        Console.WriteLine("Categories:");
        Console.WriteLine("  help");
        Console.WriteLine("  version");
        Console.WriteLine("  doctor");
        Console.WriteLine("  db ping|realms-count|accounts-count");
        Console.WriteLine("  jwt check");
        Console.WriteLine("  environment info");
        Console.WriteLine("  realms list|show|stats|create|update|set-official|set-listed|set-enabled|quarantine|restore");
        Console.WriteLine("  gamedata version|export|validate|replace");
        Console.WriteLine("  schema list|show|manifest|validate");
        Console.WriteLine();
        PrintRealmHelp();
        PrintGameDataHelp();
        PrintSchemaHelp();
    }

    private static void PrintRealmHelp()
    {
        Console.WriteLine("Realms:");
        Console.WriteLine("  command realms list");
        Console.WriteLine("  command realms show <realmId>");
        Console.WriteLine("  command realms stats");
        Console.WriteLine("  command realms create --realm-id <id> --display-name <name> --region <region> --url <url> --max-players <n> [--kind <kind>] [--official true|false] [--listed true|false] [--enabled true|false]");
        Console.WriteLine("  command realms update <realmId> [--display-name <name>] [--region <region>] [--url <url>] [--max-players <n>] [--kind <kind>] [--official true|false] [--listed true|false] [--enabled true|false]");
        Console.WriteLine("  command realms set-official <realmId> <true|false>");
        Console.WriteLine("  command realms set-listed <realmId> <true|false>");
        Console.WriteLine("  command realms set-enabled <realmId> <true|false>");
        Console.WriteLine("  command realms quarantine <realmId>");
        Console.WriteLine("  command realms restore <realmId>");
        Console.WriteLine();
    }

    private static void PrintGameDataHelp()
    {
        Console.WriteLine("GameData:");
        Console.WriteLine("  command gamedata version");
        Console.WriteLine("  command gamedata export <file>");
        Console.WriteLine("  command gamedata validate <file>");
        Console.WriteLine("  command gamedata replace <file>");
        Console.WriteLine();
    }

    private static void PrintSchemaHelp()
    {
        Console.WriteLine("Schema:");
        Console.WriteLine("  command schema list");
        Console.WriteLine("  command schema show <migrationId>");
        Console.WriteLine("  command schema manifest");
        Console.WriteLine("  command schema validate");
        Console.WriteLine();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
