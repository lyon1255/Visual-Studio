using GnosisAuthServer.Data;
using Microsoft.EntityFrameworkCore;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class DbCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "db";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command db <ping|realms-count|accounts-count>");
            return 1;
        }

        var dbContext = context.Services.GetRequiredService<AuthDbContext>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "ping":
                {
                    var ok = await dbContext.Database.CanConnectAsync();
                    Console.WriteLine(ok ? "OK" : "FAILED");
                    return ok ? 0 : 1;
                }

            case "realms-count":
                Console.WriteLine(await dbContext.Realms.CountAsync());
                return 0;

            case "accounts-count":
                Console.WriteLine(await dbContext.Accounts.CountAsync());
                return 0;

            default:
                Console.Error.WriteLine("Usage: command db <ping|realms-count|accounts-count>");
                return 1;
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("  db ping|realms-count|accounts-count");
    }
}