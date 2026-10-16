---
title: "ADR-0017: The vessel client uses ClientWebSocket"
description: "AISStream's push feed is read with the framework's ClientWebSocket, negotiating permessage-deflate itself, rather than through the community AISStream.NET package."
type: adr
---

# ADR-0017: The vessel client uses ClientWebSocket

**Status:** accepted

**Accepted 2026-10-16.** The person's call, answering `vessel-source` § 11
row 2 before the vessel client's item
([`0097`](../../features/vessel-source/.issue/0097-vessel-client-connection.yml))
starts.

## Context

`vessel-source` needs one WebSocket to `wss://stream.aisstream.io/v0/stream`
that sends its subscription as the first frame (B-002), asks for compressed
transport (B-007), and closes when the vessel strategy is swapped out (B-005).
Since September 2026 the service limits the bandwidth of uncompressed
connections and drops messages over the limit, so a client that cannot
negotiate `permessage-deflate` loses ships on stage (`vessel-source` § 4
row 7). README.md § "Closing act" names two ways to hold the socket, and
`vessel-source` § 5 row 10 leaves the choice to a record rather than a claim.

## Decision drivers

- Compression must be negotiated, and the negotiation confirmed by the
  service's `SubscriptionConfirmation`.
- The subscription frame and the close must be this repository's code, where a
  test can see them, because B-002 and B-005 are claims about exactly those two
  moments.
- No dependency whose maintenance nobody here can vouch for, for the act that
  ends the talk.

## Considered options

| Option            | Summary                                                                                                                                      | Why not                                                                                                                                                                        |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ClientWebSocket` | The framework's socket. `ClientWebSocketOptions.DangerousDeflateOptions` negotiates `permessage-deflate`; AISStream's own C# example uses it | Chosen.                                                                                                                                                                        |
| `AISStream.NET`   | A community package with typed messages over its own socket                                                                                  | One more dependency, maintained outside this repository, that must still be shown to negotiate deflate, and that owns the subscription and the close a claim needs to observe. |

## Decision

**The vessel client holds a `System.Net.WebSockets.ClientWebSocket`** and sets
`DangerousDeflateOptions` before it connects. It writes the subscription frame
itself and reads the message envelope itself. No package joins
`Directory.Packages.props` for the socket.

The socket sits behind an interface of this repository's own, so a test drives
the client with frames it scripts rather than with a network. That interface
is the client's seam for testing. It is not a contract layer for AISStream,
which `vessel-source` § 5 row 1 forbids: it carries frames, not provider
operations.

## Consequences

- The message envelope (`MessageType`, `MetaData`, `Message`) is read with
  `System.Text.Json` in this repository, so the records for a position report
  and static data are written here. Message types the client does not ask for
  never need a type (B-011).
- Whether compression was granted is read from
  `SubscriptionConfirmation.Message.CompressionEnabled`, not assumed from the
  option being set.
- README.md § "Open items" no longer lists the WebSocket client as undecided.
