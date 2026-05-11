using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GnosisAuthServer.Infrastructure;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Request aborted by client. TraceId={TraceId} Method={Method} Path={Path} RemoteIp={RemoteIp}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, type, safeDetail) = exception switch
        {
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "https://httpstatuses.com/403",
                "You do not have permission to perform this action."),
            InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                "https://httpstatuses.com/400",
                "The request could not be processed."),
            JsonException => (
                StatusCodes.Status400BadRequest,
                "Malformed JSON",
                "https://httpstatuses.com/400",
                "The request body contains malformed JSON."),
            DbUpdateException => (
                StatusCodes.Status500InternalServerError,
                "Database error",
                "https://httpstatuses.com/500",
                "An unexpected error occurred."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "https://httpstatuses.com/500",
                "An unexpected error occurred.")
        };

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceIdentifier,
            ["RequestMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value,
            ["RequestQueryString"] = context.Request.QueryString.Value,
            ["RemoteIp"] = context.Connection.RemoteIpAddress?.ToString(),
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["AuthenticatedUser"] = context.User.Identity?.Name
        }))
        {
            _logger.LogError(exception, "Unhandled exception during request processing.");
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Could not write ProblemDetails response because the response has already started. TraceId={TraceId}", context.TraceIdentifier);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = type,
            Status = statusCode,
            Title = title,
            Detail = safeDetail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["timestampUtc"] = DateTime.UtcNow;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
