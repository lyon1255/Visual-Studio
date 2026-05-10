using GnosisAuthServer.Services;
using System.Text.Json;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class SchemaCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "schema";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            PrintDetailedHelp();
            return 1;
        }

        var schemaService = context.Services.GetRequiredService<ISchemaCatalogService>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    var manifest = await schemaService.GetManifestAsync();
                    foreach (var migration in manifest.Migrations)
                    {
                        Console.WriteLine($"{migration.Id} | destructive={migration.IsDestructive} | checksum={migration.ChecksumSha256}");
                    }

                    return 0;
                }

            case "show":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command schema show <migrationId>");
                        return 1;
                    }

                    var migration = await schemaService.GetMigrationAsync(args[1]);
                    if (migration is null)
                    {
                        Console.Error.WriteLine($"Migration '{args[1]}' was not found.");
                        return 1;
                    }

                    Console.WriteLine(JsonSerializer.Serialize(migration, CommandExecutionContext.JsonOptions));
                    return 0;
                }

            case "cat":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command schema cat <migrationId>");
                        return 1;
                    }

                    var migration = await schemaService.GetMigrationAsync(args[1]);
                    if (migration is null)
                    {
                        Console.Error.WriteLine($"Migration '{args[1]}' was not found.");
                        return 1;
                    }

                    Console.WriteLine(migration.Sql);
                    return 0;
                }

            case "manifest":
                {
                    var manifest = await schemaService.GetManifestAsync();
                    Console.WriteLine(JsonSerializer.Serialize(manifest, CommandExecutionContext.JsonOptions));
                    return 0;
                }

            case "validate":
                {
                    var manifest = await schemaService.GetManifestAsync();
                    Console.WriteLine($"Schema valid. Migrations={manifest.MigrationCount}, Latest='{manifest.LatestMigrationId}'.");
                    return 0;
                }

            default:
                PrintDetailedHelp();
                return 1;
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("  schema list|show|cat|manifest|validate");
    }

    private static void PrintDetailedHelp()
    {
        Console.WriteLine("  schema list");
        Console.WriteLine("  schema show <migrationId>");
        Console.WriteLine("  schema cat <migrationId>");
        Console.WriteLine("  schema manifest");
        Console.WriteLine("  schema validate");
    }
}