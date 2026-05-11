using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GnosisAuthServer.CommandMode;

internal sealed class ServiceAuthConfigEditor
{
    private readonly IWebHostEnvironment _environment;

    public ServiceAuthConfigEditor(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public (JsonObject Root, string Path) Load()
    {
        var path = ResolveWritableConfigPath();

        JsonObject root;
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path, Encoding.UTF8);

            root = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException($"Config file is not a valid JSON object: {path}");
        }
        else
        {
            root = new JsonObject();
        }

        EnsureServiceAuthStructure(root);
        NormalizeClients(root);

        return (root, path);
    }

    public void Save(JsonObject root, string path)
    {
        EnsureServiceAuthStructure(root);
        NormalizeClients(root);
        ValidateBeforeSave(root);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Could not determine config directory for path: {path}");
        }

        Directory.CreateDirectory(directory);

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var tempPath = Path.Combine(
            directory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        var backupPath = Path.Combine(
            directory,
            $"{Path.GetFileName(path)}.bak");

        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        TryRestrictFilePermissions(tempPath);

        if (File.Exists(path))
        {
            File.Copy(path, backupPath, overwrite: true);
            TryRestrictFilePermissions(backupPath);
        }

        File.Move(tempPath, path, overwrite: true);
        TryRestrictFilePermissions(path);
    }

    public JsonArray GetClients(JsonObject root)
    {
        var serviceAuth = root["ServiceAuth"] as JsonObject;
        if (serviceAuth is null)
        {
            serviceAuth = new JsonObject();
            root["ServiceAuth"] = serviceAuth;
        }

        var clients = serviceAuth["Clients"] as JsonArray;
        if (clients is null)
        {
            clients = new JsonArray();
            serviceAuth["Clients"] = clients;
        }

        return clients;
    }

    public JsonObject? FindClient(JsonObject root, string serviceId)
    {
        var normalizedServiceId = NormalizeServiceId(serviceId);
        var clients = GetClients(root);

        foreach (var node in clients)
        {
            if (node is not JsonObject client)
            {
                continue;
            }

            var currentServiceId = client["ServiceId"]?.GetValue<string>();
            if (string.Equals(currentServiceId, normalizedServiceId, StringComparison.Ordinal))
            {
                return client;
            }
        }

        return null;
    }

    public bool DeleteClient(JsonObject root, string serviceId)
    {
        var normalizedServiceId = NormalizeServiceId(serviceId);
        var clients = GetClients(root);

        for (var i = 0; i < clients.Count; i++)
        {
            if (clients[i] is not JsonObject client)
            {
                continue;
            }

            var currentServiceId = client["ServiceId"]?.GetValue<string>();
            if (string.Equals(currentServiceId, normalizedServiceId, StringComparison.Ordinal))
            {
                clients.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public JsonObject CreateClient(JsonObject root, string serviceId, string secret, IEnumerable<string>? allowedRealmIds = null)
    {
        var normalizedServiceId = NormalizeServiceId(serviceId);
        var normalizedSecret = NormalizeSecret(secret);

        var existing = FindClient(root, normalizedServiceId);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Service '{normalizedServiceId}' already exists.");
        }

        var client = new JsonObject
        {
            ["ServiceId"] = normalizedServiceId,
            ["Secret"] = normalizedSecret
        };

        var allowed = new JsonArray();
        if (allowedRealmIds is not null)
        {
            foreach (var realmId in allowedRealmIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeRealmId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal))
            {
                allowed.Add(realmId);
            }
        }

        client["AllowedRealmIds"] = allowed;

        GetClients(root).Add(client);
        return client;
    }

    public bool AddRealm(JsonObject root, string serviceId, string realmId)
    {
        var client = FindClient(root, serviceId);
        if (client is null)
        {
            return false;
        }

        var normalizedRealmId = NormalizeRealmId(realmId);
        var allowed = GetAllowedRealmIdsArray(client);

        if (allowed.Any(x => string.Equals(x?.GetValue<string>(), normalizedRealmId, StringComparison.Ordinal)))
        {
            return true;
        }

        allowed.Add(normalizedRealmId);
        SortJsonStringArray(allowed);
        return true;
    }

    public bool RemoveRealm(JsonObject root, string serviceId, string realmId)
    {
        var client = FindClient(root, serviceId);
        if (client is null)
        {
            return false;
        }

        var normalizedRealmId = NormalizeRealmId(realmId);
        var allowed = GetAllowedRealmIdsArray(client);

        for (var i = 0; i < allowed.Count; i++)
        {
            var value = allowed[i]?.GetValue<string>();
            if (string.Equals(value, normalizedRealmId, StringComparison.Ordinal))
            {
                allowed.RemoveAt(i);
                return true;
            }
        }

        return true;
    }

    public bool SetSecret(JsonObject root, string serviceId, string secret)
    {
        var client = FindClient(root, serviceId);
        if (client is null)
        {
            return false;
        }

        client["Secret"] = NormalizeSecret(secret);
        return true;
    }

    private string ResolveWritableConfigPath()
    {
        var envSpecific = Path.Combine(_environment.ContentRootPath, $"appsettings.{_environment.EnvironmentName}.json");
        var baseConfig = Path.Combine(_environment.ContentRootPath, "appsettings.json");

        if (File.Exists(envSpecific))
        {
            return envSpecific;
        }

        if (File.Exists(baseConfig))
        {
            return baseConfig;
        }

        return envSpecific;
    }

    private void EnsureServiceAuthStructure(JsonObject root)
    {
        var serviceAuth = root["ServiceAuth"] as JsonObject;
        if (serviceAuth is null)
        {
            serviceAuth = new JsonObject();
            root["ServiceAuth"] = serviceAuth;
        }

        if (serviceAuth["Clients"] is not JsonArray)
        {
            serviceAuth["Clients"] = new JsonArray();
        }
    }

    private void NormalizeClients(JsonObject root)
    {
        var clients = GetClients(root);

        for (var i = clients.Count - 1; i >= 0; i--)
        {
            if (clients[i] is not JsonObject client)
            {
                clients.RemoveAt(i);
                continue;
            }

            NormalizeClient(client);
        }

        var normalizedClients = clients
            .OfType<JsonObject>()
            .OrderBy(x => x["ServiceId"]?.GetValue<string>() ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        clients.Clear();
        foreach (var client in normalizedClients)
        {
            clients.Add(client);
        }
    }

    private static void NormalizeClient(JsonObject client)
    {
        var serviceId = client["ServiceId"]?.GetValue<string>() ?? string.Empty;
        var secret = client["Secret"]?.GetValue<string>() ?? string.Empty;

        client["ServiceId"] = NormalizeServiceId(serviceId);
        client["Secret"] = NormalizeSecret(secret);

        var allowed = GetAllowedRealmIdsArray(client);

        var normalizedRealmIds = allowed
            .Select(x => x?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => NormalizeRealmId(x!))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        allowed.Clear();
        foreach (var realmId in normalizedRealmIds)
        {
            allowed.Add(realmId);
        }
    }

    private void ValidateBeforeSave(JsonObject root)
    {
        var clients = GetClients(root);

        var seenServiceIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in clients)
        {
            if (node is not JsonObject client)
            {
                throw new InvalidOperationException("ServiceAuth.Clients contains a non-object entry.");
            }

            var serviceId = client["ServiceId"]?.GetValue<string>();
            var secret = client["Secret"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(serviceId))
            {
                throw new InvalidOperationException("A service client contains an empty ServiceId.");
            }

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException($"Service '{serviceId}' contains an empty secret.");
            }

            if (!seenServiceIds.Add(serviceId))
            {
                throw new InvalidOperationException($"Duplicate service id detected: '{serviceId}'.");
            }

            var allowed = GetAllowedRealmIdsArray(client);
            var seenRealmIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var realmNode in allowed)
            {
                var realmId = realmNode?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(realmId))
                {
                    throw new InvalidOperationException($"Service '{serviceId}' contains an empty AllowedRealmId.");
                }

                if (!seenRealmIds.Add(realmId))
                {
                    throw new InvalidOperationException($"Service '{serviceId}' contains duplicate AllowedRealmId '{realmId}'.");
                }
            }
        }
    }

    private static string NormalizeServiceId(string serviceId)
    {
        var normalized = serviceId.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("ServiceId cannot be empty.");
        }

        return normalized;
    }

    private static string NormalizeSecret(string secret)
    {
        var normalized = secret.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Secret cannot be empty.");
        }

        return normalized;
    }

    private static string NormalizeRealmId(string realmId)
    {
        var normalized = realmId.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("RealmId cannot be empty.");
        }

        return normalized;
    }

    private static JsonArray GetAllowedRealmIdsArray(JsonObject client)
    {
        var allowed = client["AllowedRealmIds"] as JsonArray;
        if (allowed is null)
        {
            allowed = new JsonArray();
            client["AllowedRealmIds"] = allowed;
        }

        return allowed;
    }

    private static void SortJsonStringArray(JsonArray array)
    {
        var values = array
            .Select(x => x?.GetValue<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        array.Clear();
        foreach (var value in values)
        {
            array.Add(value);
        }
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
}