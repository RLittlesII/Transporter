using Transporter.Integrations.AisStream;

namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>A single-use socket that fails on unscripted sends, reads or opens.</summary>
/// <remarks>Inline completion exposes the next read without a delay or a retry loop.</remarks>
internal sealed class ScriptedVesselSocket(string name, List<string> events) : IVesselSocket
{
    public Task Opened => _openedSignal.Task;

    public List<string> Sent { get; } = [];

    public bool IsOpen { get; private set; }

    public int Reads { get; private set; }

    public bool HoldClose { get; init; }

    public bool CloseStarted { get; private set; }

    public Uri? Endpoint { get; private set; }

    /// <inheritdoc/>
    public Task Open(Uri endpoint, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_opened)
        {
            throw new InvalidOperationException("A socket cannot open twice.");
        }

        _opened = true;
        IsOpen = true;
        Endpoint = endpoint;
        events.Add(name + ":open");
        _openedSignal.SetResult();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task Send(string frame, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!IsOpen || Sent.Count != 0 || Reads != 0)
        {
            throw new InvalidOperationException("The subscription must be the first and only sent frame.");
        }

        Sent.Add(frame);
        events.Add(name + ":send");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<string> Frames(CancellationToken cancellation)
    {
        if (!IsOpen || Sent.Count != 1 || _frames is not null)
        {
            throw new InvalidOperationException("Read requires a single subscription first.");
        }

        _frames = new FramesSequence(this, cancellation);
        return _frames;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        CloseStarted = true;
        events.Add(name + ":closing");
        if (HoldClose)
        {
            return new ValueTask(_closed.Task);
        }

        FinishClose();
        return ValueTask.CompletedTask;
    }

    public void ReleaseClose()
    {
        FinishClose();
        _closed.SetResult();
    }

    public void Receive(string frame)
    {
        if (_pending is null)
        {
            throw new InvalidOperationException("No read is pending.");
        }

        _current = frame;
        var pending = _pending;
        _pending = null;
        pending.SetResult(true);
    }

    public void ServiceCloses()
    {
        if (_pending is null)
        {
            throw new InvalidOperationException("No read is pending.");
        }

        var pending = _pending;
        _pending = null;
        pending.SetResult(false);
    }

    public void ServiceFails(Exception failure)
    {
        if (_pending is null)
        {
            throw new InvalidOperationException("No read is pending.");
        }

        var pending = _pending;
        _pending = null;
        pending.SetException(failure);
    }

    private void RecordRead() => events.Add(name + ":read");

    private void FinishClose()
    {
        IsOpen = false;
        events.Add(name + ":closed");
    }

    private sealed class FramesSequence(ScriptedVesselSocket socket, CancellationToken cancellation) : IAsyncEnumerable<string>, IAsyncEnumerator<string>
    {
        /// <inheritdoc/>
        public string Current => socket._current;

        /// <inheritdoc/>
        public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

        /// <inheritdoc/>
        public ValueTask<bool> MoveNextAsync()
        {
            cancellation.ThrowIfCancellationRequested();
            socket.Reads++;
            socket.RecordRead();
            socket._pending = new TaskCompletionSource<bool>();
            _registration.Dispose();
            var pending = socket._pending;
            _registration = cancellation.Register(() => pending.TrySetCanceled(cancellation));
            return new ValueTask<bool>(pending.Task);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _registration.Dispose();
            return ValueTask.CompletedTask;
        }

        private CancellationTokenRegistration _registration;
    }

    private readonly TaskCompletionSource _closed = new();

    private readonly TaskCompletionSource _openedSignal = new();

    private bool _opened;

    private string _current = string.Empty;

    private FramesSequence? _frames;

    private TaskCompletionSource<bool>? _pending;
}
