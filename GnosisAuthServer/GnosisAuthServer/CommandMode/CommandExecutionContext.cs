using System.Text.Json;

namespace GnosisAuthServer.CommandMode;

internal sealed class CommandExecutionContext
{
    public CommandExecutionContext(WebApplication app, IServiceProvider services)
    {
        App = app;
        Services = services;
    }

    public WebApplication App { get; }
    public IServiceProvider Services { get; }

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}