using System.Net;
using MemorySystem.Api.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace MemorySystem.IntegrationTests;

public sealed class ForwardedHeadersOptionsTests
{
    [Fact]
    public void Configure_adds_configured_known_proxies_and_networks()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.10",
                ["ForwardedHeaders:KnownProxies:1"] = "2001:db8::10",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.20.0.0/16",
                ["ForwardedHeaders:KnownNetworks:1"] = "2001:db8::/32"
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        new MemorySystemForwardedHeadersOptionsSetup(configuration).Configure(options);

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.Contains(options.KnownProxies, proxy => proxy.Equals(IPAddress.Parse("10.0.0.10")));
        Assert.Contains(options.KnownProxies, proxy => proxy.Equals(IPAddress.Parse("2001:db8::10")));
        Assert.Contains(
            options.KnownIPNetworks,
            network => network.BaseAddress.Equals(IPAddress.Parse("10.20.0.0")) && network.PrefixLength == 16);
        Assert.Contains(
            options.KnownIPNetworks,
            network => network.BaseAddress.Equals(IPAddress.Parse("2001:db8::")) && network.PrefixLength == 32);
    }

    [Fact]
    public void Configure_accepts_delimited_proxy_and_network_values()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies"] = "10.0.0.10;10.0.0.11",
                ["ForwardedHeaders:KnownNetworks"] = "10.20.0.0/16,10.30.0.0/16"
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        new MemorySystemForwardedHeadersOptionsSetup(configuration).Configure(options);

        Assert.Contains(options.KnownProxies, proxy => proxy.Equals(IPAddress.Parse("10.0.0.10")));
        Assert.Contains(options.KnownProxies, proxy => proxy.Equals(IPAddress.Parse("10.0.0.11")));
        Assert.Contains(
            options.KnownIPNetworks,
            network => network.BaseAddress.Equals(IPAddress.Parse("10.20.0.0")) && network.PrefixLength == 16);
        Assert.Contains(
            options.KnownIPNetworks,
            network => network.BaseAddress.Equals(IPAddress.Parse("10.30.0.0")) && network.PrefixLength == 16);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "not-an-ip", "valid IP address")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0", "CIDR notation")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/33", "between 0 and 32")]
    public void Configure_rejects_invalid_forwarded_header_trust_configuration(
        string key,
        string value,
        string expectedMessage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = value
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new MemorySystemForwardedHeadersOptionsSetup(configuration).Configure(options));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }
}
