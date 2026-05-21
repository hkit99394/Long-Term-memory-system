namespace MemorySystem.Api.Idempotency;

internal sealed class StoredJsonResult(int statusCode, string? json) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;

        if (json is null)
        {
            return;
        }

        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }
}
