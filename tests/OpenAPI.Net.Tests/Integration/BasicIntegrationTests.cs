using FluentAssertions;
using OpenAPI.Net.Tests.TestUtilities;
using System.Reactive.Linq;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;

namespace OpenAPI.Net.Tests.Integration;

/// <summary>
/// Basic integration tests for Phase 1
/// </summary>
public class BasicIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;

    public BasicIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Connect_ToRealDemoAPI_ShouldSucceed()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        _output.WriteLine($"Connecting to {TestConstants.TestDemoHost}:{TestConstants.TestApiPort}");

        // Act
        await _client.Connect();

        // Assert
        _client.IsDisposed.Should().BeFalse();
        _output.WriteLine("Successfully connected to demo API");
    }

    [Fact]
    public async Task ApplicationAuth_WithRealCredentials_ShouldReceiveResponse()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();
        
        var responses = new List<IMessage>();
        var subscription = _client.Subscribe(new TestObserver(responses.Add));

        _output.WriteLine("Sending application authorization request...");

        // Act
        var authRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(authRequest);
        await Task.Delay(3000); // Wait for response

        // Assert
        responses.Should().NotBeEmpty();
        _output.WriteLine($"Received {responses.Count} messages");
        
        subscription.Dispose();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// Simple test observer for integration tests
/// </summary>
public class TestObserver : IObserver<IMessage>
{
    private readonly Action<IMessage> _onNext;

    public TestObserver(Action<IMessage> onNext)
    {
        _onNext = onNext ?? throw new ArgumentNullException(nameof(onNext));
    }

    public void OnNext(IMessage value) => _onNext(value);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}