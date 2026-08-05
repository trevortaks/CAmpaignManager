using System.Net;
using CampaignManager.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CampaignManager.UnitTests.Security;

public class SsrfGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.1.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata endpoint
    [InlineData("100.64.0.1")]        // CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    public void Blocks_private_and_reserved_addresses(string ip) =>
        SsrfGuard.IsPublic(IPAddress.Parse(ip)).Should().BeFalse();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]        // just outside 172.16/12
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void Allows_public_addresses(string ip) =>
        SsrfGuard.IsPublic(IPAddress.Parse(ip)).Should().BeTrue();

    [Theory]
    [InlineData("ftp://example.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    [InlineData("http://127.0.0.1/callback")]
    [InlineData("https://[::1]/callback")]
    public async Task Rejects_bad_schemes_and_literal_private_hosts(string url) =>
        (await SsrfGuard.IsSafePublicUrlAsync(url, CancellationToken.None)).Should().BeFalse();
}
