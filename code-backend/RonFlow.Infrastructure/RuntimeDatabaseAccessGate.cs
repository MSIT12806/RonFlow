namespace RonFlow.Infrastructure;

public interface IRuntimeDatabaseAccessGate
{
    ValueTask<IDisposable> EnterReadAsync(CancellationToken cancellationToken = default);

    IDisposable EnterExclusive(CancellationToken cancellationToken = default);
}

public sealed class RuntimeDatabaseAccessGate : IRuntimeDatabaseAccessGate
{
    private readonly object syncRoot = new();
    private int activeReaders;
    private bool exclusiveRequested;
    private TaskCompletionSource? readersDrained;
    private TaskCompletionSource? admissionOpened;

    public async ValueTask<IDisposable> EnterReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task? waitForAdmission = null;
            lock (syncRoot)
            {
                if (!exclusiveRequested)
                {
                    activeReaders++;
                    return new ReadLease(this);
                }

                admissionOpened ??= CreateCompletionSource();
                waitForAdmission = admissionOpened.Task;
            }

            await waitForAdmission.WaitAsync(cancellationToken);
        }
    }

    public IDisposable EnterExclusive(CancellationToken cancellationToken = default)
    {
        Task? waitForReaders = null;
        lock (syncRoot)
        {
            if (exclusiveRequested)
            {
                throw new InvalidOperationException("The runtime database is already being synchronized.");
            }

            exclusiveRequested = true;
            if (activeReaders == 0)
            {
                return new ExclusiveLease(this);
            }

            readersDrained = CreateCompletionSource();
            waitForReaders = readersDrained.Task;
        }

        try
        {
            waitForReaders.Wait(cancellationToken);
            return new ExclusiveLease(this);
        }
        catch
        {
            ExitExclusive();
            throw;
        }
    }

    private void ExitRead()
    {
        TaskCompletionSource? drained = null;
        lock (syncRoot)
        {
            activeReaders--;
            if (activeReaders < 0)
            {
                throw new InvalidOperationException("The runtime database read lease was released more than once.");
            }

            if (activeReaders == 0 && exclusiveRequested)
            {
                drained = readersDrained;
                readersDrained = null;
            }
        }

        drained?.TrySetResult();
    }

    private void ExitExclusive()
    {
        TaskCompletionSource? admission = null;
        lock (syncRoot)
        {
            exclusiveRequested = false;
            readersDrained = null;
            admission = admissionOpened;
            admissionOpened = null;
        }

        admission?.TrySetResult();
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ReadLease(RuntimeDatabaseAccessGate owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.ExitRead();
            }
        }
    }

    private sealed class ExclusiveLease(RuntimeDatabaseAccessGate owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.ExitExclusive();
            }
        }
    }
}
