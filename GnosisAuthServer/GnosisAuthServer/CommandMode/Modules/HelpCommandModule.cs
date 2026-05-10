namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class HelpCommandModule : IAuthCommandModule
{
    private readonly IEnumerable<IAuthCommandModule> _modules;

    public HelpCommandModule(IEnumerable<IAuthCommandModule> modules)
    {
        _modules = modules;
    }

    public bool CanHandle(string category) => category == "help";

    public Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        foreach (var module in _modules.OrderBy(x => x.GetType().Name, StringComparer.Ordinal))
        {
            module.PrintHelp();
        }

        return Task.FromResult(0);
    }

    public void PrintHelp()
    {
        Console.WriteLine("  help");
    }
}