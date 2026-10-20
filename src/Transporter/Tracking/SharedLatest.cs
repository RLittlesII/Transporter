using System.Reactive.Disposables;
using System.Reactive.Subjects;
using LanguageExt;

namespace Transporter.Tracking;

/// <summary>Shares one connection and hands a later subscriber the latest value at once (fleet-pipeline B-039).</summary>
internal static class SharedLatest
{
    /// <summary>
    /// Connects the source for the first subscriber, gives every later one the latest value and then
    /// each new one, and drops both the connection and the latest value when the last one leaves.
    /// </summary>
    /// <typeparam name="T">The value the source produces.</typeparam>
    /// <param name="source">What is shared.</param>
    /// <returns>The shared stream.</returns>
    /// <remarks>
    /// Rejected: <c>Replay(1).RefCount()</c>, which keeps its replay across a reconnection and hands
    /// the next first subscriber a value from the connection that ended; and
    /// <c>Publish().RefCount()</c> with <c>StartWith</c>, where a later subscriber reads nothing
    /// until the next value (fleet-pipeline § 7).
    /// </remarks>
    public static IObservable<T> ShareLatest<T>(this IObservable<T> source) => new Shared<T>(source);

    /// <summary>The connection, its subscribers and the latest value, all guarded by one gate.</summary>
    /// <typeparam name="T">The value the source produces.</typeparam>
    /// <param name="source">What is shared.</param>
    /// <remarks>
    /// The gate is held while a value is recorded and forwarded and while a subscriber joins, so a
    /// joiner can neither miss a value nor read one twice. The source's synchronous delivery during
    /// connection re-enters the gate on the same thread, which <see cref="System.Threading.Monitor"/> allows.
    /// </remarks>
    private sealed class Shared<T>(IObservable<T> source) : IObservable<T>
    {
        /// <inheritdoc/>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            lock (_gate)
            {
                _subscribers++;
                var subscription = _subject.Subscribe(observer);
                if (_subscribers == 1)
                {
                    _connection = source.Subscribe(Latest, _subject.OnError, _subject.OnCompleted);
                }
                else
                {
                    _latest.IfSome(observer.OnNext);
                }

                return Disposable.Create(() => Leave(subscription));
            }
        }

        /// <summary>Records a value and forwards it.</summary>
        /// <param name="value">What the source produced.</param>
        private void Latest(T value)
        {
            lock (_gate)
            {
                _latest = value;
                _subject.OnNext(value);
            }
        }

        /// <summary>Removes a subscriber, and with the last one the connection and the latest value.</summary>
        /// <param name="subscription">The subscriber's subscription to the forwarding subject.</param>
        private void Leave(IDisposable subscription)
        {
            lock (_gate)
            {
                subscription.Dispose();
                _subscribers--;
                if (_subscribers > 0)
                {
                    return;
                }

                _connection.Dispose();
                _connection = Disposable.Empty;
                _latest = Option<T>.None;
                _subject.Dispose();
                _subject = new Subject<T>();
            }
        }

        private readonly object _gate = new();
        private Subject<T> _subject = new();
        private IDisposable _connection = Disposable.Empty;
        private Option<T> _latest = Option<T>.None;
        private int _subscribers;
    }
}
