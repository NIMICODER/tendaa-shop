using Tendaa.Application.Inbox;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Inbox;

public class InboundEventChannelTests
{
    [Fact]
    public void GetPartition_SameBsuid_AlwaysReturnsSamePartition()
    {
        const string bsuid = "US.13491208655302741918";

        var first = InboundEventChannel.GetPartition(bsuid);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(first, InboundEventChannel.GetPartition(bsuid));
        }
    }

    [Fact]
    public void TryEnqueue_ThreeEventsForSameBsuid_ComeOutInTheSameOrder()
    {
        var channel = new InboundEventChannel();
        const string bsuid = "US.13491208655302741918";

        var events = Enumerable.Range(1, 3)
            .Select(i => new InboundEvent
            {
                Id = Guid.NewGuid(),
                ShopId = Guid.NewGuid(),
                WhatsAppMessageId = $"wamid.{i}",
                Bsuid = bsuid,
                RawPayloadJson = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
            })
            .ToList();

        foreach (var inboundEvent in events)
        {
            Assert.True(channel.TryEnqueue(inboundEvent));
        }

        var reader = channel.Reader(InboundEventChannel.GetPartition(bsuid));
        var received = new List<InboundEvent>();
        while (reader.TryRead(out var item))
        {
            received.Add(item);
        }

        Assert.Equal(events.Select(e => e.WhatsAppMessageId), received.Select(e => e.WhatsAppMessageId));
    }
}
