using GnosisAuthServer.Data;
using GnosisAuthServer.Options;
using Microsoft.Extensions.Options;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class DoctorCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "doctor";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        var dbContext = context.Services.GetRequiredService<AuthDbContext>();
        var jwtOptions = context.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var serviceAuthOptions = context.Services.GetRequiredService<IOptions<ServiceAuthOptions>>().Value;

        Console.WriteLine("Doctor report");
        Console.WriteLine("-------------");
        Console.WriteLine($"Environment: {context.App.Environment.EnvironmentName}");
        Console.WriteLine($"ContentRoot: {context.App.Environment.ContentRootPath}");
        Console.WriteLine($"JWT private key: {jwtOptions.PrivateKeyPemPath}");
        Console.WriteLine($"JWT public key: {jwtOptions.PublicKeyPemPath}");
        Console.WriteLine($"Service auth enabled: {serviceAuthOptions.Enabled}");
        Console.WriteLine($"Configured service clients: {serviceAuthOptions.Clients.Count}");

        var dbOk = await dbContext.Database.CanConnectAsync();
        Console.WriteLine($"Database: {(dbOk ? "OK" : "FAILED")}");
        Console.WriteLine($"JWT private key exists: {File.Exists(jwtOptions.PrivateKeyPemPath)}");
        Console.WriteLine($"JWT public key exists: {File.Exists(jwtOptions.PublicKeyPemPath)}");

        return dbOk ? 0 : 1;
    }

    public void PrintHelp()
    {
        Console.WriteLine("  doctor");
    }
}