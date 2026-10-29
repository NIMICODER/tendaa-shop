using System.Text;
using System.Threading.Channels;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Inbox;

/// <summary>Partitioned queue for inbound WhatsApp events — ARCHITECTURE.md's "Inbox table +
/// Channel partitions" decision. Registered as a singleton; the webhook controller writes to it,
/// a background worker per partition reads from it. The same BSUID always lands on the same
/// partition/worker, so one customer's messages process strictly in order while different
/// customers run in parallel.</summary>
public sealed class InboundEventChannel
{
    public const int PartitionCount = 4;

    private readonly Channel<InboundEvent>[] _partitions =
        Enumerable.Range(0, PartitionCount).Select(_ => Channel.CreateUnbounded<InboundEvent>()).ToArray();

    public bool TryEnqueue(InboundEvent inboundEvent) =>
        _partitions[GetPartition(inboundEvent.Bsuid)].Writer.TryWrite(inboundEvent);

    public ChannelReader<InboundEvent> Reader(int partition) => _partitions[partition].Reader;

    /// <summary>A stable hash, not <see cref="string.GetHashCode()"/> — that's randomized per
    /// process in modern .NET, which would scatter one customer's messages across different
    /// partitions on every restart.</summary>
    public static int GetPartition(string bsuid) => (int)(Fnv1aHash(bsuid) % PartitionCount);

    private static uint Fnv1aHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * prime;
        }

        return hash;
    }
}
