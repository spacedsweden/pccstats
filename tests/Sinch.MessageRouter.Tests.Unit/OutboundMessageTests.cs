using FluentAssertions;
using Sinch.MessageRouter.Core;

namespace Sinch.MessageRouter.Tests.Unit;

public class OutboundMessageTests
{
    [Fact]
    public void OutboundMessage_ShouldInitializeProperties()
    {
        var message = new OutboundMessage
        {
            To = "+15551234567",
            From = "+15559876543",
            Body = "Hello, World!",
            Channel = ChannelType.Sms
        };

        message.To.Should().Be("+15551234567");
        message.From.Should().Be("+15559876543");
        message.Body.Should().Be("Hello, World!");
        message.Channel.Should().Be(ChannelType.Sms);
    }

    [Fact]
    public void MessageResult_ShouldInitializeWithRequiredProperties()
    {
        var result = new MessageResult
        {
            MessageId = "test-id-123",
            Status = MessageStatus.Queued
        };

        result.MessageId.Should().Be("test-id-123");
        result.Status.Should().Be(MessageStatus.Queued);
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(ChannelType.Sms)]
    [InlineData(ChannelType.Rcs)]
    [InlineData(ChannelType.WhatsApp)]
    [InlineData(ChannelType.Viber)]
    [InlineData(ChannelType.Messenger)]
    public void ChannelType_ShouldSupportAllChannels(ChannelType channel)
    {
        var message = new OutboundMessage
        {
            To = "+15551234567",
            From = "+15559876543",
            Body = "test",
            Channel = channel
        };

        message.Channel.Should().Be(channel);
    }
}
