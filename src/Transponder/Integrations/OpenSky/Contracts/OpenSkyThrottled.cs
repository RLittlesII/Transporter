namespace Transponder.Integrations.OpenSky.Contracts;

internal sealed record OpenSkyThrottled(TimeSpan RetryAfter);
