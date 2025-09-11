using FluentAssertions;
using OpenAPI.Net.Tests.Utilities;
using Xunit;
using System;
using System.Threading.Tasks;

namespace OpenAPI.Net.Tests.Core;

/// <summary>
/// Simplified OpenClient tests to verify basic functionality
/// </summary>
public class SimpleOpenClientTests : IDisposable
{
    private OpenClient? _client;

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Assert
        _client.Should().NotBeNull();
        _client.Host.Should().Be(TestConstants.TestDemoHost);
        _client.Port.Should().Be(TestConstants.TestApiPort);
        _client.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullHost_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Action action = () => new OpenClient(null!, TestConstants.TestApiPort, TestConstants.HeartbeatInterval);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_InvalidPort_ShouldThrowArgumentOutOfRangeException(int invalidPort)
    {
        // Act & Assert
        Action action = () => new OpenClient(TestConstants.TestDemoHost, invalidPort, TestConstants.HeartbeatInterval);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Connect_ValidConnection_ShouldSucceed()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Act
        var connectTask = _client.Connect();

        // Assert
        await connectTask;
        _client.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldSetIsDisposedToTrue()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        // Act
        _client.Dispose();

        // Assert
        _client.IsDisposed.Should().BeTrue();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}