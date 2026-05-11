using GnosisAuthServer.Data;
using GnosisAuthServer.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GnosisAuthServer.CommandMode.Modules;

internal sealed class SecurityCommandModule : IAuthCommandModule
{
    public bool CanHandle(string category) => category == "security";

    public async Task<int> ExecuteAsync(CommandExecutionContext context, string category, string[] args)
    {
        if (args.Length == 0)
        {
            PrintDetailedHelp();
            return 1;
        }

        var subCategory = args[0].Trim().ToLowerInvariant();

        return subCategory switch
        {
            "ip-ban" => await ExecuteIpBanAsync(context, args.Skip(1).ToArray()),
            _ => UnknownSecurityCommand(subCategory)
        };
    }

    public void PrintHelp()
    {
        Console.WriteLine("  security ip-ban list|add|remove");
    }

    private static async Task<int> ExecuteIpBanAsync(CommandExecutionContext context, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: command security ip-ban <list|add|remove> ...");
            return 1;
        }

        var dbContext = context.Services.GetRequiredService<AuthDbContext>();
        var ipBanCache = context.Services.GetRequiredService<IIpBanCacheService>();
        var action = args[0].Trim().ToLowerInvariant();

        switch (action)
        {
            case "list":
                {
                    var query = dbContext.BannedIpAddresses.AsNoTracking();

                    if (!CommandModeHelpers.HasFlag(args, "--all"))
                    {
                        var nowUtc = DateTime.UtcNow;
                        query = query.Where(x =>
                            x.Enabled &&
                            (x.ExpiresAtUtc == null || x.ExpiresAtUtc > nowUtc));
                    }

                    var items = await query
                        .OrderBy(x => x.IpAddress)
                        .ToListAsync();

                    if (items.Count == 0)
                    {
                        Console.WriteLine("No banned IP addresses found.");
                        return 0;
                    }

                    foreach (var item in items)
                    {
                        var expires = item.ExpiresAtUtc?.ToString("u") ?? "never";
                        Console.WriteLine($"{item.IpAddress} | enabled={item.Enabled} | expires={expires} | reason={item.Reason ?? "-"}");
                    }

                    return 0;
                }

            case "add":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command security ip-ban add <ip> [--reason <text>] [--hours <n>]");
                        return 1;
                    }

                    var ip = args[1].Trim();
                    if (!IPAddress.TryParse(ip, out _))
                    {
                        Console.Error.WriteLine($"Invalid IP address: {ip}");
                        return 1;
                    }

                    var reason = CommandModeHelpers.GetOption(args, "--reason")?.Trim();
                    var hours = CommandModeHelpers.ParseIntOption(
                        CommandModeHelpers.GetOption(args, "--hours"),
                        0,
                        0);

                    DateTime? expiresAtUtc = hours > 0
                        ? DateTime.UtcNow.AddHours(hours)
                        : null;

                    var existing = await dbContext.BannedIpAddresses
                        .FirstOrDefaultAsync(x => x.IpAddress == ip);

                    if (existing is null)
                    {
                        existing = new BannedIpAddress
                        {
                            IpAddress = ip,
                            Reason = reason,
                            Enabled = true,
                            CreatedAtUtc = DateTime.UtcNow,
                            ExpiresAtUtc = expiresAtUtc
                        };

                        dbContext.BannedIpAddresses.Add(existing);
                    }
                    else
                    {
                        existing.Reason = reason;
                        existing.Enabled = true;
                        existing.ExpiresAtUtc = expiresAtUtc;
                    }

                    await dbContext.SaveChangesAsync();
                    ipBanCache.Invalidate(ip);

                    Console.WriteLine($"IP '{ip}' added to denylist.");
                    return 0;
                }

            case "remove":
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine("Usage: command security ip-ban remove <ip>");
                        return 1;
                    }

                    var ip = args[1].Trim();
                    var items = await dbContext.BannedIpAddresses
                        .Where(x => x.IpAddress == ip)
                        .ToListAsync();

                    if (items.Count == 0)
                    {
                        Console.Error.WriteLine($"IP '{ip}' is not in the denylist.");
                        return 1;
                    }

                    dbContext.BannedIpAddresses.RemoveRange(items);
                    await dbContext.SaveChangesAsync();
                    ipBanCache.Invalidate(ip);

                    Console.WriteLine($"IP '{ip}' removed from denylist.");
                    return 0;
                }

            default:
                Console.Error.WriteLine("Usage: command security ip-ban <list|add|remove> ...");
                return 1;
        }
    }

    private static int UnknownSecurityCommand(string category)
    {
        Console.Error.WriteLine($"Unknown security category '{category}'.");
        PrintDetailedHelp();
        return 1;
    }

    private static void PrintDetailedHelp()
    {
        Console.WriteLine("  security ip-ban list [--all]");
        Console.WriteLine("  security ip-ban add <ip> [--reason <text>] [--hours <n>]");
        Console.WriteLine("  security ip-ban remove <ip>");
    }
}