namespace OMG.Api.Telemetrics;

public sealed record TelemetryReadingRequest(
    string MeterId,
    int CurrentHumidity,
    bool IsWatering);

