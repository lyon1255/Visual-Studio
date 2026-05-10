using GnosisAuthServer.Security;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class JwtCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "jwt";

    public Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "check", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: command jwt check");
            return Task.FromResult(1);
        }

        var keyProvider = context.Services.GetRequiredService<IRsaKeyProvider>();
        _ = keyProvider.GetSigningKey();
        _ = keyProvider.GetValidationKey();

        Console.WriteLine("JWT key provider OK");
        return Task.FromResult(0);
    }

    public void PrintHelp()
    {
        Console.WriteLine("  jwt check");
    }
}