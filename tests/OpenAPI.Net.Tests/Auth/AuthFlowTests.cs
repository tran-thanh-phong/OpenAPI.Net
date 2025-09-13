using FluentAssertions;
using OpenAPI.Net.Tests.TestUtilities;
using System.Reactive.Linq;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;

namespace OpenAPI.Net.Tests.Auth;

/// <summary>
/// Tests for complete OAuth authentication flows
/// </summary>
public class AuthFlowTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;

    public AuthFlowTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ApplicationAuthFlow_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();
        
        var authResponses = new List<ProtoOAApplicationAuthRes>();
        var subscription = _client.OfType<ProtoOAApplicationAuthRes>()
            .Subscribe(authResponses.Add);

        _output.WriteLine("Testing application authentication flow...");

        // Act
        var authRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(authRequest);
        await Task.Delay(3000); // Wait for response

        // Assert
        authResponses.Should().HaveCount(1);
        _output.WriteLine("Application authentication flow completed successfully");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AccountAuthFlow_AfterApplicationAuth_ShouldSucceed()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();
        
        var appAuthResponses = new List<ProtoOAApplicationAuthRes>();
        var accountAuthResponses = new List<ProtoOAAccountAuthRes>();
        
        var appAuthSub = _client.OfType<ProtoOAApplicationAuthRes>()
            .Subscribe(appAuthResponses.Add);
        var accountAuthSub = _client.OfType<ProtoOAAccountAuthRes>()
            .Subscribe(accountAuthResponses.Add);

        _output.WriteLine("Testing complete authentication flow...");

        // Act - Step 1: Application Authentication
        var appAuthRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(appAuthRequest);
        await Task.Delay(2000);

        // Assert Step 1
        appAuthResponses.Should().HaveCount(1);
        _output.WriteLine("Application auth completed");

        // Act - Step 2: Account Authentication
        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountAuthRequest);
        await Task.Delay(2000);

        // Assert Step 2
        accountAuthResponses.Should().HaveCount(1);
        accountAuthResponses[0].CtidTraderAccountId.Should().Be(TestConstants.TestAccountId);
        _output.WriteLine($"Account auth completed for account {TestConstants.TestAccountId}");
        
        appAuthSub.Dispose();
        accountAuthSub.Dispose();
    }

    [Fact]
    public async Task AuthFlow_WithInvalidCredentials_ShouldReceiveError()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();
        
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing authentication with invalid credentials...");

        // Act
        var authRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = "invalid_client_id",
            ClientSecret = "invalid_client_secret"
        };

        await _client.SendMessage(authRequest);
        await Task.Delay(3000); // Wait for error response

        // Assert
        errorResponses.Should().NotBeEmpty();
        _output.WriteLine($"Received expected error response: {errorResponses[0].ErrorCode}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetAccountsList_AfterCompleteAuth_ShouldReturnAccounts()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();
        
        // Complete authentication first
        await PerformCompleteAuthentication();
        
        var accountResponses = new List<ProtoOAGetAccountListByAccessTokenRes>();
        var subscription = _client.OfType<ProtoOAGetAccountListByAccessTokenRes>()
            .Subscribe(accountResponses.Add);

        _output.WriteLine("Testing account list retrieval after authentication...");

        // Act
        var accountsRequest = new ProtoOAGetAccountListByAccessTokenReq
        {
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountsRequest);
        await Task.Delay(3000);

        // Assert
        accountResponses.Should().HaveCount(1);
        accountResponses[0].CtidTraderAccount.Should().NotBeEmpty();
        _output.WriteLine($"Retrieved {accountResponses[0].CtidTraderAccount.Count} accounts");
        
        subscription.Dispose();
    }

    [Fact]
    public void AuthFlow_MessageSequence_ShouldFollowCorrectOrder()
    {
        // Arrange
        var expectedSequence = new[]
        {
            typeof(ProtoOAApplicationAuthReq),
            typeof(ProtoOAApplicationAuthRes),
            typeof(ProtoOAAccountAuthReq),
            typeof(ProtoOAAccountAuthRes)
        };

        // Act & Assert
        for (int i = 0; i < expectedSequence.Length - 1; i++)
        {
            var currentType = expectedSequence[i];
            var nextType = expectedSequence[i + 1];
            
            // Verify that request types are followed by response types
            if (currentType.Name.EndsWith("Req"))
            {
                nextType.Name.Should().EndWith("Res");
                nextType.Name.Should().StartWith(currentType.Name.Replace("Req", ""));
            }
        }

        _output.WriteLine("Authentication message sequence validation passed");
    }

    private async Task PerformCompleteAuthentication()
    {
        var appAuthRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client!.SendMessage(appAuthRequest);
        await Task.Delay(2000);

        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountAuthRequest);
        await Task.Delay(2000);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}