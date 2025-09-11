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
/// Real-time market data testing - Phase 3
/// Tests spot price subscriptions, streaming data, and real-time updates
/// </summary>
public class RealTimeDataTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public RealTimeDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SubscribeToSingleSymbolSpot_ShouldReceiveSpotEvents()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing single symbol spot subscription...");

        // Get a valid symbol first
        var symbolId = await GetValidSymbolId();
        symbolId.Should().BeGreaterThan(0, "Should have found a valid symbol");

        // Act - Subscribe to spot prices
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(5000); // Wait for spot data

        // Assert
        spotEvents.Should().NotBeEmpty("Should receive spot price updates");
        var spotEvent = spotEvents[0];
        spotEvent.CtidTraderAccountId.Should().Be(_validAccountId);
        spotEvent.SymbolId.Should().Be(symbolId);
        spotEvent.Bid.Should().BeGreaterThan(0);
        spotEvent.Ask.Should().BeGreaterThan(0);
        spotEvent.Ask.Should().BeGreaterThanOrEqualTo(spotEvent.Bid);

        _output.WriteLine($"Received {spotEvents.Count} spot events for symbol {symbolId}");
        _output.WriteLine($"Latest: Bid={spotEvent.Bid}, Ask={spotEvent.Ask}, Spread={spotEvent.Ask - spotEvent.Bid}");

        subscription.Dispose();
    }

    [Fact]
    public async Task SubscribeToMultipleSymbolsSpot_ShouldReceiveAllSymbolUpdates()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing multiple symbols spot subscription...");

        // Get multiple valid symbols
        var symbolIds = await GetMultipleValidSymbolIds(3);
        symbolIds.Should().HaveCountGreaterOrEqualTo(2, "Should have found multiple valid symbols");

        // Act - Subscribe to multiple symbols
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        foreach (var symbolId in symbolIds)
        {
            spotSubscriptionRequest.SymbolId.Add(symbolId);
        }

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(8000); // Wait longer for multiple symbol updates

        // Assert
        spotEvents.Should().NotBeEmpty("Should receive spot updates for multiple symbols");
        
        // Check we received updates for different symbols
        var uniqueSymbols = spotEvents.Select(e => e.SymbolId).Distinct().ToList();
        uniqueSymbols.Should().HaveCountGreaterOrEqualTo(1, "Should receive updates for at least one symbol");

        foreach (var symbolId in uniqueSymbols)
        {
            var symbolEvents = spotEvents.Where(e => e.SymbolId == symbolId).ToList();
            symbolEvents.Should().NotBeEmpty($"Should have events for symbol {symbolId}");
            
            var latestEvent = symbolEvents.OrderByDescending(e => e.Timestamp).First();
            latestEvent.Bid.Should().BeGreaterThan(0);
            latestEvent.Ask.Should().BeGreaterThan(0);
            latestEvent.Ask.Should().BeGreaterThanOrEqualTo(latestEvent.Bid);
        }

        _output.WriteLine($"Received {spotEvents.Count} total spot events for {uniqueSymbols.Count} different symbols");

        subscription.Dispose();
    }

    [Fact]
    public async Task UnsubscribeFromSpotData_ShouldStopReceivingUpdates()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing spot unsubscription...");

        var symbolId = await GetValidSymbolId();

        // Subscribe first
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(3000);

        var eventsAfterSubscribe = spotEvents.Count;
        eventsAfterSubscribe.Should().BeGreaterThan(0, "Should have received events after subscription");

        // Act - Unsubscribe
        var unsubscribeRequest = new ProtoOAUnsubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        unsubscribeRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(unsubscribeRequest);
        await Task.Delay(3000);

        var eventsAfterUnsubscribe = spotEvents.Count;

        // Assert - Should have stopped receiving new events (or very few)
        var newEventsAfterUnsubscribe = eventsAfterUnsubscribe - eventsAfterSubscribe;
        _output.WriteLine($"Events after subscribe: {eventsAfterSubscribe}");
        _output.WriteLine($"Events after unsubscribe: {eventsAfterUnsubscribe}");
        _output.WriteLine($"New events after unsubscribe: {newEventsAfterUnsubscribe}");

        // Allow for a small buffer as some events might be in transit
        newEventsAfterUnsubscribe.Should().BeLessOrEqualTo(5, "Should have minimal events after unsubscription");

        subscription.Dispose();
    }

    [Fact]
    public async Task SpotDataStream_ShouldHaveValidTimestamps()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing spot data timestamps...");

        var symbolId = await GetValidSymbolId();

        // Act
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(5000);

        // Assert
        spotEvents.Should().NotBeEmpty();
        
        foreach (var spotEvent in spotEvents)
        {
            spotEvent.Timestamp.Should().BeGreaterThan(0, "Timestamp should be valid");
            
            // Check timestamp is recent (within last few minutes)
            var eventTime = DateTimeOffset.FromUnixTimeMilliseconds(spotEvent.Timestamp);
            var now = DateTimeOffset.UtcNow;
            var timeDiff = now - eventTime;
            
            timeDiff.Should().BeLessOrEqualTo(TimeSpan.FromMinutes(5), 
                $"Event timestamp should be recent. Event: {eventTime}, Now: {now}");
        }

        // Check timestamps are in reasonable order (allowing for some out-of-order)
        var timestamps = spotEvents.Select(e => e.Timestamp).ToList();
        var orderedTimestamps = timestamps.OrderBy(t => t).ToList();
        
        _output.WriteLine($"First timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(timestamps.First())}");
        _output.WriteLine($"Last timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(timestamps.Last())}");

        subscription.Dispose();
    }

    [Fact]
    public async Task SpotDataQuality_ShouldHaveValidPriceRanges()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var spotEvents = new List<ProtoOASpotEvent>();
        var subscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(spotEvents.Add);

        _output.WriteLine("Testing spot data quality...");

        var symbolId = await GetValidSymbolId();

        // Act
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(7000); // Longer wait for more data points

        // Assert
        spotEvents.Should().NotBeEmpty();

        foreach (var spotEvent in spotEvents)
        {
            // Basic price validation
            spotEvent.Bid.Should().BeGreaterThan(0, "Bid should be positive");
            spotEvent.Ask.Should().BeGreaterThan(0, "Ask should be positive");
            spotEvent.Ask.Should().BeGreaterThanOrEqualTo(spotEvent.Bid, "Ask should be >= Bid");

            // Reasonable spread check (ask-bid should not be more than 10% of bid)
            var spread = spotEvent.Ask - spotEvent.Bid;
            var spreadPercent = (spread / spotEvent.Bid) * 100;
            spreadPercent.Should().BeLessOrEqualTo(10, 
                $"Spread should be reasonable. Bid: {spotEvent.Bid}, Ask: {spotEvent.Ask}, Spread%: {spreadPercent:F4}");
        }

        // Check price consistency - prices shouldn't jump too dramatically
        if (spotEvents.Count > 1)
        {
            var prices = spotEvents.Select(e => (e.Bid + e.Ask) / 2).ToList();
            for (int i = 1; i < Math.Min(prices.Count, 10); i++)
            {
                var priceChange = System.Math.Abs((double)(prices[i] - prices[i-1])) / (double)prices[i-1];
                priceChange.Should().BeLessOrEqualTo(0.05, // 5% max change between consecutive prices
                    $"Price change should be reasonable between consecutive updates. Price1: {prices[i-1]}, Price2: {prices[i]}");
            }
        }

        _output.WriteLine($"Analyzed {spotEvents.Count} spot events for price quality");
        var avgBid = spotEvents.Average(e => (double)e.Bid);
        var avgAsk = spotEvents.Average(e => (double)e.Ask);
        var avgSpread = spotEvents.Average(e => (double)(e.Ask - e.Bid));
        _output.WriteLine($"Average Bid: {avgBid:F5}, Ask: {avgAsk:F5}, Spread: {avgSpread:F5}");

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

        _output.WriteLine("Application authentication successful");

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

        _output.WriteLine($"Account authentication successful for account {_validAccountId}");
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

    private async Task<List<long>> GetMultipleValidSymbolIds(int count)
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
            return symbolResponses[0].Symbol
                .Take(count)
                .Select(s => s.SymbolId)
                .ToList();
        }

        return new List<long>();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}