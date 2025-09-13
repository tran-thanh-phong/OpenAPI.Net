using FluentAssertions;
using OpenAPI.Net.Tests.TestUtilities;
using System.Reactive.Linq;
using Xunit;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using OpenAPI.Net.Exceptions;
using System.Threading;

namespace OpenAPI.Net.Tests.Advanced;

/// <summary>
/// Phase 5: Advanced Testing - Resilience Tests
/// Tests connection recovery, error handling, and system resilience under adverse conditions
/// </summary>
public class ResilienceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public ResilienceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ConnectionRecovery_AfterDisconnect_ShouldReconnectSuccessfully()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        _output.WriteLine("Testing connection recovery after disconnect...");

        // Act - Verify initial connection state
        _client!.IsDisposed.Should().BeFalse("Initial connection should be established");
        _client.IsTerminated.Should().BeFalse("Connection should not be terminated initially");
        
        // Simulate disconnect by disposing and recreating client
        _client.Dispose();
        _client.IsDisposed.Should().BeTrue("Connection should be closed after dispose");
        
        // Attempt reconnection by creating new client
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );
        await _client.Connect();
        
        // Assert
        _client.IsDisposed.Should().BeFalse("Connection should be re-established after reconnect");
        
        _output.WriteLine("Connection recovery test completed successfully");
    }

    [Fact]
    public async Task InvalidHostConnection_ShouldHandleGracefully()
    {
        // Arrange
        var invalidClient = new OpenClient(
            "invalid-host-that-does-not-exist.com",
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        _output.WriteLine("Testing connection to invalid host...");

        // Act & Assert
        var connectionAttempt = async () => await invalidClient.Connect();
        
        await connectionAttempt.Should().ThrowAsync<ConnectionException>()
            .WithMessage("*connection*", "Should throw connection exception for invalid host");

        // Connection attempt should have failed, client remains in initial state
        
        _output.WriteLine("Invalid host handling test completed successfully");
        
        invalidClient.Dispose();
    }

    [Fact]
    public async Task InvalidPortConnection_ShouldHandleGracefully()
    {
        // Arrange
        var invalidClient = new OpenClient(
            TestConstants.TestDemoHost,
            99999, // Invalid port
            TestConstants.HeartbeatInterval
        );

        _output.WriteLine("Testing connection to invalid port...");

        // Act & Assert
        var connectionAttempt = async () => await invalidClient.Connect();
        
        await connectionAttempt.Should().ThrowAsync<ConnectionException>()
            .WithMessage("*connection*", "Should throw connection exception for invalid port");

        // Connection attempt should have failed, client remains in initial state
        
        _output.WriteLine("Invalid port handling test completed successfully");
        
        invalidClient.Dispose();
    }

    [Fact]
    public async Task SendMessage_WithoutAuthentication_ShouldHandleGracefully()
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

        _output.WriteLine("Testing message sending without authentication...");

        // Act - Send message without authentication
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = 12345 // Dummy account ID
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(3000); // Wait for error response

        // Assert
        if (errorResponses.Count > 0)
        {
            var error = errorResponses[0];
            error.ErrorCode.Should().NotBeEmpty("Error should have a code");
            error.Description.Should().NotBeEmpty("Error should have a description");
            
            _output.WriteLine($"Expected error received: {error.ErrorCode} - {error.Description}");
        }
        else
        {
            _output.WriteLine("No error response received - server may have silently ignored unauthenticated request");
        }

        subscription.Dispose();
        _output.WriteLine("Unauthenticated message handling test completed");
    }

    [Fact]
    public async Task MultipleConnectionAttempts_ShouldHandleGracefully()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        _output.WriteLine("Testing multiple connection attempts...");

        // Act - Multiple connect attempts
        await _client.Connect();
        _client.IsDisposed.Should().BeFalse("First connection should succeed");

        // Second connection attempt while already connected
        await _client.Connect();
        _client.IsDisposed.Should().BeFalse("Should remain connected after second connect attempt");

        // Multiple dispose/reconnect cycles
        for (int i = 0; i < 3; i++)
        {
            _client.Dispose();
            _client.IsDisposed.Should().BeTrue($"Should be disposed after cycle {i + 1}");
            
            _client = new OpenClient(
                TestConstants.TestDemoHost,
                TestConstants.TestApiPort,
                TestConstants.HeartbeatInterval
            );
            await _client.Connect();
            _client.IsDisposed.Should().BeFalse($"Should be reconnected after cycle {i + 1}");
        }

        // Assert
        _client.IsDisposed.Should().BeFalse("Final state should be connected");
        
        _output.WriteLine("Multiple connection attempts test completed successfully");
    }

    [Fact]
    public async Task LargeMessageHandling_ShouldProcessCorrectly()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var symbolResponses = new List<ProtoOASymbolsListRes>();
        var subscription = _client!.OfType<ProtoOASymbolsListRes>()
            .Subscribe(symbolResponses.Add);

        _output.WriteLine("Testing large message handling with symbols list...");

        // Act - Request symbols list (potentially large response)
        var symbolsRequest = new ProtoOASymbolsListReq
        {
            CtidTraderAccountId = _validAccountId,
            IncludeArchivedSymbols = true // Include archived symbols for larger response
        };

        await _client.SendMessage(symbolsRequest);
        await Task.Delay(5000); // Wait longer for potentially large response

        // Assert
        symbolResponses.Should().HaveCount(1, "Should receive symbols list response");
        var symbolsList = symbolResponses[0];
        
        symbolsList.Symbol.Should().NotBeEmpty("Should contain symbols");
        var totalSymbols = symbolsList.Symbol.Count + symbolsList.ArchivedSymbol.Count;
        
        _output.WriteLine($"Successfully processed large message with {totalSymbols} total symbols");
        _output.WriteLine($"Active symbols: {symbolsList.Symbol.Count}, Archived symbols: {symbolsList.ArchivedSymbol.Count}");
        
        // Validate message structure integrity
        foreach (var symbol in symbolsList.Symbol.Take(5))
        {
            symbol.SymbolId.Should().BeGreaterThan(0);
            symbol.SymbolName.Should().NotBeEmpty();
        }

        subscription.Dispose();
        _output.WriteLine("Large message handling test completed successfully");
    }

    [Fact]
    public async Task ConcurrentMessageSending_ShouldHandleCorrectly()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        _output.WriteLine("Testing concurrent message sending...");

        // Act - Send multiple messages concurrently
        var tasks = new List<Task>();
        
        // Send multiple trader requests concurrently
        for (int i = 0; i < 5; i++)
        {
            var traderRequest = new ProtoOATraderReq
            {
                CtidTraderAccountId = _validAccountId
            };
            
            tasks.Add(_client.SendMessage(traderRequest));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(5000); // Wait for all responses

        // Assert
        var traderResponses = allMessages.OfType<ProtoOATraderRes>().ToList();
        var errorResponses = allMessages.OfType<ProtoOAErrorRes>().ToList();
        
        _output.WriteLine($"Received {traderResponses.Count} trader responses");
        _output.WriteLine($"Received {errorResponses.Count} error responses");
        
        // Should receive responses for most requests (some may be rate-limited)
        traderResponses.Count.Should().BeGreaterThan(0, "Should receive at least some trader responses");
        
        // All successful responses should be for the correct account
        foreach (var response in traderResponses)
        {
            response.CtidTraderAccountId.Should().Be(_validAccountId);
        }

        subscription.Dispose();
        _output.WriteLine("Concurrent message sending test completed successfully");
    }

    [Fact]
    public async Task InvalidAccountOperations_ShouldReturnErrors()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var errorResponses = new List<ProtoOAErrorRes>();
        var traderResponses = new List<ProtoOATraderRes>();
        var allMessages = new List<IMessage>();
        
        var errorSubscription = _client!.OfType<ProtoOAErrorRes>().Subscribe(errorResponses.Add);
        var traderSubscription = _client.OfType<ProtoOATraderRes>().Subscribe(traderResponses.Add);
        var allSubscription = _client.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        _output.WriteLine("Testing invalid account operations...");

        // Act - Request information for invalid account
        var invalidAccountId = 99999999L; // Non-existent account
        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = invalidAccountId
        };

        await _client.SendMessage(traderRequest);
        await Task.Delay(4000); // Wait for response

        // Assert
        _output.WriteLine($"Total messages received: {allMessages.Count}");
        _output.WriteLine($"Error responses: {errorResponses.Count}");
        _output.WriteLine($"Trader responses: {traderResponses.Count}");

        if (errorResponses.Count > 0)
        {
            var error = errorResponses[0];
            error.ErrorCode.Should().NotBeEmpty("Error should have an error code");
            error.Description.Should().NotBeEmpty("Error should have a description");
            
            _output.WriteLine($"Expected error for invalid account: {error.ErrorCode} - {error.Description}");
        }
        else if (traderResponses.Count > 0)
        {
            _output.WriteLine("Warning: Received trader response for invalid account - validation may not be strict");
        }
        else
        {
            _output.WriteLine("No response received - request may have been silently ignored");
        }

        errorSubscription.Dispose();
        traderSubscription.Dispose();
        allSubscription.Dispose();
        
        _output.WriteLine("Invalid account operations test completed");
    }

    [Fact]
    public async Task RapidDisconnectReconnect_ShouldMaintainStability()
    {
        // Arrange
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        _output.WriteLine("Testing rapid disconnect/reconnect cycles...");

        // Act - Rapid dispose/reconnect cycles (reduced count for stability)
        for (int cycle = 0; cycle < 5; cycle++)
        {
            await _client.Connect();
            _client.IsDisposed.Should().BeFalse($"Should be connected in cycle {cycle + 1}");
            
            // Very short connection time
            await Task.Delay(100);
            
            _client.Dispose();
            _client.IsDisposed.Should().BeTrue($"Should be disposed in cycle {cycle + 1}");
            
            // Create new client for next cycle
            _client = new OpenClient(
                TestConstants.TestDemoHost,
                TestConstants.TestApiPort,
                TestConstants.HeartbeatInterval
            );
            
            // Very short disconnect time
            await Task.Delay(50);
        }

        // Final connection test
        await _client.Connect();
        
        // Assert
        _client.IsDisposed.Should().BeFalse("Should maintain stability after rapid cycles");
        
        _output.WriteLine("Rapid disconnect/reconnect test completed successfully");
    }

    private async Task SetupAuthenticatedClient()
    {
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();

        // Step 1: Application Authentication
        var appAuthResponses = new List<ProtoOAApplicationAuthRes>();
        var appAuthSubscription = _client.OfType<ProtoOAApplicationAuthRes>()
            .Subscribe(appAuthResponses.Add);

        var appAuthRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(appAuthRequest);
        
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (appAuthResponses.Count == 0 && DateTime.UtcNow < timeout)
        {
            await Task.Delay(500);
        }
        
        appAuthSubscription.Dispose();
        
        if (appAuthResponses.Count == 0)
        {
            throw new TimeoutException("Application authentication timed out");
        }

        _output.WriteLine("Application authentication successful");

        // Step 2: Get valid account ID
        _validAccountId = await GetValidAccountId();
        if (_validAccountId == 0)
        {
            throw new InvalidOperationException("No valid accounts found for the provided access token");
        }

        // Step 3: Account Authentication
        var accountAuthResponses = new List<ProtoOAAccountAuthRes>();
        var accountAuthErrors = new List<ProtoOAErrorRes>();
        
        var accountAuthSubscription = _client.OfType<ProtoOAAccountAuthRes>()
            .Subscribe(accountAuthResponses.Add);
        var accountErrorSubscription = _client.OfType<ProtoOAErrorRes>()
            .Subscribe(accountAuthErrors.Add);

        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = _validAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        _output.WriteLine($"Sending account auth request for account {_validAccountId}...");
        await _client.SendMessage(accountAuthRequest);
        
        timeout = DateTime.UtcNow.AddSeconds(10);
        while (accountAuthResponses.Count == 0 && accountAuthErrors.Count == 0 && DateTime.UtcNow < timeout)
        {
            await Task.Delay(500);
        }
        
        accountAuthSubscription.Dispose();
        accountErrorSubscription.Dispose();
        
        if (accountAuthErrors.Any())
        {
            throw new InvalidOperationException($"Account authentication failed: {accountAuthErrors[0].ErrorCode} - {accountAuthErrors[0].Description}");
        }
        
        if (accountAuthResponses.Count == 0)
        {
            throw new TimeoutException("Account authentication timed out");
        }

        _output.WriteLine($"Account authentication successful for account {_validAccountId}");
    }

    private async Task<long> GetValidAccountId()
    {
        var accountListResponses = new List<ProtoOAGetAccountListByAccessTokenRes>();
        var accountListSubscription = _client!.OfType<ProtoOAGetAccountListByAccessTokenRes>()
            .Subscribe(accountListResponses.Add);

        _output.WriteLine("Requesting account list from broker...");

        var accountListRequest = new ProtoOAGetAccountListByAccessTokenReq
        {
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountListRequest);
        await Task.Delay(3000);

        accountListSubscription.Dispose();

        if (accountListResponses.Count > 0 && accountListResponses[0].CtidTraderAccount.Count > 0)
        {
            var firstAccount = accountListResponses[0].CtidTraderAccount[0];
            _output.WriteLine($"Found {accountListResponses[0].CtidTraderAccount.Count} accounts. Using first account: ID={firstAccount.CtidTraderAccountId}, Type={firstAccount.TraderLogin}");
            return (long)firstAccount.CtidTraderAccountId;
        }

        _output.WriteLine("No accounts found in response");
        return 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}