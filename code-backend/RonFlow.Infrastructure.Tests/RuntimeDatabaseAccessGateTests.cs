using RonFlow.Infrastructure;

namespace RonFlow.Infrastructure.Tests;

public sealed class RuntimeDatabaseAccessGateTests
{
    [Test]
    public async Task EnterExclusive_WaitsForActiveReaders_AndBlocksNewReadersUntilReleased()
    {
        var databaseAccessGate = new RuntimeDatabaseAccessGate();
        var activeReader = await databaseAccessGate.EnterReadAsync();
        var exclusiveAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exclusiveTask = Task.Run(() =>
        {
            exclusiveAttempted.SetResult();
            return databaseAccessGate.EnterExclusive();
        });

        await exclusiveAttempted.Task;
        Assert.That(exclusiveTask.IsCompleted, Is.False);

        activeReader.Dispose();
        using var exclusiveLease = await exclusiveTask.WaitAsync(TimeSpan.FromSeconds(1));

        var queuedReader = databaseAccessGate.EnterReadAsync().AsTask();
        Assert.That(queuedReader.IsCompleted, Is.False);

        exclusiveLease.Dispose();
        using var readerAfterCutover = await queuedReader.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
