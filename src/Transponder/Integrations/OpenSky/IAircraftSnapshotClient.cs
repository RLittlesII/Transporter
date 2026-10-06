using System;

namespace Transponder.Integrations.OpenSky;

/// <summary>The poller a strategy's subscription starts and stops.</summary>
internal interface IAircraftSnapshotClient
{
    /// <summary>Starts polling, and keeps polling until the returned subscription is disposed.</summary>
    /// <returns>The subscription that stops the polling.</returns>
    IDisposable Poll();
}
