
using WorkerKit.Hosting.Queues;

using WorkerKit.Abstractions.Queues;
namespace WorkerKit.Hosting.Tests;

public sealed class InMemoryWorkQueueTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Claim_does_not_lease_the_same_item_twice()
    {
        var queue = NewQueue();
        queue.Enqueue("one");
        queue.Enqueue("two");

        var first = await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None);
        var second = await queue.ClaimAsync(10, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal("one", Assert.Single(first).Payload);
        Assert.Equal("two", Assert.Single(second).Payload);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task Claim_keeps_one_live_lease_per_partition_key()
    {
        var queue = NewQueue();
        queue.Enqueue("a1", "ico-1");
        queue.Enqueue("a2", "ico-1");
        queue.Enqueue("b1", "ico-2");

        var claimed = await queue.ClaimAsync(10, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(["a1", "b1"], claimed.Select(item => item.Payload).ToArray());
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(1, claimed[0].Attempt);
    }

    [Fact]
    public async Task Complete_is_idempotent_and_drops_the_item()
    {
        var queue = NewQueue();
        queue.Enqueue("one");
        var item = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));

        await queue.CompleteAsync(item, CancellationToken.None);
        await queue.CompleteAsync(item, CancellationToken.None);

        Assert.Equal(0, queue.PendingCount);
        Assert.Empty(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    [Fact]
    public async Task Release_delay_hides_the_item_until_the_clock_advances()
    {
        var time = new ManualTime(Start);
        var queue = new InMemoryWorkQueue<string>(time, "probe");
        queue.Enqueue("one");
        var item = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));

        await queue.ReleaseAsync(item, TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.Equal(1, queue.PendingCount);
        Assert.Empty(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
        time.UtcNow += TimeSpan.FromMinutes(5);
        var again = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Equal(item.Id, again.Id);
        Assert.Equal(2, again.Attempt);
    }

    [Fact]
    public async Task Expired_lease_can_be_claimed_again_and_the_old_lease_cannot_complete()
    {
        var time = new ManualTime(Start);
        var queue = new InMemoryWorkQueue<string>(time, "probe");
        queue.Enqueue("one");
        var first = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
        time.UtcNow += TimeSpan.FromMinutes(2);

        var second = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.Attempt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.CompleteAsync(first, CancellationToken.None));
        await queue.CompleteAsync(second, CancellationToken.None);
        Assert.Equal(0, await queue.GetPendingCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fail_moves_the_item_to_the_dead_letter_list()
    {
        var queue = NewQueue();
        queue.Enqueue("one", "ico-1");
        var item = Assert.Single(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));

        await queue.FailAsync(item, "bad payload", CancellationToken.None);
        await queue.FailAsync(item, "bad payload", CancellationToken.None);

        var dead = Assert.Single(queue.DeadLetters);
        Assert.Equal(item.Id, dead.Id);
        Assert.Equal("bad payload", dead.Reason);
        Assert.Equal("ico-1", dead.PartitionKey);
        Assert.Equal(0, queue.PendingCount);
        Assert.Empty(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_item_is_rejected()
    {
        var queue = NewQueue();
        var ghost = new WorkItem<string>("missing", "x", 1, Start, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.CompleteAsync(ghost, CancellationToken.None));
    }

    private static InMemoryWorkQueue<string> NewQueue() => new(TimeProvider.System, "probe");

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
