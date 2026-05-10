namespace GnosisAuthServer.CommandMode;

public static class AuthCommandModeRunner
{
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

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var modules = services.GetServices<IAuthCommandModule>().ToArray();
        var context = new CommandExecutionContext(app, services);

        if (commandArgs.Length == 0)
        {
            PrintGlobalHelp(modules);
            return 0;
        }

        var category = commandArgs[0].Trim().ToLowerInvariant();

        try
        {
            if (category == "help")
            {
                PrintGlobalHelp(modules);
                return 0;
            }

            var module = modules.FirstOrDefault(x => x.CanHandle(category));
            if (module is null)
            {
                Console.Error.WriteLine($"Unknown command category '{category}'.");
                PrintGlobalHelp(modules);
                return 1;
            }

            return await module.ExecuteAsync(context, category, commandArgs.Skip(1).ToArray());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    private static void PrintGlobalHelp(IEnumerable<IAuthCommandModule> modules)
    {
        Console.WriteLine("GnosisAuthServer command mode");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  GnosisAuthServer command <category> <action> [options]");
        Console.WriteLine("  GnosisAuthServer cmd <category> <action> [options]");
        Console.WriteLine();
        Console.WriteLine("Categories:");

        foreach (var module in modules.OrderBy(x => x.GetType().Name, StringComparer.Ordinal))
        {
            module.PrintHelp();
        }
    }
}