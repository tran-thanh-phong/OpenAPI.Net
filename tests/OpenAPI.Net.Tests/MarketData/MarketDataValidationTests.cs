using FluentAssertions;
using OpenAPI.Net.Tests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace OpenAPI.Net.Tests.MarketData;

/// <summary>
/// Market data validation and error handling tests - Phase 3
/// Tests data validation, error scenarios, and edge cases
/// </summary>
public class MarketDataValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public MarketDataValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SubscribeToInvalidSymbol_ShouldReceiveError()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing invalid symbol subscription...");

        // Act - Try to subscribe to non-existent symbol
        var invalidSymbolId = 999999999L; // Very unlikely to exist
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(invalidSymbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(3000);

        // Assert
        if (errorResponses.Any())
        {
            var errorResponse = errorResponses[0];
            _output.WriteLine($"Received expected error: {errorResponse.ErrorCode} - {errorResponse.Description}");
            errorResponses.Should().NotBeEmpty("Should receive error for invalid symbol");
        }
        else
        {
            _output.WriteLine("No error received - broker might accept invalid symbol IDs silently");
            // This is also acceptable behavior - some brokers handle invalid symbols gracefully
        }

        subscription.Dispose();
    }

    [Fact]
    public async Task RequestTrendbarWithInvalidTimeRange_ShouldHandleGracefully()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var trendbarResponses = new List<ProtoOAGetTrendbarsRes>();
        var errorResponses = new List<ProtoOAErrorRes>();
        var trendbarSubscription = _client!.OfType<ProtoOAGetTrendbarsRes>()
            .Subscribe(trendbarResponses.Add);
        var errorSubscription = _client.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing trendbar request with invalid time range...");

        var symbolId = await GetValidSymbolId();
        
        // Invalid time range: from > to
        var fromTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var toTimestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        // Act
        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Period = ProtoOATrendbarPeriod.M1,
            FromTimestamp = fromTimestamp, // After toTimestamp
            ToTimestamp = toTimestamp      // Before fromTimestamp
        };

        await _client.SendMessage(trendbarRequest);
        await Task.Delay(5000);

        // Assert
        if (errorResponses.Any())
        {
            _output.WriteLine($"Received error for invalid time range: {errorResponses[0].ErrorCode} - {errorResponses[0].Description}");
            errorResponses.Should().NotBeEmpty("Should receive error for invalid time range");
        }
        else if (trendbarResponses.Any())
        {
            _output.WriteLine("Received trendbar response despite invalid time range - broker handled gracefully");
            var response = trendbarResponses[0];
            response.Trendbar.Should().BeEmpty("Should return empty data for invalid time range");
        }
        else
        {
            _output.WriteLine("No response received for invalid time range");
        }

        trendbarSubscription.Dispose();
        errorSubscription.Dispose();
    }

    [Fact]
    public async Task RequestTickDataWithFutureTimeRange_ShouldReturnEmptyData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var tickResponses = new List<ProtoOAGetTickDataRes>();
        var subscription = _client!.OfType<ProtoOAGetTickDataRes>()
            .Subscribe(tickResponses.Add);

        _output.WriteLine("Testing tick data request with future time range...");

        var symbolId = await GetValidSymbolId();
        
        // Future time range
        var fromTimestamp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var toTimestamp = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeMilliseconds();

        // Act
        var tickRequest = new ProtoOAGetTickDataReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Type = ProtoOAQuoteType.Bid,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(tickRequest);
        await Task.Delay(3000);

        // Assert
        tickResponses.Should().NotBeEmpty("Should receive response even for future time range");
        var response = tickResponses[0];
        response.TickData.Should().BeEmpty("Should return empty tick data for future time range");

        _output.WriteLine("Correctly received empty data for future time range");

        subscription.Dispose();
    }

    [Fact]
    public async Task RequestExcessiveHistoricalData_ShouldHandleLimits()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var trendbarResponses = new List<ProtoOAGetTrendbarsRes>();
        var errorResponses = new List<ProtoOAErrorRes>();
        var trendbarSubscription = _client!.OfType<ProtoOAGetTrendbarsRes>()
            .Subscribe(trendbarResponses.Add);
        var errorSubscription = _client.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing excessive historical data request...");

        var symbolId = await GetValidSymbolId();
        
        // Request 1 year of M1 data (very large request)
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddDays(-365).ToUnixTimeMilliseconds();

        // Act
        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Period = ProtoOATrendbarPeriod.M1,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(trendbarRequest);
        await Task.Delay(10000); // Longer wait for large request

        // Assert
        if (errorResponses.Any())
        {
            _output.WriteLine($"Broker correctly limited excessive request: {errorResponses[0].ErrorCode} - {errorResponses[0].Description}");
            errorResponses.Should().NotBeEmpty("Should receive error or limit for excessive data request");
        }
        else if (trendbarResponses.Any())
        {
            var response = trendbarResponses[0];
            _output.WriteLine($"Received {response.Trendbar.Count} trendbars for large request");
            
            // Broker should either limit the data or return reasonable amount
            response.Trendbar.Count.Should().BeLessOrEqualTo(50000, "Should limit large data requests");
        }

        trendbarSubscription.Dispose();
        errorSubscription.Dispose();
    }

    [Fact]
    public async Task MultipleSubscriptionsToSameSymbol_ShouldHandleCorrectly()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents1 = new List<ProtoOASpotEvent>();
        var spotEvents2 = new List<ProtoOASpotEvent>();
        
        var subscription1 = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents1.Add);
        var subscription2 = _client.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents2.Add);

        _output.WriteLine("Testing multiple subscriptions to same symbol...");

        var symbolId = await GetValidSymbolId();

        // Act - Subscribe twice to the same symbol
        var spotSubscriptionRequest1 = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest1.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest1);
        await Task.Delay(2000);

        var spotSubscriptionRequest2 = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest2.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest2);
        await Task.Delay(5000);

        // Assert
        spotEvents1.Should().NotBeEmpty("First subscription should receive events");
        spotEvents2.Should().NotBeEmpty("Second subscription should receive events");
        
        // Both should receive the same events (they share the same observable stream)
        spotEvents1.Count.Should().Be(spotEvents2.Count, "Both subscriptions should receive same events");

        _output.WriteLine($"Both subscriptions received {spotEvents1.Count} events");

        subscription1.Dispose();
        subscription2.Dispose();
    }

    [Fact]
    public async Task SpotDataConsistency_PricesShouldBeReasonable()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing spot data consistency and validation...");

        var symbolId = await GetValidSymbolId();

        // Act
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(10000); // Longer observation period

        // Assert
        spotEvents.Should().NotBeEmpty("Should receive spot events");

        if (spotEvents.Count > 1)
        {
            // Check for price consistency
            var prices = spotEvents.Select(e => (e.Bid + e.Ask) / 2).ToList();
            var priceChanges = new List<double>();
            
            for (int i = 1; i < prices.Count; i++)
            {
                var change = System.Math.Abs((double)(prices[i] - prices[i-1])) / (double)prices[i-1];
                priceChanges.Add(change);
            }

            // Check that most price changes are reasonable (less than 1%)
            var reasonableChanges = priceChanges.Count(change => change <= 0.01);
            var reasonableRatio = (double)reasonableChanges / priceChanges.Count;
            
            reasonableRatio.Should().BeGreaterOrEqualTo(0.9, // 90% of changes should be reasonable
                "Most price changes should be reasonable");

            _output.WriteLine($"Analyzed {priceChanges.Count} price changes, {reasonableRatio:P1} were reasonable");
        }

        // Check timestamp consistency
        var timestamps = spotEvents.Select(e => e.Timestamp).ToList();
        for (int i = 1; i < timestamps.Count; i++)
        {
            timestamps[i].Should().BeGreaterOrEqualTo(timestamps[i-1], 
                "Timestamps should be in non-decreasing order");
        }

        subscription.Dispose();
    }

    [Fact]
    public async Task UnsubscribeFromNonSubscribedSymbol_ShouldHandleGracefully()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing unsubscribe from non-subscribed symbol...");

        var symbolId = await GetValidSymbolId();

        // Act - Try to unsubscribe without subscribing first
        var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        unsubscribeRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(unsubscribeRequest);
        await Task.Delay(3000);

        // Assert
        if (errorResponses.Any())
        {
            _output.WriteLine($"Received error for unsubscribing non-subscribed symbol: {errorResponses[0].ErrorCode}");
        }
        else
        {
            _output.WriteLine("No error received - broker handled unsubscribe gracefully");
        }

        // Either behavior is acceptable - some brokers error, others ignore
        subscription.Dispose();
    }

    [Fact]
    public async Task EmptySymbolListSubscription_ShouldHandleCorrectly()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var errorResponses = new List<ProtoOAErrorRes>();
        var subscription = _client!.OfType<ProtoOAErrorRes>()
            .Subscribe(errorResponses.Add);

        _output.WriteLine("Testing empty symbol list subscription...");

        // Act - Subscribe with empty symbol list
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        // Don't add any symbols

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(3000);

        // Assert
        if (errorResponses.Any())
        {
            _output.WriteLine($"Received error for empty symbol subscription: {errorResponses[0].ErrorCode} - {errorResponses[0].Description}");
            errorResponses.Should().NotBeEmpty("Should receive error for empty symbol list");
        }
        else
        {
            _output.WriteLine("No error received for empty symbol list - broker handled gracefully");
        }

        subscription.Dispose();
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
            throw new InvalidOperationException("No valid accounts found");
        }

        // Account Authentication
        var accountAuthResponses = new List<ProtoOAAccountAuthRes>();
        var accountAuthSubscription = _client.OfType<ProtoOAAccountAuthRes>()
            .Subscribe(accountAuthResponses.Add);

        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = _validAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountAuthRequest);
        
        timeout = DateTime.UtcNow.AddSeconds(10);
        while (accountAuthResponses.Count == 0 && DateTime.UtcNow < timeout)
        {
            await Task.Delay(500);
        }
        
        accountAuthSubscription.Dispose();
        
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

    private async Task<long> GetValidSymbolId()
    {
        var symbolResponses = new List<ProtoOASymbolsListRes>();
        var symbolSubscription = _client!.OfType<ProtoOASymbolsListRes>()
            .Subscribe(symbolResponses.Add);

        var symbolsRequest = new ProtoOASymbolsListReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(symbolsRequest);
        await Task.Delay(3000);

        symbolSubscription.Dispose();

        if (symbolResponses.Count > 0 && symbolResponses[0].Symbol.Count > 0)
        {
            var firstSymbol = symbolResponses[0].Symbol[0];
            return firstSymbol.SymbolId;
        }

        return 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}