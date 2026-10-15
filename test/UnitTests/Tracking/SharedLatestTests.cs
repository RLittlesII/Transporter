using System.Reactive.Linq;
using System.Reactive.Subjects;
using AwesomeAssertions;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking;

public class SharedLatestTests
{
    /// <summary>
    /// fleet-pipeline B-039, the operator alone. A later subscriber reads the latest value at once,
    /// and after the last leaves the next one connects afresh and reads only what the new connection
    /// produces. The hazard is <c>Replay(1).RefCount()</c>, which keeps its replay across the
    /// reconnection and hands the newcomer a value from the connection that ended.
    /// </summary>
    [Fact]
    public void GivenALatestValue_WhenALaterSubscriberJoinsAndAllLeaveAndOneReturns_ThenItReadsTheLatestThenOnlyTheNewConnection()
    {
        // Given
        var connections = 0;
        var values = new Subject<int>();
        var source = Observable.Defer(() =>
        {
            connections++;

            return values.StartWith(-connections);
        });
        var sut = source.ShareLatest();
        var first = new List<int>();
        var firstSubscription = sut.Subscribe(first.Add);
        values.OnNext(7);
        var second = new List<int>();

        // When
        var secondSubscription = sut.Subscribe(second.Add);
        firstSubscription.Dispose();
        secondSubscription.Dispose();
        var third = new List<int>();
        using var thirdSubscription = sut.Subscribe(third.Add);

        // Then
        first.Should().Equal(-1, 7);
        second.Should().Equal([7], "a later subscriber reads the latest at once");
        third.Should().Equal([-2], "the reconnection's own first value, and nothing kept from the last");
        connections.Should().Be(2, "one connection while subscribed, a fresh one after");
    }
}
