namespace MemorySystem.Api.Idempotency;

internal sealed class StoredJsonResult(int statusCode, string? json, string? contentType) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;

        if (json is null)
        {
            return;
        }

        httpContext.Response.ContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/json; charset=utf-8"
            : contentType;
        await httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }
}
