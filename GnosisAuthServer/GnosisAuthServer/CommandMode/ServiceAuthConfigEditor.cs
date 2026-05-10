using Microsoft.AspNetCore.Hosting;
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
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidOperationException($"Config file is not a valid JSON object: {path}");
        }
        else
        {
            root = new JsonObject();
        }

        return (root, path);
    }

    public void Save(JsonObject root, string path)
    {
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
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
        var clients = GetClients(root);

        foreach (var node in clients)
        {
            if (node is not JsonObject client)
            {
                continue;
            }

            var currentServiceId = client["ServiceId"]?.GetValue<string>();
            if (string.Equals(currentServiceId, serviceId, StringComparison.Ordinal))
            {
                return client;
            }
        }

        return null;
    }

    public bool DeleteClient(JsonObject root, string serviceId)
    {
        var clients = GetClients(root);

        for (var i = 0; i < clients.Count; i++)
        {
            if (clients[i] is not JsonObject client)
            {
                continue;
            }

            var currentServiceId = client["ServiceId"]?.GetValue<string>();
            if (string.Equals(currentServiceId, serviceId, StringComparison.Ordinal))
            {
                clients.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public JsonObject CreateClient(JsonObject root, string serviceId, string secret, IEnumerable<string>? allowedRealmIds = null)
    {
        var existing = FindClient(root, serviceId);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Service '{serviceId}' already exists.");
        }

        var client = new JsonObject
        {
            ["ServiceId"] = serviceId,
            ["Secret"] = secret
        };

        var allowed = new JsonArray();
        if (allowedRealmIds is not null)
        {
            foreach (var realmId in allowedRealmIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
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

        var allowed = GetAllowedRealmIdsArray(client);

        if (allowed.Any(x => string.Equals(x?.GetValue<string>(), realmId, StringComparison.Ordinal)))
        {
            return true;
        }

        allowed.Add(realmId);
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

        var allowed = GetAllowedRealmIdsArray(client);

        for (var i = 0; i < allowed.Count; i++)
        {
            var value = allowed[i]?.GetValue<string>();
            if (string.Equals(value, realmId, StringComparison.Ordinal))
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

        client["Secret"] = secret;
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
}