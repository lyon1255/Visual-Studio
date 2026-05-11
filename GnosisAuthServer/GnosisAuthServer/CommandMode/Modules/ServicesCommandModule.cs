using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class ServicesCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "services";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            PrintDetailedHelp();
            return 1;
        }

        var options = context.Services.GetRequiredService<IOptions<ServiceAuthOptions>>().Value;
        var editor = new ServiceAuthConfigEditor(context.App.Environment);
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

                        Console.WriteLine($"{client.ServiceId} | realms={allowed} | secret={CommandModeHelpers.MaskSecret(client.Secret)}");
                    }

                    return 0;
                }

            case "show":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services show <serviceId> [--include-secret]");
                        return 1;
                    }

                    var includeSecret = CommandModeHelpers.HasFlag(args, "--include-secret");

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
                        Secret = includeSecret ? client.Secret : CommandModeHelpers.MaskSecret(client.Secret),
                        client.AllowedRealmIds
                    };

                    Console.WriteLine(JsonSerializer.Serialize(output, CommandExecutionContext.JsonOptions));
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
                        else if (LooksMasked(client.Secret))
                        {
                            errors.Add($"Service '{client.ServiceId}' appears to contain a masked secret instead of a real secret.");
                        }

                        if (client.Secret?.Length < 24)
                        {
                            warnings.Add($"Service '{client.ServiceId}' has a short secret. Consider rotating it to a stronger value.");
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
                    var serviceId = CommandModeHelpers.GetOption(args, "--service-id");
                    if (string.IsNullOrWhiteSpace(serviceId))
                    {
                        Console.Error.WriteLine("Usage: command services create --service-id <id> [--secret <secret>] [--bytes <n>] [--realm <realmId>] [--show-secret] [--secret-out <file>]");
                        return 1;
                    }

                    var secret = CommandModeHelpers.GetOption(args, "--secret");
                    if (string.IsNullOrWhiteSpace(secret))
                    {
                        var byteCount = CommandModeHelpers.ParseIntOption(CommandModeHelpers.GetOption(args, "--bytes"), 48, 16);
                        secret = CommandModeHelpers.GenerateSecret(byteCount);
                    }

                    if (LooksMasked(secret))
                    {
                        throw new InvalidOperationException("Refusing to create service with a masked-looking secret.");
                    }

                    var firstRealm = CommandModeHelpers.GetOption(args, "--realm");
                    var allowed = string.IsNullOrWhiteSpace(firstRealm)
                        ? Array.Empty<string>()
                        : new[] { firstRealm.Trim() };

                    var (root, path) = editor.Load();
                    editor.CreateClient(root, serviceId.Trim(), secret, allowed);
                    editor.Save(root, path);

                    Console.WriteLine($"Created service '{serviceId}' in '{path}'.");
                    PrintSecretOutput(secret, args);
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
                        Console.Error.WriteLine("Usage: command services set-secret <serviceId> <secret> [--show-secret] [--secret-out <file>]");
                        return 1;
                    }

                    var newSecret = args[2];
                    if (LooksMasked(newSecret))
                    {
                        throw new InvalidOperationException("Refusing to store a masked-looking secret.");
                    }

                    var (root, path) = editor.Load();
                    var ok = editor.SetSecret(root, args[1], newSecret);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Updated secret for service '{args[1]}' in '{path}'.");
                    PrintSecretOutput(newSecret, args);
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "rotate-secret":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services rotate-secret <serviceId> [--bytes <n>] [--show-secret] [--secret-out <file>]");
                        return 1;
                    }

                    var byteCount = CommandModeHelpers.ParseIntOption(CommandModeHelpers.GetOption(args, "--bytes"), 48, 16);
                    var newSecret = CommandModeHelpers.GenerateSecret(byteCount);

                    var (root, path) = editor.Load();
                    var ok = editor.SetSecret(root, args[1], newSecret);
                    if (!ok)
                    {
                        Console.Error.WriteLine($"Service '{args[1]}' was not found.");
                        return 1;
                    }

                    editor.Save(root, path);
                    Console.WriteLine($"Rotated secret for service '{args[1]}' in '{path}'.");
                    PrintSecretOutput(newSecret, args);
                    Console.WriteLine("Restart the Auth service to apply config changes.");
                    return 0;
                }

            case "export":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services export <file> [--include-secrets]");
                        return 1;
                    }

                    var includeSecrets = CommandModeHelpers.HasFlag(args, "--include-secrets");

                    var export = new ServiceImportDocument
                    {
                        Clients = options.Clients
                            .OrderBy(x => x.ServiceId, StringComparer.Ordinal)
                            .Select(x => new ServiceImportClient
                            {
                                ServiceId = x.ServiceId,
                                Secret = includeSecrets
                                    ? x.Secret
                                    : CommandModeHelpers.MaskSecret(x.Secret),
                                AllowedRealmIds = x.AllowedRealmIds
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(y => y, StringComparer.Ordinal)
                                    .ToArray()
                            })
                            .ToList()
                    };

                    await WriteSecureTextFileAsync(args[1], JsonSerializer.Serialize(export, CommandExecutionContext.JsonOptions));

                    Console.WriteLine(includeSecrets
                        ? $"Exported {export.Clients.Count} service client(s) with raw secrets to '{args[1]}'. Handle this file as sensitive."
                        : $"Exported {export.Clients.Count} service client(s) with masked secrets to '{args[1]}'.");

                    return 0;
                }

            case "import":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command services import <file> [--replace]");
                        return 1;
                    }

                    var replace = CommandModeHelpers.HasFlag(args, "--replace");
                    var document = await ReadServiceImportFileAsync(args[1]);

                    var (root, path) = editor.Load();
                    var clients = editor.GetClients(root);

                    if (replace)
                    {
                        clients.Clear();
                    }

                    foreach (var client in document.Clients.OrderBy(x => x.ServiceId, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(client.ServiceId))
                        {
                            throw new InvalidOperationException("Imported service contains empty ServiceId.");
                        }

                        if (string.IsNullOrWhiteSpace(client.Secret))
                        {
                            throw new InvalidOperationException($"Imported service '{client.ServiceId}' has empty secret.");
                        }

                        if (LooksMasked(client.Secret))
                        {
                            throw new InvalidOperationException(
                                $"Imported service '{client.ServiceId}' contains a masked secret. Use an export created with --include-secrets.");
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
                PrintDetailedHelp();
                return 1;
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("  services list|show|validate|create|delete|add-realm|remove-realm|set-secret|rotate-secret|export|import");
    }

    private static async Task<ServiceImportDocument> ReadServiceImportFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File was not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var document = JsonSerializer.Deserialize<ServiceImportDocument>(json, CommandExecutionContext.JsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException("Service import file could not be deserialized.");
        }

        document.Clients ??= new List<ServiceImportClient>();
        return document;
    }

    private static bool LooksMasked(string value)
    {
        return value.Contains("***", StringComparison.Ordinal) || value.All(x => x == '*');
    }

    private static void PrintSecretOutput(string secret, string[] args)
    {
        var showSecret = CommandModeHelpers.HasFlag(args, "--show-secret");
        var secretOut = CommandModeHelpers.GetOption(args, "--secret-out");

        if (!string.IsNullOrWhiteSpace(secretOut))
        {
            WriteSecureTextFileAsync(secretOut, secret).GetAwaiter().GetResult();
            Console.WriteLine($"Secret written to '{secretOut}' with restricted file permissions when supported.");
        }

        if (showSecret)
        {
            Console.WriteLine($"Secret: {secret}");
        }
        else if (string.IsNullOrWhiteSpace(secretOut))
        {
            Console.WriteLine("Secret generated/updated successfully. It was not printed. Use --show-secret or --secret-out <file> if you need to capture it.");
        }
    }

    private static async Task WriteSecureTextFileAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        TryRestrictFilePermissions(path);
    }

    private static void TryRestrictFilePermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
        }
    }

    private static void PrintDetailedHelp()
    {
        Console.WriteLine("  services list");
        Console.WriteLine("  services show <serviceId> [--include-secret]");
        Console.WriteLine("  services validate");
        Console.WriteLine("  services create --service-id <id> [--secret <secret>] [--bytes <n>] [--realm <realmId>] [--show-secret] [--secret-out <file>]");
        Console.WriteLine("  services delete <serviceId>");
        Console.WriteLine("  services add-realm <serviceId> <realmId>");
        Console.WriteLine("  services remove-realm <serviceId> <realmId>");
        Console.WriteLine("  services set-secret <serviceId> <secret> [--show-secret] [--secret-out <file>]");
        Console.WriteLine("  services rotate-secret <serviceId> [--bytes <n>] [--show-secret] [--secret-out <file>]");
        Console.WriteLine("  services export <file> [--include-secrets]");
        Console.WriteLine("  services import <file> [--replace]");
    }
}