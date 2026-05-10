namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class EnvironmentCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "environment";

    public Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "info", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: command environment info");
            return Task.FromResult(1);
        }

        Console.WriteLine($"Environment: {context.App.Environment.EnvironmentName}");
        Console.WriteLine($"ApplicationName: {context.App.Environment.ApplicationName}");
        Console.WriteLine($"ContentRootPath: {context.App.Environment.ContentRootPath}");
        return Task.FromResult(0);
    }

    public void PrintHelp()
    {
        Console.WriteLine("  environment info");
    }
}