using System.Reflection;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class VersionCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "version";

    public Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"GnosisAuthServer {version}");
        return Task.FromResult(0);
    }

    public void PrintHelp()
    {
        Console.WriteLine("  version");
    }
}