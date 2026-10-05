using System;

namespace Transponder.Integrations.OpenSky;

/// <summary>
/// What a poll asks for and how often. Two types rather than one: B-027 bans a credential reaching
/// a log line, and a single options object carrying both invites being logged whole — so the
/// secrets live on <see cref="OpenSkyCredentials"/> and nothing here is sensitive.
/// </summary>
internal sealed class OpenSkyOptions
{
    /// <summary>The configuration section both this and <see cref="OpenSkyCredentials"/> bind under.</summary>
    internal const string Section = "OpenSky";

    /// <summary>The provider's own base URL, which there is nothing to decide about.</summary>
    internal const string DefaultBaseUrl = "https://opensky-network.org/api";

    /// <summary>The interval a poll defaults to, per decisions/0001 (B-050).</summary>
    internal static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the provider's base URL. Defaulted, unlike <see cref="Box"/>: the URL is
    /// OpenSky's and nobody here chooses it, so compiling one in settles nothing a reader has a
    /// stake in. Configuration overrides it, which is what a test and a recording both need.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Gets or sets how often to poll. Fifteen seconds unless configuration says otherwise: above
    /// the registered tier's five-second resolution limit with room to spare, so lowering it in
    /// configuration cannot out-run the provider (decisions/0001).
    /// </summary>
    public TimeSpan PollInterval { get; set; } = DefaultPollInterval;

    /// <summary>
    /// Gets or sets the box to poll. <see langword="null"/> until configuration supplies one —
    /// B-050 gives the box no default, so an absent box fails validation rather than quietly
    /// becoming a box at the origin.
    /// </summary>
    public BoundingBox? Box { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a poll asks the provider for the aircraft category
    /// element. True
    /// because the application offers grouping by category, which is what B-025 makes
    /// <c>extended=1</c> conditional on. A poll without it yields snapshots whose category is
    /// absent rather than defaulted — B-018 decides presence by element count alone, so the two
    /// cannot disagree.
    /// </summary>
    public bool RequestsCategory { get; set; } = true;
}
