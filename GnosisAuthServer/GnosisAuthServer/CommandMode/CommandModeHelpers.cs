using System.Security.Cryptography;

namespace GnosisAuthServer.CommandMode;

internal static class CommandModeHelpers
{
    public static string? GetOption(string[] args, string optionName)
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

    public static bool HasFlag(string[] args, string flag)
    {
        return args.Any(x => string.Equals(x, flag, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ParseBool(string value, string optionName)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Option '{optionName}' must be 'true' or 'false'.");
    }

    public static int ParseIntOption(string? rawValue, int defaultValue, int min)
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

    public static string GenerateDefaultServiceIdForRealm(string realmId)
    {
        return $"realm-{realmId}";
    }

    public static string GenerateSecret(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string MaskSecret(string secret)
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
}