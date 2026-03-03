using System.Net;
using System.Net.Http.Json;
using OMG.Api.Management.Models;
using OMG.Api.Tests.Auth;

namespace OMG.Api.Tests;

public class ReportEndpointTests : IClassFixture<ManagementApiFactory>
{
    private readonly ManagementApiFactory _factory;
    private readonly HttpClient _client;

    public ReportEndpointTests(ManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGardenReport_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var response = await _client.GetAsync("/api/v1/reports/garden-report");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGardenReport_ReturnsOkWithReport_WhenAuthenticated()
    {
        await AuthTestHelper.AuthenticateAsync(_factory, _client, "report-user@example.com");

        var response = await _client.GetAsync("/api/v1/reports/garden-report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<GardenReportResponse>();
        Assert.NotNull(report);
        Assert.True(report.WateredCount >= 0);
        Assert.True(report.UnwateredCount >= 0);
        Assert.NotNull(report.WateringFrequencyPerPlant);
        Assert.True(report.PlantsAddedSince >= 0);
        Assert.True(report.PlantsDeletedSince >= 0);
    }

    [Fact]
    public async Task GetGardenReport_ReturnsOkWithZeros_WhenUserHasNoGardens()
    {
        await AuthTestHelper.AuthenticateAsync(_factory, _client, "report-no-gardens@example.com");

        var response = await _client.GetAsync("/api/v1/reports/garden-report?lastMinutes=60");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<GardenReportResponse>();
        Assert.NotNull(report);
        Assert.Equal(0, report.WateredCount);
        Assert.Equal(0, report.UnwateredCount);
        Assert.Empty(report.WateringFrequencyPerPlant);
    }

    [Fact]
    public async Task GetGardenReport_AcceptsQueryParameters()
    {
        await AuthTestHelper.AuthenticateAsync(_factory, _client, "report-params@example.com");

        var response = await _client.GetAsync(
            "/api/v1/reports/garden-report?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z&sinceDate=2026-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<GardenReportResponse>();
        Assert.NotNull(report);
        Assert.NotNull(report.PeriodStart);
        Assert.NotNull(report.PeriodEnd);
        Assert.NotNull(report.SinceDate);
    }
}
