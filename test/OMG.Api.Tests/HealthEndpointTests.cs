using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OMG.Api.Tests;

public class HealthEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ManagementApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content));
    }
}
