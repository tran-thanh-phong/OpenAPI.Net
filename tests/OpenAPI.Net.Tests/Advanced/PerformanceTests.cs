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
using System.Threading;

namespace OpenAPI.Net.Tests.Advanced;

/// <summary>
/// Phase 5: Advanced Testing - Performance Tests
/// Tests message throughput, memory usage, and high-frequency operations
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;
    private long _validSymbolId;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task MessageThroughput_MultipleRequests_ShouldMaintainPerformance()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var responses = new List<ProtoOATraderRes>();
        var subscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(responses.Add);

        _output.WriteLine("Testing message throughput with multiple trader requests...");

        const int messageCount = 20;
        var stopwatch = Stopwatch.StartNew();

        // Act - Send multiple messages rapidly
        var tasks = new List<Task>();
        for (int i = 0; i < messageCount; i++)
        {
            var traderRequest = new ProtoOATraderReq
            {
                CtidTraderAccountId = _validAccountId
            };
            
            tasks.Add(_client.SendMessage(traderRequest));
            
            // Small delay to avoid overwhelming the server
            await Task.Delay(10);
        }

        await Task.WhenAll(tasks);
        var sendDuration = stopwatch.Elapsed;

        // Wait for responses
        await Task.Delay(10000); // Extended wait for all responses
        stopwatch.Stop();
        
        var totalDuration = stopwatch.Elapsed;

        // Assert
        _output.WriteLine($"Sent {messageCount} messages in {sendDuration.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Total test duration: {totalDuration.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Received {responses.Count} responses");
        
        var messagesPerSecond = messageCount / sendDuration.TotalSeconds;
        _output.WriteLine($"Message send rate: {messagesPerSecond:F2} messages/second");
        
        // Performance assertions
        sendDuration.TotalSeconds.Should().BeLessThan(30, "Message sending should complete within reasonable time");
        messagesPerSecond.Should().BeGreaterThan(0.5, "Should achieve reasonable message throughput");
        
        // Should receive at least some responses (some may be rate-limited)
        responses.Count.Should().BeGreaterThan(0, "Should receive at least some responses");

        subscription.Dispose();
        _output.WriteLine("Message throughput test completed");
    }

    [Fact]
    public async Task MemoryUsage_ExtendedOperations_ShouldBeStable()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        _output.WriteLine("Testing memory usage during extended operations...");

        // Get initial memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var initialMemory = GC.GetTotalMemory(false);
        _output.WriteLine($"Initial memory usage: {initialMemory / 1024 / 1024:F2} MB");

        // Act - Perform multiple operations
        for (int cycle = 0; cycle < 10; cycle++)
        {
            var responses = new List<ProtoOATraderRes>();
            var subscription = _client!.OfType<ProtoOATraderRes>()
                .Subscribe(responses.Add);

            // Send multiple requests in this cycle
            for (int i = 0; i < 5; i++)
            {
                var traderRequest = new ProtoOATraderReq
                {
                    CtidTraderAccountId = _validAccountId
                };
                
                await _client.SendMessage(traderRequest);
                await Task.Delay(50); // Small delay
            }

            await Task.Delay(2000); // Wait for responses
            subscription.Dispose();
            
            // Force garbage collection every few cycles
            if (cycle % 3 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Check final memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        
        _output.WriteLine($"Final memory usage: {finalMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F2} MB");

        // Assert - Memory should not increase excessively
        var memoryIncreasePercent = (double)memoryIncrease / initialMemory * 100;
        _output.WriteLine($"Memory increase percentage: {memoryIncreasePercent:F1}%");
        
        memoryIncreasePercent.Should().BeLessThan(200, "Memory usage should not increase excessively");
        
        _output.WriteLine("Memory usage test completed");
    }

    [Fact]
    public async Task HighFrequencySymbolRequests_ShouldHandleEfficiently()
    {
        // Arrange
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        _output.WriteLine("Testing high-frequency symbol requests...");

        var symbolResponses = new List<ProtoOASymbolByIdRes>();
        var subscription = _client!.OfType<ProtoOASymbolByIdRes>()
            .Subscribe(symbolResponses.Add);

        const int requestCount = 15;
        var stopwatch = Stopwatch.StartNew();

        // Act - Send rapid symbol requests
        for (int i = 0; i < requestCount; i++)
        {
            var symbolRequest = new ProtoOASymbolByIdReq
            {
                CtidTraderAccountId = _validAccountId
            };
            symbolRequest.SymbolId.Add(_validSymbolId);

            await _client.SendMessage(symbolRequest);
            await Task.Delay(100); // Small delay to avoid server overload
        }

        // Wait for responses
        await Task.Delay(8000);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Sent {requestCount} symbol requests in {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Received {symbolResponses.Count} symbol responses");
        
        var requestsPerSecond = requestCount / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Request rate: {requestsPerSecond:F2} requests/second");

        // Performance validation
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "High-frequency requests should complete promptly");
        symbolResponses.Count.Should().BeGreaterThan(0, "Should receive at least some responses");

        // Validate response quality
        foreach (var response in symbolResponses.Take(3))
        {
            response.CtidTraderAccountId.Should().Be(_validAccountId);
            if (response.Symbol.Count > 0)
            {
                var symbol = response.Symbol[0];
                symbol.SymbolId.Should().Be(_validSymbolId);
                // Note: ProtoOASymbol doesn't have SymbolName property
            }
        }

        subscription.Dispose();
        _output.WriteLine("High-frequency symbol requests test completed");
    }

    [Fact]
    public async Task ConcurrentSubscriptions_MultipleDataStreams_ShouldHandleEfficiently()
    {
        // Arrange
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        _output.WriteLine("Testing concurrent subscriptions to multiple data streams...");

        var spotEvents = new List<ProtoOASpotEvent>();
        var allMessages = new List<IMessage>();
        
        var spotSubscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);
        var allSubscription = _client.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        var stopwatch = Stopwatch.StartNew();

        // Act - Subscribe to multiple symbol spot prices
        var symbolIds = new List<long> { _validSymbolId };
        
        // If we have multiple symbols, use them
        if (symbolIds.Count == 1)
        {
            // Add some common symbol IDs that might exist
            symbolIds.AddRange(new long[] { 1, 2, 3, 4, 5 });
        }

        var subscriptionTasks = new List<Task>();
        
        foreach (var symbolId in symbolIds.Take(5)) // Limit to 5 concurrent subscriptions
        {
            var subscribeRequest = new ProtoOASubscribeSpotsReq
            {
                CtidTraderAccountId = _validAccountId
            };
            subscribeRequest.SymbolId.Add(symbolId);

            subscriptionTasks.Add(_client.SendMessage(subscribeRequest));
            await Task.Delay(200); // Stagger subscriptions
        }

        await Task.WhenAll(subscriptionTasks);
        
        // Wait for spot events
        await Task.Delay(10000);
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Concurrent subscription test duration: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Total messages received: {allMessages.Count}");
        _output.WriteLine($"Spot events received: {spotEvents.Count}");
        
        // Validate performance
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "Concurrent subscriptions should be efficient");
        
        // Should receive some market data
        if (spotEvents.Count > 0)
        {
            _output.WriteLine($"Average spot events per second: {spotEvents.Count / stopwatch.Elapsed.TotalSeconds:F2}");
            
            // Validate spot event structure
            foreach (var spotEvent in spotEvents.Take(3))
            {
                spotEvent.CtidTraderAccountId.Should().Be(_validAccountId);
                spotEvent.SymbolId.Should().BeGreaterThan(0);
                spotEvent.Timestamp.Should().BeGreaterThan(0);
            }
        }
        else
        {
            _output.WriteLine("No spot events received - market may be closed or symbols unavailable");
        }

        // Cleanup - Unsubscribe from all
        var unsubscribeTasks = new List<Task>();
        foreach (var symbolId in symbolIds.Take(5))
        {
            var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq
            {
                CtidTraderAccountId = _validAccountId
            };
            unsubscribeRequest.SymbolId.Add(symbolId);
            
            unsubscribeTasks.Add(_client.SendMessage(unsubscribeRequest));
        }
        
        await Task.WhenAll(unsubscribeTasks);

        spotSubscription.Dispose();
        allSubscription.Dispose();
        
        _output.WriteLine("Concurrent subscriptions test completed");
    }

    [Fact]
    public async Task LargeDatasetRetrieval_HistoricalData_ShouldHandleEfficiently()
    {
        // Arrange
        await SetupAuthenticatedClient();
        await DiscoverValidSymbol();
        
        _output.WriteLine("Testing large dataset retrieval with historical data...");

        var trendbarResponses = new List<ProtoOAGetTrendbarsRes>();
        var subscription = _client!.OfType<ProtoOAGetTrendbarsRes>()
            .Subscribe(trendbarResponses.Add);

        var stopwatch = Stopwatch.StartNew();

        // Act - Request large historical dataset
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds(); // 30 days of data

        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = _validSymbolId,
            Period = ProtoOATrendbarPeriod.M1, // 1-minute bars for more data
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            Count = 1000 // Request large number of bars
        };

        await _client.SendMessage(trendbarRequest);
        await Task.Delay(10000); // Extended wait for large dataset
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Large dataset retrieval took: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Received {trendbarResponses.Count} trendbar responses");

        if (trendbarResponses.Count > 0)
        {
            var response = trendbarResponses[0];
            var totalBars = response.Trendbar.Count;
            
            _output.WriteLine($"Retrieved {totalBars} trendbar records");
            _output.WriteLine($"Data retrieval rate: {totalBars / stopwatch.Elapsed.TotalSeconds:F2} records/second");
            _output.WriteLine($"Has more data: {response.HasMore}");

            // Performance validation
            stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "Large dataset retrieval should be efficient");
            totalBars.Should().BeGreaterThan(0, "Should retrieve historical data");

            // Validate data integrity
            if (totalBars > 0)
            {
                var firstBar = response.Trendbar[0];
                firstBar.Volume.Should().BeGreaterOrEqualTo(0);
                firstBar.Period.Should().Be(ProtoOATrendbarPeriod.M1);
                
                _output.WriteLine($"Sample bar - Volume: {firstBar.Volume}, Period: {firstBar.Period}");
            }
        }
        else
        {
            _output.WriteLine("No historical data received - may be outside trading hours or symbol unavailable");
        }

        subscription.Dispose();
        _output.WriteLine("Large dataset retrieval test completed");
    }

    [Fact]
    public async Task ConnectionStability_UnderLoad_ShouldMaintainConnection()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        _output.WriteLine("Testing connection stability under load...");

        var messageCount = 0;
        var errorCount = 0;
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(msg =>
        {
            allMessages.Add(msg);
            Interlocked.Increment(ref messageCount);
            
            if (msg is ProtoOAErrorRes)
            {
                Interlocked.Increment(ref errorCount);
            }
        }));

        var stopwatch = Stopwatch.StartNew();
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Act - Generate continuous load
        var loadTask = Task.Run(async () =>
        {
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    var traderRequest = new ProtoOATraderReq
                    {
                        CtidTraderAccountId = _validAccountId
                    };

                    await _client.SendMessage(traderRequest);
                    await Task.Delay(500, cancellationToken.Token); // Send every 500ms
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error during load generation: {ex.Message}");
                    break;
                }
            }
        });

        await loadTask;
        stopwatch.Stop();

        // Give time for final responses
        await Task.Delay(2000);

        // Assert
        _output.WriteLine($"Load test duration: {stopwatch.Elapsed.TotalSeconds:F1} seconds");
        _output.WriteLine($"Total messages received: {messageCount}");
        _output.WriteLine($"Error messages: {errorCount}");
        _output.WriteLine($"Connection status: {(_client.IsDisposed ? "Disconnected" : "Connected")}");

        // Connection should remain stable
        _client.IsDisposed.Should().BeFalse("Connection should remain stable under load");
        
        // Should have processed messages
        messageCount.Should().BeGreaterThan(0, "Should have received messages during load test");
        
        // Error rate should not be excessive
        var errorRate = errorCount / (double)Math.Max(messageCount, 1) * 100;
        _output.WriteLine($"Error rate: {errorRate:F1}%");
        errorRate.Should().BeLessThan(50, "Error rate should not be excessive under normal load");

        subscription.Dispose();
        _output.WriteLine("Connection stability under load test completed");
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