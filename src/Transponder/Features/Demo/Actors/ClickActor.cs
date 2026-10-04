using System.Diagnostics.CodeAnalysis;
using Akka.Actor;
using Akka.Util.Internal;

namespace Transponder.Features.Demo.Actors;

public class ClickActor : ReceiveActor
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public ClickActor() =>
        Receive<Click>(_ =>
        {
            var count = _counter.IncrementAndGet();
            var text = count == 1 ? $"Clicked {count} time" : $"Clicked {count} times";
            Sender.Tell(text);
        });

    public static Props Props { get; } = Props.Create(static () => new ClickActor());

    private readonly AtomicCounter _counter = new AtomicCounter(0);
}

internal class Click
{
    private Click() { }

    public static readonly Click Instance = new();
}
