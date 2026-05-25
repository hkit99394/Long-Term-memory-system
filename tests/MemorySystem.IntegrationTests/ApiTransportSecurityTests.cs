using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.IntegrationTests;

public sealed class ApiTransportSecurityTests
{
    [Fact]
    public async Task Non_testing_http_requests_are_rejected_before_api_key_authentication()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Staging");
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            "Host=unused;Database=unused;Username=unused;Password=unused",
                        ["Authentication:ApiKey:Keys:test-key:Key"] = "test-api-key",
                        ["Authentication:ApiKey:Keys:test-key:PrincipalId"] = "11111111-1111-1111-1111-111111111111",
                        ["Authentication:ApiKey:Keys:test-key:DisplayName"] = "Test API caller"
                    });
                });
            });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Api-Key", "test-api-key");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("HTTPS is required for this API.", body);
    }
}
