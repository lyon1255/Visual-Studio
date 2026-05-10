namespace GnosisAuthServer.CommandMode;

internal interface IAuthCommandModule
{
    bool CanHandle(string category);

    Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args);

    void PrintHelp();
}
