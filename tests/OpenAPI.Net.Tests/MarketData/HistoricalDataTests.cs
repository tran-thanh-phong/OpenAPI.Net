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
/// Historical market data testing - Phase 3
/// Tests trendbar data, tick data, and historical data retrieval
/// </summary>
public class HistoricalDataTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public HistoricalDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetTrendbarData_M1Period_ShouldReturnValidOHLCData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var trendbarResponses = new List<ProtoOAGetTrendbarsRes>();
        var subscription = _client!.OfType<ProtoOAGetTrendbarsRes>()
            .Subscribe(trendbarResponses.Add);

        _output.WriteLine("Testing M1 trendbar data retrieval...");

        var symbolId = await GetValidSymbolId();
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeMilliseconds();

        // Act - Request 1-minute trendbars
        var trendbarRequest = new ProtoOAGetTrendbarsReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Period = ProtoOATrendbarPeriod.M1,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(trendbarRequest);
        await Task.Delay(5000); // Wait for response

        // Assert
        trendbarResponses.Should().NotBeEmpty("Should receive trendbar response");
        var response = trendbarResponses[0];
        
        response.CtidTraderAccountId.Should().Be(_validAccountId);
        response.Period.Should().Be(ProtoOATrendbarPeriod.M1);
        response.Trendbar.Should().NotBeEmpty("Should contain trendbar data");

        foreach (var trendbar in response.Trendbar)
        {
            // OHLC validation
            trendbar.Low.Should().BeGreaterThan(0, "Open price should be positive");
            var calculatedHigh = trendbar.Low + (long)trendbar.DeltaHigh;
            calculatedHigh.Should().BeGreaterThan(0, "High price should be positive");
            trendbar.Low.Should().BeGreaterThan(0, "Low price should be positive");
            var calculatedClose = trendbar.Low + (long)trendbar.DeltaClose;
            calculatedClose.Should().BeGreaterThan(0, "Close price should be positive");

            // OHLC relationship validation
            var calculatedOpen = trendbar.Low + (long)trendbar.DeltaOpen;
            calculatedHigh.Should().BeGreaterThanOrEqualTo(calculatedOpen, "High >= Open");
            calculatedHigh.Should().BeGreaterThanOrEqualTo(calculatedClose, "High >= Close");
            trendbar.Low.Should().BeLessOrEqualTo(calculatedOpen, "Low <= Open");
            trendbar.Low.Should().BeLessOrEqualTo(calculatedClose, "Low <= Close");
            calculatedHigh.Should().BeGreaterThanOrEqualTo(trendbar.Low, "High >= Low");

            // Volume should be non-negative
            trendbar.Volume.Should().BeGreaterOrEqualTo(0, "Volume should be non-negative");

            // Timestamp validation
            trendbar.UtcTimestampInMinutes.Should().BeGreaterThan(0, "Timestamp should be valid");
            // Timestamp validation - converting minutes to milliseconds for comparison
            // Additional timestamp validation could be added here
        }

        _output.WriteLine($"Retrieved {response.Trendbar.Count} M1 trendbars for symbol {symbolId}");
        _output.WriteLine($"Period: {DateTimeOffset.FromUnixTimeMilliseconds(fromTimestamp)} to {DateTimeOffset.FromUnixTimeMilliseconds(toTimestamp)}");

        subscription.Dispose();
    }

    [Fact]
    public async Task GetTrendbarData_DifferentPeriods_ShouldReturnAppropriateData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var symbolId = await GetValidSymbolId();
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();

        var periods = new[] { ProtoOATrendbarPeriod.M1, ProtoOATrendbarPeriod.M5, ProtoOATrendbarPeriod.H1 };

        foreach (var period in periods)
        {
            _output.WriteLine($"Testing {period} trendbar data...");

            var trendbarResponses = new List<ProtoOAGetTrendbarsRes>();
            var subscription = _client!.OfType<ProtoOAGetTrendbarsRes>()
                .Subscribe(trendbarResponses.Add);

            // Act
            var trendbarRequest = new ProtoOAGetTrendbarsReq
            {
                CtidTraderAccountId = _validAccountId,
                SymbolId = symbolId,
                Period = period,
                FromTimestamp = fromTimestamp,
                ToTimestamp = toTimestamp
            };

            await _client.SendMessage(trendbarRequest);
            await Task.Delay(3000);

            // Assert
            trendbarResponses.Should().NotBeEmpty($"Should receive {period} trendbar response");
            var response = trendbarResponses[0];
            response.Period.Should().Be(period);
            response.Trendbar.Should().NotBeEmpty($"Should contain {period} trendbar data");

            // Verify period-specific characteristics
            if (response.Trendbar.Count > 1)
            {
                var firstBar = response.Trendbar[0];
                var secondBar = response.Trendbar[1];
                var timeDiff = secondBar.UtcTimestampInMinutes - firstBar.UtcTimestampInMinutes;

                // Check approximate time difference based on period
                var expectedTimeDiff = period switch
                {
                    ProtoOATrendbarPeriod.M1 => TimeSpan.FromMinutes(1).TotalMilliseconds,
                    ProtoOATrendbarPeriod.M5 => TimeSpan.FromMinutes(5).TotalMilliseconds,
                    ProtoOATrendbarPeriod.H1 => TimeSpan.FromHours(1).TotalMilliseconds,
                    _ => 0
                };

                if (expectedTimeDiff > 0)
                {
                    var actualDiff = (double)timeDiff;
                    var expectedDiff = (double)expectedTimeDiff;
                    var tolerance = expectedTimeDiff * 0.1;
                    
                    actualDiff.Should().BeGreaterOrEqualTo(expectedDiff - tolerance, 
                        $"Time difference should be within tolerance for {period} bars");
                    actualDiff.Should().BeLessOrEqualTo(expectedDiff + tolerance, 
                        $"Time difference should be within tolerance for {period} bars");
                }
            }

            _output.WriteLine($"Retrieved {response.Trendbar.Count} {period} trendbars");

            subscription.Dispose();
        }
    }

    [Fact]
    public async Task GetTickData_BidQuotes_ShouldReturnValidTickData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var tickResponses = new List<ProtoOAGetTickDataRes>();
        var subscription = _client!.OfType<ProtoOAGetTickDataRes>()
            .Subscribe(tickResponses.Add);

        _output.WriteLine("Testing bid tick data retrieval...");

        var symbolId = await GetValidSymbolId();
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds(); // 1 hour of tick data

        // Act - Request bid tick data
        var tickRequest = new ProtoOAGetTickDataReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Type = ProtoOAQuoteType.Bid,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(tickRequest);
        await Task.Delay(5000);

        // Assert
        tickResponses.Should().NotBeEmpty("Should receive tick data response");
        var response = tickResponses[0];
        
        response.CtidTraderAccountId.Should().Be(_validAccountId);
        // Tick data validation - response doesn't include Type property
        response.TickData.Should().NotBeEmpty("Should contain tick data");

        foreach (var tick in response.TickData)
        {
            tick.Tick.Should().BeGreaterThan(0, "Tick price should be positive");
            tick.Timestamp.Should().BeGreaterThan(0, "Tick timestamp should be valid");
            tick.Timestamp.Should().BeGreaterOrEqualTo(fromTimestamp, "Tick should be within requested time range");
            tick.Timestamp.Should().BeLessOrEqualTo(toTimestamp, "Tick should be within requested time range");
        }

        // Check that ticks are in chronological order
        var timestamps = response.TickData.Select(t => t.Timestamp).ToList();
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();
        timestamps.Should().BeEquivalentTo(sortedTimestamps, "Ticks should be in chronological order");

        _output.WriteLine($"Retrieved {response.TickData.Count} bid ticks for symbol {symbolId}");
        _output.WriteLine($"Period: {DateTimeOffset.FromUnixTimeMilliseconds(fromTimestamp)} to {DateTimeOffset.FromUnixTimeMilliseconds(toTimestamp)}");

        subscription.Dispose();
    }

    [Fact]
    public async Task GetTickData_AskQuotes_ShouldReturnValidTickData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var tickResponses = new List<ProtoOAGetTickDataRes>();
        var subscription = _client!.OfType<ProtoOAGetTickDataRes>()
            .Subscribe(tickResponses.Add);

        _output.WriteLine("Testing ask tick data retrieval...");

        var symbolId = await GetValidSymbolId();
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

        // Act - Request ask tick data
        var tickRequest = new ProtoOAGetTickDataReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Type = ProtoOAQuoteType.Ask,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(tickRequest);
        await Task.Delay(5000);

        // Assert
        tickResponses.Should().NotBeEmpty("Should receive ask tick data response");
        var response = tickResponses[0];
        
        response.CtidTraderAccountId.Should().Be(_validAccountId);
        // Ask tick data validation
        response.TickData.Should().NotBeEmpty("Should contain ask tick data");

        _output.WriteLine($"Retrieved {response.TickData.Count} ask ticks for symbol {symbolId}");

        subscription.Dispose();
    }

    [Fact]
    public async Task CompareBidAskTickData_ShouldHaveReasonableSpread()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var symbolId = await GetValidSymbolId();
        var toTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fromTimestamp = DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeMilliseconds(); // 30 minutes

        // Get bid tick data
        var bidTickResponses = new List<ProtoOAGetTickDataRes>();
        var bidSubscription = _client!.OfType<ProtoOAGetTickDataRes>()
            .Subscribe(bidTickResponses.Add);

        var bidTickRequest = new ProtoOAGetTickDataReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Type = ProtoOAQuoteType.Bid,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(bidTickRequest);
        await Task.Delay(3000);
        bidSubscription.Dispose();

        // Get ask tick data
        var askTickResponses = new List<ProtoOAGetTickDataRes>();
        var askSubscription = _client!.OfType<ProtoOAGetTickDataRes>()
            .Subscribe(askTickResponses.Add);

        var askTickRequest = new ProtoOAGetTickDataReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Type = ProtoOAQuoteType.Ask,
            FromTimestamp = fromTimestamp,
            ToTimestamp = toTimestamp
        };

        await _client.SendMessage(askTickRequest);
        await Task.Delay(3000);
        askSubscription.Dispose();

        // Assert
        bidTickResponses.Should().NotBeEmpty("Should receive bid tick data");
        askTickResponses.Should().NotBeEmpty("Should receive ask tick data");

        var bidResponse = bidTickResponses[0];
        var askResponse = askTickResponses[0];

        bidResponse.TickData.Should().NotBeEmpty();
        askResponse.TickData.Should().NotBeEmpty();

        // Compare spreads at similar times
        var bidTicks = bidResponse.TickData.ToDictionary(t => t.Timestamp, t => t.Tick);
        var askTicks = askResponse.TickData.ToDictionary(t => t.Timestamp, t => t.Tick);

        var commonTimestamps = bidTicks.Keys.Intersect(askTicks.Keys).Take(10).ToList();
        
        if (commonTimestamps.Any())
        {
            foreach (var timestamp in commonTimestamps)
            {
                var bid = bidTicks[timestamp];
                var ask = askTicks[timestamp];
                
                ask.Should().BeGreaterThanOrEqualTo(bid, "Ask should be >= Bid");
                
                var spread = ask - bid;
                var spreadPercent = (spread / bid) * 100;
                spreadPercent.Should().BeLessOrEqualTo(1, // 1% max spread
                    $"Spread should be reasonable at {DateTimeOffset.FromUnixTimeMilliseconds(timestamp)}. Bid: {bid}, Ask: {ask}");
            }
            
            _output.WriteLine($"Analyzed {commonTimestamps.Count} common timestamps for bid/ask spread");
        }
        else
        {
            _output.WriteLine("No exact timestamp matches found between bid and ask data, which is normal for tick data");
        }
    }

    [Fact]
    public async Task SubscribeToLiveTrendbars_ShouldReceiveTrendbarUpdates()
    {
        // Arrange
        await SetupAuthenticatedClient();
        var trendbarEvents = new List<ProtoOASpotEvent>();
        
        // First subscribe to spot data (required for live trendbars)
        var spotSubscription = _client!.OfType<ProtoOASpotEvent>()
            .Subscribe(trendbarEvents.Add);

        var symbolId = await GetValidSymbolId();

        // Subscribe to spot data first
        var spotSubscriptionRequest = new ProtoOASubscribeSpotsReq
        {
            CtidTraderAccountId = _validAccountId
        };
        spotSubscriptionRequest.SymbolId.Add(symbolId);

        await _client.SendMessage(spotSubscriptionRequest);
        await Task.Delay(2000);

        // Now subscribe to live trendbars
        var liveTrendbarResponses = new List<ProtoOASubscribeLiveTrendbarRes>();
        var trendbarSubscription = _client.OfType<ProtoOASubscribeLiveTrendbarRes>()
            .Subscribe(liveTrendbarResponses.Add);

        _output.WriteLine("Testing live trendbar subscription...");

        // Act - Subscribe to live M1 trendbars
        var liveTrendbarRequest = new ProtoOASubscribeLiveTrendbarReq
        {
            CtidTraderAccountId = _validAccountId,
            SymbolId = symbolId,
            Period = ProtoOATrendbarPeriod.M1
        };

        await _client.SendMessage(liveTrendbarRequest);
        await Task.Delay(5000); // Wait for confirmation and initial data

        // Assert
        liveTrendbarResponses.Should().NotBeEmpty("Should receive live trendbar subscription confirmation");
        var response = liveTrendbarResponses[0];
        response.CtidTraderAccountId.Should().Be(_validAccountId);
        // Live trendbar response doesn't include Period property

        _output.WriteLine($"Successfully subscribed to live M1 trendbars for symbol {symbolId}");

        spotSubscription.Dispose();
        trendbarSubscription.Dispose();
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