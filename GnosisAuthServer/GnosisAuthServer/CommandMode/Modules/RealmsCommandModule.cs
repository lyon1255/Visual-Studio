using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using GnosisAuthServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class RealmsCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "realms";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            PrintDetailedHelp();
            return 1;
        }

        var realmService = context.Services.GetRequiredService<IRealmRegistryService>();
        var dbContext = context.Services.GetRequiredService<AuthDbContext>();
        var realmOptions = context.Services.GetRequiredService<IOptions<RealmRegistryOptions>>().Value;
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    var query = dbContext.Realms.AsNoTracking();
                    var filters = args.Skip(1).ToArray();

                    if (CommandModeHelpers.HasFlag(filters, "--official"))
                    {
                        query = query.Where(x => x.IsOfficial);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--community"))
                    {
                        query = query.Where(x => !x.IsOfficial);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--enabled"))
                    {
                        query = query.Where(x => x.Enabled);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--disabled"))
                    {
                        query = query.Where(x => !x.Enabled);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--listed"))
                    {
                        query = query.Where(x => x.IsListed);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--unlisted"))
                    {
                        query = query.Where(x => !x.IsListed);
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--online"))
                    {
                        query = query.Where(x => x.Status == "online");
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--offline"))
                    {
                        query = query.Where(x => x.Status == "offline");
                    }

                    if (CommandModeHelpers.HasFlag(filters, "--degraded"))
                    {
                        query = query.Where(x => x.Status == "degraded");
                    }

                    var all = await query
                        .OrderByDescending(x => x.IsOfficial)
                        .ThenBy(x => x.DisplayName)
                        .ToListAsync();

                    PrintRealmRows(all);
                    return 0;
                }

            case "public-list":
                {
                    var all = await realmService.GetPublicRealmsAsync();
                    PrintRealmRows(all);
                    return 0;
                }

            case "inactive":
                {
                    var minutes = CommandModeHelpers.ParseIntOption(
                        CommandModeHelpers.GetOption(args, "--minutes"),
                        15,
                        1);

                    var cutoff = DateTime.UtcNow.AddMinutes(-minutes);

                    var all = await dbContext.Realms
                        .AsNoTracking()
                        .Where(x => x.LastHeartbeatAt == null || x.LastHeartbeatAt < cutoff)
                        .OrderByDescending(x => x.IsOfficial)
                        .ThenBy(x => x.DisplayName)
                        .ToListAsync();

                    PrintRealmRows(all);
                    return 0;
                }

            case "unhealthy":
                {
                    var timeoutSeconds = CommandModeHelpers.ParseIntOption(
                        CommandModeHelpers.GetOption(args, "--timeout-seconds"),
                        realmOptions.HeartbeatTimeoutSeconds,
                        1);

                    var cutoff = DateTime.UtcNow.AddSeconds(-timeoutSeconds);

                    var all = await dbContext.Realms
                        .AsNoTracking()
                        .Where(x =>
                            x.Enabled &&
                            x.IsListed &&
                            (
                                x.Status != "online" ||
                                x.LastHeartbeatAt == null ||
                                x.LastHeartbeatAt < cutoff
                            ))
                        .OrderByDescending(x => x.IsOfficial)
                        .ThenBy(x => x.DisplayName)
                        .ToListAsync();

                    PrintRealmRows(all);
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

                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(realm, CommandExecutionContext.JsonOptions));
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
            case "hide":
            case "unhide":
            case "enable":
            case "disable":
            case "mark-offline":
                {
                    return await ExecuteRealmMutationAsync(context, realmService, dbContext, action, args);
                }

            case "create-service":
                {
                    return await ExecuteRealmCreateServiceAsync(context, args.Skip(1).ToArray());
                }

            case "revoke-service":
                {
                    return await ExecuteRealmRevokeServiceAsync(context, args.Skip(1).ToArray());
                }

            default:
                PrintDetailedHelp();
                return 1;
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("  realms list|public-list|inactive|unhealthy|show|stats|create|update|set-official|set-listed|set-enabled|hide|unhide|enable|disable|quarantine|restore|mark-offline|create-service|revoke-service");
    }

    private static async Task<int> ExecuteRealmMutationAsync(
        CommandExecutionContext context,
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

        if ((action is "quarantine" or "restore" or "hide" or "unhide" or "enable" or "disable" or "mark-offline") && args.Length < 2)
        {
            Console.Error.WriteLine($"Usage: command realms {action} <realmId>");
            return 1;
        }

        var realmId = args[1];
        var existing = await dbContext.Realms.FirstOrDefaultAsync(x => x.RealmId == realmId);
        if (existing is null)
        {
            Console.Error.WriteLine($"Realm '{realmId}' was not found.");
            return 1;
        }

        if (action == "mark-offline")
        {
            existing.Status = "offline";
            existing.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            Console.WriteLine($"Realm '{realmId}' status set to offline.");
            return 0;
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
                request.IsOfficial = CommandModeHelpers.ParseBool(args[2], "is_official");
                break;

            case "set-listed":
                request.IsListed = CommandModeHelpers.ParseBool(args[2], "is_listed");
                break;

            case "set-enabled":
                request.Enabled = CommandModeHelpers.ParseBool(args[2], "enabled");
                break;

            case "quarantine":
                request.IsListed = false;
                request.Enabled = false;
                break;

            case "restore":
                request.IsListed = true;
                request.Enabled = true;
                break;

            case "hide":
                request.IsListed = false;
                break;

            case "unhide":
                request.IsListed = true;
                break;

            case "enable":
                request.Enabled = true;
                break;

            case "disable":
                request.Enabled = false;
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

    private static async Task<int> ExecuteRealmCreateServiceAsync(CommandExecutionContext context, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command realms create-service <realmId> [--service-id <id>] [--secret <secret>] [--bytes <n>]");
            return 1;
        }

        var realmId = args[0].Trim();
        var dbContext = context.Services.GetRequiredService<AuthDbContext>();

        var realm = await dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == realmId);
        if (realm is null)
        {
            Console.Error.WriteLine($"Realm '{realmId}' was not found.");
            return 1;
        }

        var serviceId = CommandModeHelpers.GetOption(args, "--service-id")?.Trim();
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            serviceId = CommandModeHelpers.GenerateDefaultServiceIdForRealm(realmId);
        }

        var secret = CommandModeHelpers.GetOption(args, "--secret")?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            var byteCount = CommandModeHelpers.ParseIntOption(CommandModeHelpers.GetOption(args, "--bytes"), 48, 16);
            secret = CommandModeHelpers.GenerateSecret(byteCount);
        }

        var editor = new ServiceAuthConfigEditor(context.App.Environment);
        var (root, path) = editor.Load();

        editor.CreateClient(root, serviceId, secret, new[] { realmId });
        editor.Save(root, path);

        Console.WriteLine($"Created service '{serviceId}' for realm '{realmId}' in '{path}'.");
        Console.WriteLine($"Secret: {secret}");
        Console.WriteLine("Restart the Auth service to apply config changes.");
        return 0;
    }

    private static async Task<int> ExecuteRealmRevokeServiceAsync(CommandExecutionContext context, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command realms revoke-service <realmId> [--service-id <id>] [--keep-empty]");
            return 1;
        }

        var realmId = args[0].Trim();
        var specificServiceId = CommandModeHelpers.GetOption(args, "--service-id")?.Trim();
        var keepEmpty = CommandModeHelpers.HasFlag(args, "--keep-empty");

        var editor = new ServiceAuthConfigEditor(context.App.Environment);
        var (root, path) = editor.Load();
        var clients = editor.GetClients(root);

        var touchedServices = 0;
        var deletedServices = 0;

        for (var i = clients.Count - 1; i >= 0; i--)
        {
            if (clients[i] is not JsonObject client)
            {
                continue;
            }

            var currentServiceId = client["ServiceId"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(specificServiceId) &&
                !string.Equals(currentServiceId, specificServiceId, StringComparison.Ordinal))
            {
                continue;
            }

            var allowed = client["AllowedRealmIds"] as JsonArray;
            if (allowed is null)
            {
                continue;
            }

            var removed = false;

            for (var j = allowed.Count - 1; j >= 0; j--)
            {
                var currentRealmId = allowed[j]?.GetValue<string>();
                if (string.Equals(currentRealmId, realmId, StringComparison.Ordinal))
                {
                    allowed.RemoveAt(j);
                    removed = true;
                }
            }

            if (!removed)
            {
                continue;
            }

            touchedServices++;

            if (!keepEmpty && allowed.Count == 0)
            {
                clients.RemoveAt(i);
                deletedServices++;
            }
        }

        if (touchedServices == 0)
        {
            Console.Error.WriteLine($"No service mapping found for realm '{realmId}'.");
            return 1;
        }

        editor.Save(root, path);

        Console.WriteLine($"Revoked realm '{realmId}' from {touchedServices} service(s).");
        Console.WriteLine($"Deleted empty services: {deletedServices}");
        Console.WriteLine("Restart the Auth service to apply config changes.");
        return 0;
    }

    private static AdminRealmUpsertRequest BuildAdminRealmRequest(string[] args, bool requireRealmId)
    {
        var realmId = CommandModeHelpers.GetOption(args, "--realm-id");
        var displayName = CommandModeHelpers.GetOption(args, "--display-name");
        var region = CommandModeHelpers.GetOption(args, "--region");
        var publicBaseUrl = CommandModeHelpers.GetOption(args, "--url");
        var kind = CommandModeHelpers.GetOption(args, "--kind") ?? "realmcore";
        var maxPlayersRaw = CommandModeHelpers.GetOption(args, "--max-players") ?? "0";
        var isOfficialRaw = CommandModeHelpers.GetOption(args, "--official") ?? "false";
        var isListedRaw = CommandModeHelpers.GetOption(args, "--listed") ?? "true";
        var enabledRaw = CommandModeHelpers.GetOption(args, "--enabled") ?? "true";

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
            IsOfficial = CommandModeHelpers.ParseBool(isOfficialRaw, "official"),
            IsListed = CommandModeHelpers.ParseBool(isListedRaw, "listed"),
            Enabled = CommandModeHelpers.ParseBool(enabledRaw, "enabled")
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

        var displayName = CommandModeHelpers.GetOption(args, "--display-name");
        var region = CommandModeHelpers.GetOption(args, "--region");
        var publicBaseUrl = CommandModeHelpers.GetOption(args, "--url");
        var kind = CommandModeHelpers.GetOption(args, "--kind");
        var maxPlayersRaw = CommandModeHelpers.GetOption(args, "--max-players");
        var isOfficialRaw = CommandModeHelpers.GetOption(args, "--official");
        var isListedRaw = CommandModeHelpers.GetOption(args, "--listed");
        var enabledRaw = CommandModeHelpers.GetOption(args, "--enabled");

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
            request.IsOfficial = CommandModeHelpers.ParseBool(isOfficialRaw, "official");
        }

        if (!string.IsNullOrWhiteSpace(isListedRaw))
        {
            request.IsListed = CommandModeHelpers.ParseBool(isListedRaw, "listed");
        }

        if (!string.IsNullOrWhiteSpace(enabledRaw))
        {
            request.Enabled = CommandModeHelpers.ParseBool(enabledRaw, "enabled");
        }

        return request;
    }

    private static void PrintRealmRows(IEnumerable<Realm> realms)
    {
        foreach (var realm in realms)
        {
            var lastHeartbeat = realm.LastHeartbeatAt?.ToString("u") ?? "-";
            Console.WriteLine($"{realm.RealmId} | {realm.DisplayName} | official={realm.IsOfficial} | listed={realm.IsListed} | enabled={realm.Enabled} | status={realm.Status} | players={realm.CurrentPlayers}/{realm.MaxPlayers} | lastHeartbeat={lastHeartbeat}");
        }
    }

    private static void PrintDetailedHelp()
    {
        Console.WriteLine("  realms list [--official] [--community] [--enabled] [--disabled] [--listed] [--unlisted] [--online] [--offline] [--degraded]");
        Console.WriteLine("  realms public-list");
        Console.WriteLine("  realms inactive [--minutes <n>]");
        Console.WriteLine("  realms unhealthy [--timeout-seconds <n>]");
        Console.WriteLine("  realms show <realmId>");
        Console.WriteLine("  realms stats");
        Console.WriteLine("  realms create --realm-id <id> --display-name <name> --region <region> --url <url> --max-players <n> [--kind <kind>] [--official true|false] [--listed true|false] [--enabled true|false]");
        Console.WriteLine("  realms update <realmId> [--display-name <name>] [--region <region>] [--url <url>] [--max-players <n>] [--kind <kind>] [--official true|false] [--listed true|false] [--enabled true|false]");
        Console.WriteLine("  realms set-official <realmId> <true|false>");
        Console.WriteLine("  realms set-listed <realmId> <true|false>");
        Console.WriteLine("  realms set-enabled <realmId> <true|false>");
        Console.WriteLine("  realms hide <realmId>");
        Console.WriteLine("  realms unhide <realmId>");
        Console.WriteLine("  realms enable <realmId>");
        Console.WriteLine("  realms disable <realmId>");
        Console.WriteLine("  realms quarantine <realmId>");
        Console.WriteLine("  realms restore <realmId>");
        Console.WriteLine("  realms mark-offline <realmId>");
        Console.WriteLine("  realms create-service <realmId> [--service-id <id>] [--secret <secret>] [--bytes <n>]");
        Console.WriteLine("  realms revoke-service <realmId> [--service-id <id>] [--keep-empty]");
    }
}