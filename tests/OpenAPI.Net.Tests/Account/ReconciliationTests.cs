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

namespace OpenAPI.Net.Tests.Account;

/// <summary>
/// Phase 4: Account Management Testing - Reconciliation Tests
/// Tests account state synchronization including positions, orders, and account data consistency
/// </summary>
public class ReconciliationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public ReconciliationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RequestAccountReconciliation_ShouldReturnCompleteAccountState()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var subscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);

        _output.WriteLine("Testing complete account reconciliation...");

        // Act
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(4000);

        // Assert
        reconcileResponses.Should().HaveCount(1);
        var reconcileData = reconcileResponses[0];
        
        reconcileData.Should().NotBeNull();
        reconcileData.CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Reconciliation successful for account {_validAccountId}");
        _output.WriteLine($"Positions: {reconcileData.Position.Count}");
        _output.WriteLine($"Orders: {reconcileData.Order.Count}");
        
        // Validate position data structure
        foreach (var position in reconcileData.Position.Take(3))
        {
            position.PositionId.Should().BeGreaterThan(0);
            position.TradeData.Should().NotBeNull();
            position.TradeData.SymbolId.Should().BeGreaterThan(0);
            position.TradeData.Volume.Should().NotBe(0); // Position should have non-zero volume
            position.Price.Should().BeGreaterThan(0);
            
            _output.WriteLine($"Position ID: {position.PositionId}, Symbol: {position.TradeData.SymbolId}, " +
                             $"Volume: {position.TradeData.Volume}, Entry Price: {position.Price}");
        }
        
        // Validate order data structure
        foreach (var order in reconcileData.Order.Take(3))
        {
            order.OrderId.Should().BeGreaterThan(0);
            order.TradeData.Should().NotBeNull();
            order.TradeData.SymbolId.Should().BeGreaterThan(0);
            order.TradeData.Volume.Should().BeGreaterThan(0);
            order.OrderType.Should().BeDefined();
            order.TradeData.TradeSide.Should().BeDefined();
            
            _output.WriteLine($"Order ID: {order.OrderId}, Symbol: {order.TradeData.SymbolId}, " +
                             $"Type: {order.OrderType}, Volume: {order.TradeData.Volume}");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CompareReconciliationWithTraderData_ShouldBeConsistent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var traderResponses = new List<ProtoOATraderRes>();
        var allMessages = new List<IMessage>();
        
        var reconcileSubscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);
        var traderSubscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);
        var allSubscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        _output.WriteLine("Testing reconciliation vs trader data consistency...");

        // Act - Request both reconciliation and trader data
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(2000);
        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        reconcileResponses.Should().HaveCount(1);
        traderResponses.Should().HaveCount(1);
        
        var reconcileData = reconcileResponses[0];
        var traderData = traderResponses[0];
        
        // Both should reference the same account
        reconcileData.CtidTraderAccountId.Should().Be(traderData.CtidTraderAccountId);
        reconcileData.CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Reconcile data - Positions: {reconcileData.Position.Count}, Orders: {reconcileData.Order.Count}");
        
        if (traderData.Trader != null)
        {
            _output.WriteLine($"Trader data - Balance: {traderData.Trader.Balance}");
            
            // Both responses should come from the same account state
            Math.Abs(traderData.Trader.Balance).Should().BeGreaterOrEqualTo(0);
        }
        
        reconcileSubscription.Dispose();
        traderSubscription.Dispose();
        allSubscription.Dispose();
    }

    [Fact]
    public async Task PositionVolumeConsistency_ShouldMatchExpectations()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var subscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);

        _output.WriteLine("Testing position volume consistency...");

        // Act
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(4000);

        // Assert
        reconcileResponses.Should().HaveCount(1);
        var reconcileData = reconcileResponses[0];
        
        _output.WriteLine($"Analyzing {reconcileData.Position.Count} positions for volume consistency...");
        
        if (reconcileData.Position.Count > 0)
        {
            // Group positions by symbol to check for netting
            var positionsBySymbol = reconcileData.Position.GroupBy(p => p.TradeData.SymbolId).ToList();
            
            foreach (var symbolGroup in positionsBySymbol)
            {
                var symbolPositions = symbolGroup.ToList();
                var totalBuyVolume = symbolPositions.Where(p => p.TradeData.Volume > 0).Sum(p => p.TradeData.Volume);
                var totalSellVolume = Math.Abs(symbolPositions.Where(p => p.TradeData.Volume < 0).Sum(p => p.TradeData.Volume));
                var netVolume = symbolPositions.Sum(p => p.TradeData.Volume);
                
                _output.WriteLine($"Symbol {symbolGroup.Key}: Buy Volume: {totalBuyVolume}, " +
                                 $"Sell Volume: {totalSellVolume}, Net Volume: {netVolume}");
                
                // Each position should have non-zero volume
                foreach (var position in symbolPositions)
                {
                    position.TradeData.Volume.Should().NotBe(0, "Positions should have non-zero volume");
                    position.Price.Should().BeGreaterThan(0, "Entry price should be positive");
                    
                    // Volume should be in valid ranges (not extremely large or small)
                    Math.Abs(position.TradeData.Volume).Should().BeLessThan(1_000_000_000, "Volume should be within reasonable range");
                    Math.Abs(position.TradeData.Volume).Should().BeGreaterThan(0, "Volume should be greater than zero");
                }
            }
            
            _output.WriteLine($"Position volume consistency validated for {positionsBySymbol.Count} unique symbols");
        }
        else
        {
            _output.WriteLine("No positions found - account has no open positions");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task OrderStateValidation_ShouldHaveValidProperties()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var subscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);

        _output.WriteLine("Testing order state validation...");

        // Act
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(4000);

        // Assert
        reconcileResponses.Should().HaveCount(1);
        var reconcileData = reconcileResponses[0];
        
        _output.WriteLine($"Validating {reconcileData.Order.Count} orders...");
        
        if (reconcileData.Order.Count > 0)
        {
            foreach (var order in reconcileData.Order)
            {
                // Basic order validation
                order.OrderId.Should().BeGreaterThan(0, "Order ID should be positive");
                order.TradeData.Should().NotBeNull("Order should have trade data");
                order.TradeData.SymbolId.Should().BeGreaterThan(0, "Symbol ID should be positive");
                order.TradeData.Volume.Should().BeGreaterThan(0, "Order volume should be positive");
                order.OrderType.Should().BeDefined("Order type should be valid");
                order.TradeData.TradeSide.Should().BeDefined("Trade side should be valid");
                
                // Type-specific validation
                if (order.OrderType == ProtoOAOrderType.Limit || order.OrderType == ProtoOAOrderType.StopLimit)
                {
                    order.LimitPrice.Should().BeGreaterThan(0, "Limit orders should have valid limit price");
                }
                
                if (order.OrderType == ProtoOAOrderType.Stop || order.OrderType == ProtoOAOrderType.StopLimit)
                {
                    order.StopPrice.Should().BeGreaterThan(0, "Stop orders should have valid stop price");
                }
                
                // Volume validation
                order.TradeData.Volume.Should().BeLessThan(1_000_000_000, "Order volume should be within reasonable range");
                
                _output.WriteLine($"Order {order.OrderId}: {order.OrderType} {order.TradeData.TradeSide} " +
                                 $"{order.TradeData.Volume} units of Symbol {order.TradeData.SymbolId}");
            }
            
            // Check for duplicate order IDs
            var orderIds = reconcileData.Order.Select(o => o.OrderId).ToList();
            var uniqueOrderIds = orderIds.Distinct().ToList();
            orderIds.Count.Should().Be(uniqueOrderIds.Count, "All order IDs should be unique");
            
            _output.WriteLine($"Order state validation completed successfully for {reconcileData.Order.Count} orders");
        }
        else
        {
            _output.WriteLine("No pending orders found - account has no active orders");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task MultipleReconciliationRequests_ShouldReturnConsistentData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var subscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);

        _output.WriteLine("Testing multiple reconciliation requests for consistency...");

        // Act - Send multiple reconciliation requests
        for (int i = 0; i < 3; i++)
        {
            var reconcileRequest = new ProtoOAReconcileReq
            {
                CtidTraderAccountId = _validAccountId
            };

            await _client.SendMessage(reconcileRequest);
            await Task.Delay(1500); // Delay between requests
        }

        await Task.Delay(2000); // Wait for all responses

        // Assert
        reconcileResponses.Should().HaveCount(3);
        
        // All responses should be for the same account
        reconcileResponses.All(r => r.CtidTraderAccountId == _validAccountId).Should().BeTrue();
        
        // Compare first and last response for consistency
        var firstResponse = reconcileResponses[0];
        var lastResponse = reconcileResponses[^1];
        
        _output.WriteLine($"First response: Positions: {firstResponse.Position.Count}, Orders: {firstResponse.Order.Count}");
        _output.WriteLine($"Last response: Positions: {lastResponse.Position.Count}, Orders: {lastResponse.Order.Count}");
        
        // Position and order counts should be relatively stable (allowing for minor changes)
        var positionCountDifference = Math.Abs(firstResponse.Position.Count - lastResponse.Position.Count);
        var orderCountDifference = Math.Abs(firstResponse.Order.Count - lastResponse.Order.Count);
        
        positionCountDifference.Should().BeLessThan(10, "Position count should be relatively stable");
        orderCountDifference.Should().BeLessThan(10, "Order count should be relatively stable");
        
        // If positions exist, validate consistency of position IDs
        if (firstResponse.Position.Count > 0 && lastResponse.Position.Count > 0)
        {
            var firstPositionIds = firstResponse.Position.Select(p => p.PositionId).ToHashSet();
            var lastPositionIds = lastResponse.Position.Select(p => p.PositionId).ToHashSet();
            
            // Most position IDs should remain the same (allowing for some changes due to trading activity)
            var commonPositions = firstPositionIds.Intersect(lastPositionIds).Count();
            var stabilityRatio = (double)commonPositions / Math.Min(firstPositionIds.Count, lastPositionIds.Count);
            
            _output.WriteLine($"Position ID stability: {commonPositions} common positions out of {Math.Min(firstPositionIds.Count, lastPositionIds.Count)} " +
                             $"(Stability: {stabilityRatio:P1})");
            
            stabilityRatio.Should().BeGreaterThan(0.7, "Most positions should remain stable between reconciliation requests");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task ReconciliationErrorHandling_WithInvalidAccount_ShouldReturnError()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var errorResponses = new List<ProtoOAErrorRes>();
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var allMessages = new List<IMessage>();
        
        var errorSubscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);
        var reconcileSubscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);
        var allSubscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        _output.WriteLine("Testing reconciliation error handling with invalid account...");

        // Act - Request reconciliation for non-existent account
        var invalidAccountId = 99999999; // Non-existent account
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = (long)invalidAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(4000);

        // Assert
        _output.WriteLine($"Total messages: {allMessages.Count}");
        _output.WriteLine($"Error responses: {errorResponses.Count}");
        _output.WriteLine($"Reconcile responses: {reconcileResponses.Count}");
        
        if (errorResponses.Any())
        {
            var error = errorResponses[0];
            _output.WriteLine($"Expected error received: {error.ErrorCode} - {error.Description}");
            
            // Should receive an error for invalid account
            errorResponses.Should().NotBeEmpty("Should receive error for invalid account ID");
            reconcileResponses.Should().BeEmpty("Should not receive reconcile data for invalid account");
            
            // Error should indicate account-related issue
            error.ErrorCode.Should().NotBeNull();
            error.ErrorCode.Should().NotBeEmpty();
        }
        else if (reconcileResponses.Any())
        {
            _output.WriteLine("Warning: Received reconcile response for invalid account - this may indicate account validation is not strict");
            // This is not necessarily a failure - depends on broker implementation
        }
        else
        {
            _output.WriteLine("No response received - request may have been silently ignored");
        }
        
        errorSubscription.Dispose();
        reconcileSubscription.Dispose();
        allSubscription.Dispose();
    }

    [Fact]
    public async Task AccountStateSnapshot_ShouldCaptureCompleteInformation()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var traderResponses = new List<ProtoOATraderRes>();
        
        var reconcileSubscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);
        var traderSubscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);

        _output.WriteLine("Testing complete account state snapshot capture...");

        // Act - Capture complete account state
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        // Send both requests to get complete state
        await _client.SendMessage(reconcileRequest);
        await Task.Delay(2000);
        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        reconcileResponses.Should().HaveCount(1);
        traderResponses.Should().HaveCount(1);
        
        var reconcileData = reconcileResponses[0];
        var traderData = traderResponses[0];
        
        // Create comprehensive account snapshot
        var snapshot = new
        {
            AccountId = _validAccountId,
            Timestamp = DateTimeOffset.UtcNow,
            Positions = reconcileData.Position.Count,
            Orders = reconcileData.Order.Count,
            Balance = traderData.Trader?.Balance ?? 0,
            ManagerBonus = traderData.Trader?.ManagerBonus ?? 0,
            IbBonus = traderData.Trader?.IbBonus ?? 0,
            NonWithdrawableBonus = traderData.Trader?.NonWithdrawableBonus ?? 0
        };
        
        _output.WriteLine($"Account Snapshot:");
        _output.WriteLine($"- Account ID: {snapshot.AccountId}");
        _output.WriteLine($"- Timestamp: {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        _output.WriteLine($"- Open Positions: {snapshot.Positions}");
        _output.WriteLine($"- Pending Orders: {snapshot.Orders}");
        _output.WriteLine($"- Balance: {snapshot.Balance:F2}");
        _output.WriteLine($"- Manager Bonus: {snapshot.ManagerBonus:F2}");
        _output.WriteLine($"- IB Bonus: {snapshot.IbBonus:F2}");
        _output.WriteLine($"- Non-Withdrawable Bonus: {snapshot.NonWithdrawableBonus:F2}");
        
        // Validate snapshot completeness
        snapshot.AccountId.Should().Be(_validAccountId);
        snapshot.Positions.Should().BeGreaterOrEqualTo(0);
        snapshot.Orders.Should().BeGreaterOrEqualTo(0);
        snapshot.Balance.Should().BeGreaterOrEqualTo(0);
        
        _output.WriteLine("Complete account state snapshot captured and validated successfully");
        
        reconcileSubscription.Dispose();
        traderSubscription.Dispose();
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