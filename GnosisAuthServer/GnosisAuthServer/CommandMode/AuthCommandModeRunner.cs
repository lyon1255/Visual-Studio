using GnosisAuthServer.Data;
using GnosisAuthServer.Models;
using GnosisAuthServer.Options;
using GnosisAuthServer.Security;
using GnosisAuthServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GnosisAuthServer.CommandMode;

public static class AuthCommandModeRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int?> TryRunAsync(WebApplication app, string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        var root = args[0].Trim().ToLowerInvariant();
        if (root is not ("command" or "cmd"))
        {
            return null;
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
            "environment" => ExecuteEnvironment(app, args.Skip(1).ToArray()),
            "realms" => await ExecuteRealmsAsync(app, services, args.Skip(1).ToArray()),
            "services" => await ExecuteServicesAsync(app, services, args.Skip(1).ToArray()),
            "gamedata" => await ExecuteGameDataAsync(services, args.Skip(1).ToArray()),
            "schema" => await ExecuteSchemaAsync(services, args.Skip(1).ToArray()),
            "security" => await ExecuteSecurityAsync(services, args.Skip(1).ToArray()),
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
        var serviceAuthOptions = services.GetRequiredService<IOptions<ServiceAuthOptions>>().Value;

        Console.WriteLine("Doctor report");
        Console.WriteLine("-------------");
        Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"ContentRoot: {app.Environment.ContentRootPath}");
        Console.WriteLine($"JWT private key: {jwtOptions.PrivateKeyPemPath}");
        Console.WriteLine($"JWT public key: {jwtOptions.PublicKeyPemPath}");
        Console.WriteLine($"Schema enabled: {schemaOptions.Enabled}");
        Console.WriteLine($"Schema directory: {schemaOptions.DirectoryPath}");
        Console.WriteLine($"Service auth enabled: {serviceAuthOptions.Enabled}");
        Console.WriteLine($"Configured service clients: {serviceAuthOptions.Clients.Count}");

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
        _ = keyProvider.GetSigningKey();
        _ = keyProvider.GetValidationKey();

        Console.WriteLine("JWT key provider OK");
        return 0;
    }

    private static int ExecuteEnvironment(WebApplication app, string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "info", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: command environment info");
            return 1;
        }

        Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
        Console.WriteLine($"ApplicationName: {app.Environment.ApplicationName}");
        Console.WriteLine($"ContentRootPath: {app.Environment.ContentRootPath}");
        return 0;
    }

    private static async Task<int> ExecuteRealmsAsync(WebApplication app, IServiceProvider services, string[] args)
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

            case "create-service":
                {
                    return await ExecuteRealmCreateServiceAsync(app, services, args.Skip(1).ToArray());
                }

            case "revoke-service":
                {
                    return await ExecuteRealmRevokeServiceAsync(app, services, args.Skip(1).ToArray());
                }

            default:
                PrintRealmHelp();
                return 1;
        }
    }

    private static async Task<int> ExecuteServicesAsync(WebApplication app, IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintServicesHelp();
            return 1;
        }

        var options = services.GetRequiredService<IOptions<ServiceAuthOptions>>().Value;
        var editor = new ServiceAuthConfigEditor(app.Environment);
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    foreach (var client in options.Clients.OrderBy(x => x.ServiceId, StringComparer.Ordinal))
                    {
                        var allowed = client.AllowedRealmIds.Length == 0
                            ? "-"
                            : string.Join(",", client.AllowedRealmIds);

                        Console.WriteLine($"{client.ServiceId} | realms={allowed} | secret={MaskSecret(client.Secret)}");
                    }

                    return 0;
                }

            case "show":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services show <serviceId>");
                        return 1;
                    }

                    var client = options.Clients.FirstOrDefault(x =>
                        string.Equals(x.ServiceId, args[1], StringComparison.Ordinal));

                    if (client is null)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    var output = new
                    {
                        client.ServiceId,
                        Secret = MaskSecret(client.Secret),
                        client.AllowedRealmIds
                    };

                    Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
                    return 0;
                }

            case "export":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services export <file>");
                        return 1;
                    }

                    var export = new ServiceImportDocument
                    {
                        Clients = options.Clients
                            .OrderBy(x => x.ServiceId, StringComparer.Ordinal)
                            .Select(x => new ServiceImportClient
                            {
                                ServiceId = x.ServiceId,
                                Secret = x.Secret,
                                AllowedRealmIds = x.AllowedRealmIds
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(y => y, StringComparer.Ordinal)
                                    .ToArray()
                            })
                            .ToList()
                    };

                    await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(export, JsonOptions));
                    Console.WriteLine($"Exported {export.Clients.Count} service client(s) to '{args[1]}'.");
                    return 0;
                }

            case "import":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services import <file> [--replace]");
                        return 1;
                    }

                    var replace = HasFlag(args, "--replace");
                    var document = await ReadServiceImportFileAsync(args[1]);

                    var (root, path) = editor.Load();
                    var clients = editor.GetClients(root);

                    if (replace)
                    {
                        clients.Clear();
                    }

                    foreach (var client in document.Clients
                        .OrderBy(x => x.ServiceId, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(client.ServiceId))
                        {
                            throw new InvalidOperationException("Imported service contains empty ServiceId.");
                        }

                        if (string.IsNullOrWhiteSpace(client.Secret))
                        {
                            throw new InvalidOperationException($"Imported service '{client.ServiceId}' has empty secret.");
                        }

                        editor.DeleteClient(root, client.ServiceId);
                        editor.CreateClient(
                            root,
                            client.ServiceId.Trim(),
                            client.Secret.Trim(),
                            client.AllowedRealmIds ?? Array.Empty<string>());
                    }

                    editor.Save(root, path);

                    Console.WriteLine($"Imported {document.Clients.Count} service client(s) into '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            default:
                PrintServicesHelp();
                return 1;
        }
    }

    private static async Task<int> ExecuteRealmCreateServiceAsync(WebApplication app, IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command realms create-service <realmId> [--service-id <id>] [--secret <secret>] [--bytes <n>]");
            return 1;
        }

        var realmId = args[0].Trim();
        var dbContext = services.GetRequiredService<AuthDbContext>();

        var realm = await dbContext.Realms.AsNoTracking().FirstOrDefaultAsync(x => x.RealmId == realmId);
        if (realm is null)
        {
            Console.Error.WriteLine($"Realm '{realmId}' was not found.");
            return 1;
        }

        var serviceId = GetOption(args, "--service-id")?.Trim();
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            serviceId = GenerateDefaultServiceIdForRealm(realmId);
        }

        var secret = GetOption(args, "--secret")?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            var byteCount = ParseIntOption(GetOption(args, "--bytes"), 48, min: 16);
            secret = GenerateSecret(byteCount);
        }

        var editor = new ServiceAuthConfigEditor(app.Environment);
        var (root, path) = editor.Load();

        editor.CreateClient(root, serviceId, secret, new[] { realmId });
        editor.Save(root, path);

        Console.WriteLine($"Created service '{serviceId}' for realm '{realmId}' in '{path}'.");
        Console.WriteLine($"Secret: {secret}");
        Console.WriteLine("Restart the Auth service to apply config changes.");
        return 0;
    }

    private static async Task<int> ExecuteRealmRevokeServiceAsync(WebApplication app, IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command realms revoke-service <realmId> [--service-id <id>] [--keep-empty]");
            return 1;
        }

        var realmId = args[0].Trim();
        var specificServiceId = GetOption(args, "--service-id")?.Trim();
        var keepEmpty = HasFlag(args, "--keep-empty");

        var editor = new ServiceAuthConfigEditor(app.Environment);
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

    private static async Task<int> ExecuteSecurityAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintSecurityHelp();
            return 1;
        }

        var category = args[0].Trim().ToLowerInvariant();

        return category switch
        {
            "ip-ban" => await ExecuteIpBanAsync(services, args.Skip(1).ToArray()),
            _ => UnknownSecurityCommand(category)
        };
    }

    private static async Task<int> ExecuteIpBanAsync(IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command security ip-ban <list|add|remove> ...");
            return 1;
        }

        var dbContext = services.GetRequiredService<AuthDbContext>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    var query = dbContext.BannedIpAddresses.AsNoTracking();

                    if (!HasFlag(args, "--all"))
                    {
                        var nowUtc = DateTime.UtcNow;
                        query = query.Where(x =>
                            x.Enabled &&
                            (x.ExpiresAtUtc == null || x.ExpiresAtUtc > nowUtc));
                    }

                    var items = await query
                        .OrderBy(x => x.IpAddress)
                        .ToListAsync();

                    foreach (var item in items)
                    {
                        var expires = item.ExpiresAtUtc?.ToString("u") ?? "never";
                        Console.WriteLine($"{item.IpAddress} | enabled={item.Enabled} | expires={expires} | reason={item.Reason ?? "-"}");
                    }

                    return 0;
                }

            case "add":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command security ip-ban add <ip> [--reason <text>] [--hours <n>]");
                        return 1;
                    }

                    var ip = args[1].Trim();
                    if (!IPAddress.TryParse(ip, out _))
                    {
                        Console.Error.WriteLine($"Invalid IP address: {ip}");
                        return 1;
                    }

                    var reason = GetOption(args, "--reason")?.Trim();
                    var hours = ParseIntOption(GetOption(args, "--hours"), 0, min: 0);
                    DateTime? expiresAtUtc = hours > 0 ? DateTime.UtcNow.AddHours(hours) : null;

                    var existing = await dbContext.BannedIpAddresses.FirstOrDefaultAsync(x => x.IpAddress == ip);
                    if (existing is null)
                    {
                        existing = new BannedIpAddress
                        {
                            IpAddress = ip,
                            Reason = reason,
                            Enabled = true,
                            CreatedAtUtc = DateTime.UtcNow,
                            ExpiresAtUtc = expiresAtUtc
                        };

                        dbContext.BannedIpAddresses.Add(existing);
                    }
                    else
                    {
                        existing.Reason = reason;
                        existing.Enabled = true;
                        existing.ExpiresAtUtc = expiresAtUtc;
                    }

                    await dbContext.SaveChangesAsync();

                    Console.WriteLine($"IP '{ip}' added to denylist.");
                    return 0;
                }

            case "remove":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command security ip-ban remove <ip>");
                        return 1;
                    }

                    var ip = args[1].Trim();
                    var items = await dbContext.BannedIpAddresses
                        .Where(x => x.IpAddress == ip)
                        .ToListAsync();

                    if (items.Count == 0)
                    {
                        Console.Error.WriteLine($"IP '{ip}' is not in the denylist.");
                        return 1;
                    }

                    dbContext.BannedIpAddresses.RemoveRange(items);
                    await dbContext.SaveChangesAsync();

                    Console.WriteLine($"IP '{ip}' removed from denylist.");
                    return 0;
                }

            default:
                Console.Error.WriteLine("Usage: command security ip-ban <list|add|remove> ...");
                return 1;
        }
    }

    private static async Task<ServiceImportDocument> ReadServiceImportFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File was not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var document = JsonSerializer.Deserialize<ServiceImportDocument>(json, JsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException("Service import file could not be deserialized.");
        }

        document.Clients ??= new List<ServiceImportClient>();
        return document;
    }

    private static string GenerateDefaultServiceIdForRealm(string realmId)
    {
        return $"realm-{realmId}";
    }

    private static string GenerateSecret(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string MaskSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return "(empty)";
        }

        if (secret.Length <= 6)
        {
            return new string('*', secret.Length);
        }

        return $"{secret[..3]}***{secret[^3..]}";
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

    private static int ExecuteServices(WebApplication app, IServiceProvider services, string[] args)
    {
        if (args.Length == 0)
        {
            PrintServicesHelp();
            return 1;
        }

        var options = services.GetRequiredService<IOptions<ServiceAuthOptions>>().Value;
        var editor = new ServiceAuthConfigEditor(app.Environment);
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    foreach (var client in options.Clients.OrderBy(x => x.ServiceId, StringComparer.Ordinal))
                    {
                        var allowed = client.AllowedRealmIds.Length == 0
                            ? "-"
                            : string.Join(",", client.AllowedRealmIds);

                        Console.WriteLine($"{client.ServiceId} | realms={allowed} | secret={MaskSecret(client.Secret)}");
                    }

                    return 0;
                }

            case "show":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services show <serviceId>");
                        return 1;
                    }

                    var client = options.Clients.FirstOrDefault(x =>
                        string.Equals(x.ServiceId, args[1], StringComparison.Ordinal));

                    if (client is null)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    var output = new
                    {
                        client.ServiceId,
                        Secret = MaskSecret(client.Secret),
                        client.AllowedRealmIds
                    };

                    Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
                    return 0;
                }

            case "validate":
                {
                    var errors = new List<string>();
                    var warnings = new List<string>();

                    var duplicateServiceIds = options.Clients
                        .GroupBy(x => x.ServiceId, StringComparer.Ordinal)
                        .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
                        .Select(x => x.Key)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToList();

                    foreach (var serviceId in duplicateServiceIds)
                    {
                        errors.Add($"Duplicate service id: {serviceId}");
                    }

                    foreach (var client in options.Clients)
                    {
                        if (string.IsNullOrWhiteSpace(client.ServiceId))
                        {
                            errors.Add("One service client has an empty ServiceId.");
                        }

                        if (string.IsNullOrWhiteSpace(client.Secret))
                        {
                            errors.Add($"Service '{client.ServiceId}' has an empty secret.");
                        }

                        if (client.AllowedRealmIds.Length == 0)
                        {
                            warnings.Add($"Service '{client.ServiceId}' has no AllowedRealmIds.");
                        }

                        var duplicateRealms = client.AllowedRealmIds
                            .GroupBy(x => x, StringComparer.Ordinal)
                            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1)
                            .Select(x => x.Key)
                            .OrderBy(x => x, StringComparer.Ordinal)
                            .ToList();

                        foreach (var realmId in duplicateRealms)
                        {
                            errors.Add($"Service '{client.ServiceId}' contains duplicate allowed realm id '{realmId}'.");
                        }
                    }

                    if (errors.Count == 0)
                    {
                        Console.WriteLine("Service auth config is valid.");
                    }
                    else
                    {
                        Console.Error.WriteLine("Service auth config is INVALID:");
                        foreach (var error in errors)
                        {
                            Console.Error.WriteLine($"  - {error}");
                        }
                    }

                    if (warnings.Count > 0)
                    {
                        Console.WriteLine("Warnings:");
                        foreach (var warning in warnings)
                        {
                            Console.WriteLine($"  - {warning}");
                        }
                    }

                    return errors.Count == 0 ? 0 : 1;
                }

            case "create":
                {
                    var serviceId = GetOption(args, "--service-id");
                    if (string.IsNullOrWhiteSpace(serviceId))
                    {
                        Console.Error.WriteLine("Usage: command services create --service-id <id> [--secret <secret>] [--bytes <n>] [--realm <realmId>]");
                        return 1;
                    }

                    var secret = GetOption(args, "--secret");
                    if (string.IsNullOrWhiteSpace(secret))
                    {
                        var byteCount = ParseIntOption(GetOption(args, "--bytes"), 48, min: 16);
                        secret = GenerateSecret(byteCount);
                    }

                    var firstRealm = GetOption(args, "--realm");
                    var allowed = string.IsNullOrWhiteSpace(firstRealm)
                        ? Array.Empty<string>()
                        : new[] { firstRealm.Trim() };

                    var (root, path) = editor.Load();
                    editor.CreateClient(root, serviceId.Trim(), secret, allowed);
                    editor.Save(root, path);

                    Console.WriteLine($"Created service '{serviceId}' in '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "delete":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services delete <serviceId>");
                        return 1;
                    }

                    var (root, path) = editor.Load();
                    var deleted = editor.DeleteClient(root, args[1]);
                    if (!deleted)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Deleted service '{args[1]}' from '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "add-realm":
                {
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Usage: command services add-realm <serviceId> <realmId>");
                        return 1;
                    }

                    var (root, path) = editor.Load();
                    var ok = editor.AddRealm(root, args[1], args[2]);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Added realm '{args[2]}' to service '{args[1]}' in '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "remove-realm":
                {
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Usage: command services remove-realm <serviceId> <realmId>");
                        return 1;
                    }

                    var (root, path) = editor.Load();
                    var ok = editor.RemoveRealm(root, args[1], args[2]);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Removed realm '{args[2]}' from service '{args[1]}' in '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "set-secret":
                {
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Usage: command services set-secret <serviceId> <secret>");
                        return 1;
                    }

                    var (root, path) = editor.Load();
                    var ok = editor.SetSecret(root, args[1], args[2]);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Updated secret for service '{args[1]}' in '{path}'.");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "rotate-secret":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services rotate-secret <serviceId> [--bytes <n>]");
                        return 1;
                    }

                    var byteCount = ParseIntOption(GetOption(args, "--bytes"), 48, min: 16);
                    var newSecret = GenerateSecret(byteCount);

                    var (root, path) = editor.Load();
                    var ok = editor.SetSecret(root, args[1], newSecret);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Rotated secret for service '{args[1]}' in '{path}'.");
                    Console.WriteLine($"New secret: {newSecret}");
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            default:
                PrintServicesHelp();
                return 1;
        }
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

            case "cat":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command schema cat <migrationId>");
                        return 1;
                    }

                    var migration = await schemaService.GetMigrationAsync(args[1]);
                    if (migration is null)
                    {
                        Console.Error.WriteLine($"Migration '{args[1]}' was not found.");
                        return 1;
                    }

                    Console.WriteLine(migration.Sql);
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

    private static void PrintRealmRows(IEnumerable<Realm> realms)
    {
        foreach (var realm in realms)
        {
            var lastHeartbeat = realm.LastHeartbeatAt?.ToString("u") ?? "-";
            Console.WriteLine($"{realm.RealmId} | {realm.DisplayName} | official={realm.IsOfficial} | listed={realm.IsListed} | enabled={realm.Enabled} | status={realm.Status} | players={realm.CurrentPlayers}/{realm.MaxPlayers} | lastHeartbeat={lastHeartbeat}");
        }
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

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ParseBool(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Option '{optionName}' must be 'true' or 'false'.");
    }

    private static int ParseIntOption(string? rawValue, int defaultValue, int min)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(rawValue, out var parsed) || parsed < min)
        {
            throw new InvalidOperationException($"Integer option must be >= {min}.");
        }

        return parsed;
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
        Console.WriteLine("  realms list|show|stats|create|update|set-official|set-listed|set-enabled|quarantine|restore|create-service|revoke-service");
        Console.WriteLine("  services list|show|export|import");
        Console.WriteLine("  gamedata version|export|validate|replace");
        Console.WriteLine("  schema list|show|manifest|validate");
        Console.WriteLine("  security ip-ban list|add|remove");
        Console.WriteLine();

        PrintRealmHelp();
        PrintServicesHelp();
        PrintGameDataHelp();
        PrintSchemaHelp();
        PrintSecurityHelp();
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
        Console.WriteLine("  command realms create-service <realmId> [--service-id <id>] [--secret <secret>] [--bytes <n>]");
        Console.WriteLine("  command realms revoke-service <realmId> [--service-id <id>] [--keep-empty]");
        Console.WriteLine();
    }

    private static void PrintServicesHelp()
    {
        Console.WriteLine("Services:");
        Console.WriteLine("  command services list");
        Console.WriteLine("  command services show <serviceId>");
        Console.WriteLine("  command services validate");
        Console.WriteLine("  command services create --service-id <id> [--secret <secret>] [--bytes <n>] [--realm <realmId>]");
        Console.WriteLine("  command services delete <serviceId>");
        Console.WriteLine("  command services add-realm <serviceId> <realmId>");
        Console.WriteLine("  command services remove-realm <serviceId> <realmId>");
        Console.WriteLine("  command services set-secret <serviceId> <secret>");
        Console.WriteLine("  command services rotate-secret <serviceId> [--bytes <n>]");
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
        Console.WriteLine("  command schema cat <migrationId>");
        Console.WriteLine("  command schema manifest");
        Console.WriteLine("  command schema validate");
        Console.WriteLine();
    }

    private static void PrintSecurityHelp()
    {
        Console.WriteLine("Security:");
        Console.WriteLine("  command security ip-ban list [--all]");
        Console.WriteLine("  command security ip-ban add <ip> [--reason <text>] [--hours <n>]");
        Console.WriteLine("  command security ip-ban remove <ip>");
        Console.WriteLine();
    }

    private static int UnknownSecurityCommand(string category)
    {
        Console.Error.WriteLine($"Unknown security category '{category}'.");
        PrintSecurityHelp();
        return 1;
    }

    private sealed class ServiceImportDocument
    {
        public List<ServiceImportClient> Clients { get; set; } = new();
    }

    private sealed class ServiceImportClient
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string[] AllowedRealmIds { get; set; } = Array.Empty<string>();
    }
}