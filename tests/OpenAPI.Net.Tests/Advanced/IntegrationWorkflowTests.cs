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
using System.Diagnostics;

namespace OpenAPI.Net.Tests.Advanced;

/// <summary>
/// Phase 5: Advanced Testing - Integration Workflow Tests
/// Tests complete end-to-end workflows combining authentication, market data, and trading operations
/// </summary>
public class IntegrationWorkflowTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;
    private long _validSymbolId;

    public IntegrationWorkflowTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CompleteMarketDataToTradingWorkflow_ShouldExecuteSuccessfully()
    {
        // Arrange
        _output.WriteLine("Testing complete market data to trading integration workflow...");
        var stopwatch = Stopwatch.StartNew();

        // Step 1: Authentication and Setup
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        // Step 2: Subscribe to Market Data
        _output.WriteLine($"Step 2: Subscribing to market data for symbol {_validSymbolId}...");
        
        var subscribeRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        subscribeRequest.SymbolId.Add(_validSymbolId);

        await _client.SendMessage(subscribeRequest);
        await Task.Delay(3000); // Wait for subscription confirmation

        // Step 3: Wait for Market Data
        _output.WriteLine("Step 3: Waiting for market data...");
        var spotEvents = allMessages.OfType<ProtoOASpotEvent>()
            .Where(s => s.SymbolId == _validSymbolId)
            .ToList();

        var attempts = 0;
        while (spotEvents.Count == 0 && attempts < 10)
        {
            await Task.Delay(2000);
            spotEvents = allMessages.OfType<ProtoOASpotEvent>()
                .Where(s => s.SymbolId == _validSymbolId)
                .ToList();
            attempts++;
            _output.WriteLine($"Attempt {attempts}: Found {spotEvents.Count} spot events");
        }

        if (spotEvents.Count > 0)
        {
            var latestSpot = spotEvents.OrderByDescending(s => s.Timestamp).First();
            _output.WriteLine($"Latest market data - Bid: {latestSpot.Bid}, Ask: {latestSpot.Ask}");

            // Step 4: Place Market Order Based on Market Data
            _output.WriteLine("Step 4: Placing market order based on received market data...");
            
            var orderRequest = new ProtoOANewOrderReq
            {
                CtidTraderAccountId = _validAccountId,
                SymbolId = _validSymbolId,
                OrderType = ProtoOAOrderType.Market,
                TradeSide = ProtoOATradeSide.Buy,
                Volume = 1000, // Minimum volume
                Comment = "Integration test order"
            };

            await _client.SendMessage(orderRequest);
            await Task.Delay(5000); // Wait for order execution

            // Step 5: Verify Order Execution
            _output.WriteLine("Step 5: Verifying order execution...");
            var executionEvents = allMessages.OfType<ProtoOAExecutionEvent>().ToList();
            
            _output.WriteLine($"Received {executionEvents.Count} execution events");
            
            if (executionEvents.Count > 0)
            {
                var orderExecution = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderAccepted);
                if (orderExecution != null)
                {
                    _output.WriteLine($"Order accepted: {orderExecution.Order?.OrderId}");
                }

                var fillExecution = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
                if (fillExecution != null)
                {
                    _output.WriteLine($"Order filled: {fillExecution.Deal?.DealId}");
                }
            }

            // Step 6: Get Updated Account Information
            _output.WriteLine("Step 6: Retrieving updated account information...");
            var reconcileRequest = new ProtoOAReconcileReq
            {
                CtidTraderAccountId = _validAccountId
            };

            await _client.SendMessage(reconcileRequest);
            await Task.Delay(3000);

            var reconcileResponses = allMessages.OfType<ProtoOAReconcileRes>().ToList();
            if (reconcileResponses.Count > 0)
            {
                var accountState = reconcileResponses.Last();
                _output.WriteLine($"Updated account state - Positions: {accountState.Position.Count}, Orders: {accountState.Order.Count}");
            }
        }
        else
        {
            _output.WriteLine("No market data received - market may be closed");
        }

        // Cleanup
        var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        unsubscribeRequest.SymbolId.Add(_validSymbolId);
        await _client.SendMessage(unsubscribeRequest);

        stopwatch.Stop();
        subscription.Dispose();

        // Assert
        _output.WriteLine($"Complete workflow executed in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(60, "Complete workflow should execute within reasonable time");
        
        // Should have some market activity or acknowledgments
        allMessages.Count.Should().BeGreaterThan(5, "Should have received multiple messages during workflow");
        
        _output.WriteLine("Complete market data to trading workflow test completed");
    }

    [Fact]
    public async Task MultiAccountOperationsWorkflow_ShouldHandleCorrectly()
    {
        // Arrange
        _output.WriteLine("Testing multi-account operations workflow...");
        
        await SetupAuthenticatedClient();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        // Step 1: Discover All Available Accounts
        _output.WriteLine("Step 1: Discovering all available accounts...");
        
        var accountListRequest = new ProtoOAGetAccountListByAccessTokenReq
        {
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountListRequest);
        await Task.Delay(3000);

        var accountListResponses = allMessages.OfType<ProtoOAGetAccountListByAccessTokenRes>().ToList();
        accountListResponses.Should().HaveCount(1, "Should receive account list response");

        var availableAccounts = accountListResponses[0].CtidTraderAccount.ToList();
        _output.WriteLine($"Found {availableAccounts.Count} available accounts");

        foreach (var account in availableAccounts)
        {
            _output.WriteLine($"Account: {account.CtidTraderAccountId} (Login: {account.TraderLogin})");
        }

        // Step 2: Authenticate and Test Each Account
        _output.WriteLine("Step 2: Testing operations on each available account...");
        
        var successfulAccounts = 0;
        foreach (var account in availableAccounts.Take(3)) // Limit to first 3 accounts
        {
            try
            {
                // Authenticate to this account
                var accountAuthRequest = new ProtoOAAccountAuthReq
                {
                    CtidTraderAccountId = (long)account.CtidTraderAccountId,
                    AccessToken = TestConstants.TestAccessToken
                };

                await _client.SendMessage(accountAuthRequest);
                await Task.Delay(2000);

                // Test basic operations on this account
                var traderRequest = new ProtoOATraderReq
                {
                    CtidTraderAccountId = (long)account.CtidTraderAccountId
                };

                await _client.SendMessage(traderRequest);
                await Task.Delay(2000);

                var traderResponses = allMessages.OfType<ProtoOATraderRes>()
                    .Where(r => r.CtidTraderAccountId == (long)account.CtidTraderAccountId)
                    .ToList();

                if (traderResponses.Count > 0)
                {
                    var traderInfo = traderResponses.Last();
                    _output.WriteLine($"Account {account.CtidTraderAccountId}: Balance available, operations successful");
                    successfulAccounts++;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Account {account.CtidTraderAccountId}: Operations failed - {ex.Message}");
            }
        }

        subscription.Dispose();

        // Assert
        availableAccounts.Count.Should().BeGreaterThan(0, "Should have at least one available account");
        successfulAccounts.Should().BeGreaterThan(0, "Should successfully operate on at least one account");
        
        _output.WriteLine($"Multi-account workflow completed: {successfulAccounts}/{availableAccounts.Count} accounts successful");
    }

    [Fact]
    public async Task MarketDataAggregationWorkflow_ShouldCollectAndAnalyze()
    {
        // Arrange
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        _output.WriteLine("Testing market data aggregation and analysis workflow...");
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        // Step 1: Get Historical Data
        _output.WriteLine("Step 1: Retrieving historical trendbar data...");
        
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeMilliseconds(); // Last 24 hours

        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = _validSymbolId,
            Period = ProtoOATrendbarPeriod.M5, // 5-minute bars
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            Count = 288 // 24 hours * 12 (5-min periods per hour)
        };

        await _client.SendMessage(trendbarRequest);
        await Task.Delay(5000);

        // Step 2: Get Current Market Data
        _output.WriteLine("Step 2: Subscribing to real-time market data...");
        
        var subscribeRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        subscribeRequest.SymbolId.Add(_validSymbolId);

        await _client.SendMessage(subscribeRequest);
        await Task.Delay(5000); // Collect real-time data

        // Step 3: Analyze Collected Data
        _output.WriteLine("Step 3: Analyzing collected market data...");
        
        var trendbarResponses = allMessages.OfType<ProtoOAGetTrendbarsRes>().ToList();
        var spotEvents = allMessages.OfType<ProtoOASpotEvent>()
            .Where(s => s.SymbolId == _validSymbolId)
            .ToList();

        if (trendbarResponses.Count > 0)
        {
            var trendbars = trendbarResponses[0].Trendbar.ToList();
            _output.WriteLine($"Retrieved {trendbars.Count} historical trendbars");

            if (trendbars.Count > 0)
            {
                // Calculate basic statistics
                var volumes = trendbars.Select(t => (double)t.Volume).ToList();
                var averageVolume = volumes.Average();
                var maxVolume = volumes.Max();
                var minVolume = volumes.Min();

                _output.WriteLine($"Volume analysis - Avg: {averageVolume:F0}, Max: {maxVolume:F0}, Min: {minVolume:F0}");
                
                // Validate data quality
                trendbars.All(t => t.Volume >= 0).Should().BeTrue("All volumes should be non-negative");
            }
        }

        if (spotEvents.Count > 0)
        {
            _output.WriteLine($"Received {spotEvents.Count} real-time spot events");
            
            var latestSpot = spotEvents.OrderByDescending(s => s.Timestamp).First();
            _output.WriteLine($"Latest price - Bid: {latestSpot.Bid}, Ask: {latestSpot.Ask}, Spread: {latestSpot.Ask - latestSpot.Bid}");
            
            // Validate real-time data
            spotEvents.All(s => s.Bid > 0 && s.Ask > 0 && s.Ask >= s.Bid).Should().BeTrue("All spot prices should be valid");
        }

        // Cleanup
        var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        unsubscribeRequest.SymbolId.Add(_validSymbolId);
        await _client.SendMessage(unsubscribeRequest);

        subscription.Dispose();

        // Assert
        var totalDataPoints = (trendbarResponses.Sum(r => r.Trendbar.Count)) + spotEvents.Count;
        _output.WriteLine($"Total data points collected and analyzed: {totalDataPoints}");
        
        totalDataPoints.Should().BeGreaterThan(0, "Should collect and analyze market data");
        
        _output.WriteLine("Market data aggregation workflow test completed");
    }

    [Fact]
    public async Task ErrorRecoveryWorkflow_ShouldRecoverGracefully()
    {
        // Arrange
        _output.WriteLine("Testing error recovery workflow...");
        
        await SetupAuthenticatedClient();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        // Step 1: Generate Known Error Condition
        _output.WriteLine("Step 1: Generating known error condition...");
        
        var invalidOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = 999999, // Invalid symbol ID
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = -1000, // Invalid volume
            Comment = "Error recovery test"
        };

        await _client.SendMessage(invalidOrderRequest);
        await Task.Delay(3000);

        // Step 2: Handle Error Response
        _output.WriteLine("Step 2: Handling error response...");
        
        var errorResponses = allMessages.OfType<ProtoOAErrorRes>().ToList();
        var executionEvents = allMessages.OfType<ProtoOAExecutionEvent>()
            .Where(e => e.ExecutionType == ProtoOAExecutionType.OrderRejected)
            .ToList();

        if (errorResponses.Count > 0 || executionEvents.Count > 0)
        {
            _output.WriteLine($"Errors detected: {errorResponses.Count} error responses, {executionEvents.Count} rejections");
            
            if (errorResponses.Count > 0)
            {
                foreach (var error in errorResponses)
                {
                    _output.WriteLine($"Error: {error.ErrorCode} - {error.Description}");
                }
            }
        }

        // Step 3: Recover with Valid Operations
        _output.WriteLine("Step 3: Recovering with valid operations...");
        
        // Valid operation after error
        var validTraderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(validTraderRequest);
        await Task.Delay(3000);

        var traderResponses = allMessages.OfType<ProtoOATraderRes>().ToList();
        
        // Step 4: Verify System Recovery
        _output.WriteLine("Step 4: Verifying system recovery...");
        
        if (traderResponses.Count > 0)
        {
            var latestTraderResponse = traderResponses.Last();
            _output.WriteLine("System recovered successfully - valid operations working");
            
            latestTraderResponse.CtidTraderAccountId.Should().Be(_validAccountId);
        }

        subscription.Dispose();

        // Assert
        _client!.IsDisposed.Should().BeFalse("Connection should remain stable after errors");
        traderResponses.Count.Should().BeGreaterThan(0, "Should successfully execute valid operations after errors");
        
        _output.WriteLine("Error recovery workflow test completed");
    }

    [Fact]
    public async Task ComprehensiveSystemWorkflow_AllOperations_ShouldIntegrateSeamlessly()
    {
        // Arrange
        _output.WriteLine("Testing comprehensive system workflow with all operations...");
        var stopwatch = Stopwatch.StartNew();
        
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        var workflowSteps = new List<string>();

        // Step 1: Account Information
        workflowSteps.Add("Account Information");
        var traderRequest = new ProtoOATraderReq { CtidTraderAccountId = _validAccountId };
        await _client.SendMessage(traderRequest);
        await Task.Delay(2000);

        // Step 2: Symbol Information
        workflowSteps.Add("Symbol Information");
        var symbolRequest = new ProtoOASymbolByIdReq 
        { 
            CtidTraderAccountId = _validAccountId
        };
        symbolRequest.SymbolId.Add(_validSymbolId);
        await _client.SendMessage(symbolRequest);
        await Task.Delay(2000);

        // Step 3: Historical Data
        workflowSteps.Add("Historical Data");
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();
        
        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = _validSymbolId,
            Period = ProtoOATrendbarPeriod.M1,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            Count = 60
        };
        await _client.SendMessage(trendbarRequest);
        await Task.Delay(3000);

        // Step 4: Market Data Subscription
        workflowSteps.Add("Market Data Subscription");
        var subscribeRequest = new ProtoOASubscribeSpotsReq { CtidTraderAccountId = _validAccountId };
        subscribeRequest.SymbolId.Add(_validSymbolId);
        await _client.SendMessage(subscribeRequest);
        await Task.Delay(3000);

        // Step 5: Account Reconciliation
        workflowSteps.Add("Account Reconciliation");
        var reconcileRequest = new ProtoOAReconcileReq { CtidTraderAccountId = _validAccountId };
        await _client.SendMessage(reconcileRequest);
        await Task.Delay(2000);

        // Step 6: Cleanup
        workflowSteps.Add("Cleanup");
        var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq { CtidTraderAccountId = _validAccountId };
        unsubscribeRequest.SymbolId.Add(_validSymbolId);
        await _client.SendMessage(unsubscribeRequest);
        await Task.Delay(1000);

        stopwatch.Stop();
        subscription.Dispose();

        // Assert - Analyze Results
        _output.WriteLine($"Comprehensive workflow completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
        _output.WriteLine($"Workflow steps executed: {string.Join(" → ", workflowSteps)}");
        _output.WriteLine($"Total messages received: {allMessages.Count}");

        // Validate each operation type
        var traderResponses = allMessages.OfType<ProtoOATraderRes>().Count();
        var symbolResponses = allMessages.OfType<ProtoOASymbolByIdRes>().Count();
        var trendbarResponses = allMessages.OfType<ProtoOAGetTrendbarsRes>().Count();
        var subscribeResponses = allMessages.OfType<ProtoOASubscribeSpotsRes>().Count();
        var reconcileResponses = allMessages.OfType<ProtoOAReconcileRes>().Count();
        var spotEvents = allMessages.OfType<ProtoOASpotEvent>().Count();

        _output.WriteLine($"Response breakdown:");
        _output.WriteLine($"  Trader: {traderResponses}, Symbol: {symbolResponses}, Trendbar: {trendbarResponses}");
        _output.WriteLine($"  Subscribe: {subscribeResponses}, Reconcile: {reconcileResponses}, Spot Events: {spotEvents}");

        // Performance and reliability assertions
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "Comprehensive workflow should complete efficiently");
        allMessages.Count.Should().BeGreaterThan(5, "Should receive responses from multiple operations");
        
        // Should receive at least some core responses
        var coreResponses = traderResponses + symbolResponses + reconcileResponses;
        coreResponses.Should().BeGreaterThan(0, "Should receive core operation responses");

        _output.WriteLine("Comprehensive system workflow test completed successfully");
    }

    private async Task SetupAuthenticatedClient()
    {
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();

        // Application Authentication
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

        // Get valid account ID
        _validAccountId = await GetValidAccountId();
        if (_validAccountId == 0)
        {
            throw new InvalidOperationException("No valid accounts found for the provided access token");
        }

        // Account Authentication
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
    }

    private async Task<long> GetValidAccountId()
    {
        var accountListResponses = new List<ProtoOAGetAccountListByAccessTokenRes>();
        var accountListSubscription = _client!.OfType<ProtoOAGetAccountListByAccessTokenRes>()
            .Subscribe(accountListResponses.Add);

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
            return (long)firstAccount.CtidTraderAccountId;
        }

        return 0;
    }

    private async Task DiscoverValidSymbol()
    {
        var symbolResponses = new List<ProtoOASymbolsListRes>();
        var subscription = _client!.OfType<ProtoOASymbolsListRes>()
            .Subscribe(symbolResponses.Add);

        var symbolsRequest = new ProtoOASymbolsListReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(symbolsRequest);
        await Task.Delay(3000);

        subscription.Dispose();

        if (symbolResponses.Count > 0 && symbolResponses[0].Symbol.Count > 0)
        {
            _validSymbolId = symbolResponses[0].Symbol[0].SymbolId;
        }
        else
        {
            _validSymbolId = 1; // Fallback to EURUSD
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}