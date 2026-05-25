using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace MemorySystem.Api.Http;

public sealed class MemorySystemForwardedHeadersOptionsSetup(IConfiguration configuration)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    private const string SectionName = "ForwardedHeaders";

    public void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var section = configuration.GetSection(SectionName);

        foreach (var proxy in ReadConfiguredValues(section.GetSection("KnownProxies")))
        {
            options.KnownProxies.Add(ParseIpAddress(proxy, "known proxy"));
        }

        foreach (var network in ReadConfiguredValues(section.GetSection("KnownNetworks")))
        {
            options.KnownIPNetworks.Add(ParseKnownNetwork(network));
        }
    }

    private static IEnumerable<string> ReadConfiguredValues(IConfigurationSection section)
    {
        var children = section.GetChildren().ToArray();

        if (children.Length == 0)
        {
            return SplitConfiguredValue(section.Value);
        }

        return children.SelectMany(child => SplitConfiguredValue(child.Value));
    }

    private static IEnumerable<string> SplitConfiguredValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                new[] { ',', ';' },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static IPAddress ParseIpAddress(string value, string description)
    {
        return IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException(
                $"ForwardedHeaders {description} '{value}' must be a valid IP address.");
    }

    private static System.Net.IPNetwork ParseKnownNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders known network '{value}' must use CIDR notation, for example '10.0.0.0/8'.");
        }

        var prefix = ParseIpAddress(parts[0], "known network prefix");

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders known network '{value}' must include a numeric prefix length.");
        }

        var maxPrefixLength = prefix.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => throw new InvalidOperationException(
                $"ForwardedHeaders known network '{value}' must use an IPv4 or IPv6 prefix.")
        };

        if (prefixLength < 0 || prefixLength > maxPrefixLength)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders known network '{value}' prefix length must be between 0 and {maxPrefixLength}.");
        }

        return new System.Net.IPNetwork(prefix, prefixLength);
    }
}
