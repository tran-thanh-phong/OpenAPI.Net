using FluentAssertions;
using OpenAPI.Net.Exceptions;
using OpenAPI.Net.Tests.TestUtilities;
using Xunit;
using System;
using System.Threading.Tasks;

namespace OpenAPI.Net.Tests.Core;

/// <summary>
/// Tests for OpenClient connection management functionality
/// </summary>
public class ConnectionTests : IDisposable
{
    private OpenClient? _client;

    [Fact]
    public async Task Connect_ToValidHost_ShouldEstablishConnection()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Act
        await _client.Connect();

        // Assert
        _client.IsDisposed.Should().BeFalse();
        _client.IsTerminated.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_WithWebSocket_ShouldEstablishConnection()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval,
            40, // maxRequestPerSecond
            true // useWebSocket
        );

        // Act
        await _client.Connect();

        // Assert
        _client.IsUsingWebSocket.Should().BeTrue();
        _client.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_ToInvalidHost_ShouldThrowConnectionException()
    {
        // Arrange
        _client = new OpenClient(
            "invalid.host.name",
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConnectionException>(() => _client.Connect());
        exception.Should().NotBeNull();
        exception.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task Connect_WithInvalidPort_ShouldThrowConnectionException()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            9999, // Invalid port
            TestConstants.HeartbeatInterval
        );

        // Act & Assert
        await Assert.ThrowsAsync<ConnectionException>(() => _client.Connect());
    }

    [Fact]
    public async Task Connect_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );
        
        _client.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _client.Connect());
    }

    [Fact]
    public async Task Disconnect_AfterConnect_ShouldDisposeCleanly()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();

        // Act
        _client.Dispose();

        // Assert
        _client.IsDisposed.Should().BeTrue();
        _client.MessagesQueueCount.Should().Be(0);
    }

    [Fact]
    public void MaxRequestPerSecond_CustomValue_ShouldBeSet()
    {
        // Arrange
        const int customMaxRequests = 20;

        // Act
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval,
            customMaxRequests
        );

        // Assert
        _client.MaxRequestPerSecond.Should().Be(customMaxRequests);
    }

    [Fact]
    public void Properties_InitialState_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Assert
        _client.Host.Should().Be(TestConstants.TestDemoHost);
        _client.Port.Should().Be(TestConstants.TestApiPort);
        _client.IsDisposed.Should().BeFalse();
        _client.IsCompleted.Should().BeFalse();
        _client.IsTerminated.Should().BeFalse();
        _client.IsUsingWebSocket.Should().BeFalse(); // Default is TCP
        _client.LastSentMessageTime.Should().Be(default(DateTimeOffset));
        _client.MessagesQueueCount.Should().Be(0);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}