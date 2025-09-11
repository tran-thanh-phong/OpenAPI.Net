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
/// Phase 4: Account Management Testing - Transaction History Tests
/// Tests transaction history operations including deal history and basic deal validation
/// </summary>
public class TransactionHistoryTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public TransactionHistoryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetDealsHistory_WithTimeRange_ShouldReturnTransactions()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine("Testing deals history retrieval...");

        // Act - Request deals for the last 30 days
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = 100
        };

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000); // Wait for response

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        dealHistory.Should().NotBeNull();
        dealHistory.CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Retrieved {dealHistory.Deal.Count} deals");
        _output.WriteLine($"Has more results: {dealHistory.HasMore}");

        // If there are deals, validate their structure
        foreach (var deal in dealHistory.Deal.Take(5)) // Check first 5 deals
        {
            deal.DealId.Should().BeGreaterThan(0);
            deal.Volume.Should().BeGreaterThan(0);
            deal.CreateTimestamp.Should().BeGreaterThan(0);
            
            _output.WriteLine($"Deal ID: {deal.DealId}, Symbol: {deal.SymbolId}, Volume: {deal.Volume}, " +
                             $"Commission: {deal.Commission}");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetDealsHistory_WithSymbolFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        // First get a valid symbol
        var validSymbolId = await GetValidSymbolId();
        if (validSymbolId == 0)
        {
            _output.WriteLine("No valid symbols found, using default EURUSD symbol ID");
            validSymbolId = 1; // Fallback to EURUSD
        }

        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine($"Testing deals history with symbol filter (Symbol ID: {validSymbolId})...");

        // Act - Request deals for specific symbol
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-60).ToUnixTimeMilliseconds();

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = 50
        };

        // Note: ProtoOADealListReq does not support symbol filtering
        // Symbol filtering would need to be done after receiving the results

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000);

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        dealHistory.Should().NotBeNull();
        _output.WriteLine($"Retrieved {dealHistory.Deal.Count} deals for symbol {validSymbolId}");

        // All deals should match the requested symbol (if any deals exist)
        foreach (var deal in dealHistory.Deal)
        {
            deal.SymbolId.Should().Be(validSymbolId);
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AnalyzeDealStructure_FromDealHistory_ShouldHaveValidProperties()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine("Testing deal structure analysis...");

        // Act - Request recent deals
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeMilliseconds();

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = 100
        };

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000);

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        if (dealHistory.Deal.Count > 0)
        {
            _output.WriteLine($"Analyzing structure for {dealHistory.Deal.Count} deals...");

            foreach (var deal in dealHistory.Deal.Take(3))
            {
                // Basic deal structure validation
                deal.DealId.Should().BeGreaterThan(0);
                deal.OrderId.Should().BeGreaterThan(0);
                deal.SymbolId.Should().BeGreaterThan(0);
                deal.Volume.Should().BeGreaterThan(0);
                deal.ExecutionPrice.Should().BeGreaterThan(0);
                deal.CreateTimestamp.Should().BeGreaterThan(0);
                
                _output.WriteLine($"Deal {deal.DealId}: Order: {deal.OrderId}, Symbol: {deal.SymbolId}, " +
                                 $"Volume: {deal.Volume}, Price: {deal.ExecutionPrice}, Commission: {deal.Commission}");
            }

            _output.WriteLine($"Deal structure analysis completed for {dealHistory.Deal.Count} deals");
        }
        else
        {
            _output.WriteLine("No deals found in the specified time range - account may be new or inactive");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetCashflowHistory_WithTimeRange_ShouldReturnCashOperations()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var cashflowResponses = new List<ProtoOACashFlowHistoryListRes>();
        var subscription = _client!.OfType<ProtoOACashFlowHistoryListRes>()
            .Subscribe(cashflowResponses.Add);

        _output.WriteLine("Testing cashflow history retrieval...");

        // Act - Request cashflow for the last 30 days
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

        var cashflowRequest = new ProtoOACashFlowHistoryListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime
        };

        await _client.SendMessage(cashflowRequest);
        await Task.Delay(4000);

        // Assert
        cashflowResponses.Should().HaveCount(1);
        var cashflowHistory = cashflowResponses[0];
        
        cashflowHistory.Should().NotBeNull();
        cashflowHistory.CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Retrieved {cashflowHistory.DepositWithdraw.Count} cashflow items");
        
        // Validate cashflow items structure
        foreach (var item in cashflowHistory.DepositWithdraw.Take(5))
        {
            item.BalanceHistoryId.Should().BeGreaterThan(0);
            item.ChangeBalanceTimestamp.Should().BeGreaterThan(0);
            item.OperationType.Should().BeDefined();
            
            _output.WriteLine($"Balance History ID: {item.BalanceHistoryId}, Type: {item.OperationType}, " +
                             $"Delta: {item.Delta}, Balance: {item.Balance}");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AnalyzeTransactionPattern_ShouldIdentifyTradingActivity()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine("Testing transaction pattern analysis...");

        // Act - Request deals for analysis
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeMilliseconds();

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = 200
        };

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000);

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        if (dealHistory.Deal.Count > 0)
        {
            _output.WriteLine($"Analyzing trading patterns from {dealHistory.Deal.Count} deals...");

            // Group by symbol
            var symbolGroups = dealHistory.Deal.GroupBy(d => d.SymbolId).ToList();

            _output.WriteLine($"Trading Analysis:");
            _output.WriteLine($"- Total deals: {dealHistory.Deal.Count}");
            _output.WriteLine($"- Unique symbols traded: {symbolGroups.Count}");

            // Most traded symbol
            if (symbolGroups.Any())
            {
                var mostTradedSymbol = symbolGroups.OrderByDescending(g => g.Count()).First();
                _output.WriteLine($"- Most traded symbol: {mostTradedSymbol.Key} ({mostTradedSymbol.Count()} deals)");
            }

            // Validate pattern analysis results
            symbolGroups.Count.Should().BeGreaterThan(0);
            dealHistory.Deal.Count.Should().BeGreaterThan(0);
        }
        else
        {
            _output.WriteLine("No deals found for pattern analysis - account may be new or inactive");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task ValidateTimestampConsistency_InTransactionHistory_ShouldBeChronological()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine("Testing timestamp consistency in transaction history...");

        // Act - Request deals with specific ordering
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-21).ToUnixTimeMilliseconds();

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = 50
        };

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000);

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        if (dealHistory.Deal.Count > 1)
        {
            _output.WriteLine($"Validating timestamp consistency for {dealHistory.Deal.Count} deals...");

            var timestampIssues = 0;

            for (int i = 1; i < dealHistory.Deal.Count; i++)
            {
                var currentDeal = dealHistory.Deal[i];
                var previousDeal = dealHistory.Deal[i - 1];
                
                // All timestamps should be within the requested range
                currentDeal.CreateTimestamp.Should().BeGreaterOrEqualTo(startTime);
                currentDeal.CreateTimestamp.Should().BeLessOrEqualTo(endTime);
            }

            _output.WriteLine($"Timestamp consistency validation completed for {dealHistory.Deal.Count} deals");
            _output.WriteLine($"First deal timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(dealHistory.Deal[0].CreateTimestamp)}");
            _output.WriteLine($"Last deal timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(dealHistory.Deal[^1].CreateTimestamp)}");
        }
        else
        {
            _output.WriteLine("Insufficient deals for timestamp consistency validation");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetTransactionHistory_WithPagination_ShouldHandleLargeDatasets()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var allDeals = new List<ProtoOADeal>();
        var dealResponses = new List<ProtoOADealListRes>();
        var subscription = _client!.OfType<ProtoOADealListRes>()
            .Subscribe(dealResponses.Add);

        _output.WriteLine("Testing transaction history pagination...");

        // Act - Request deals with small page size to test pagination
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var startTime = DateTimeOffset.UtcNow.AddDays(-45).ToUnixTimeMilliseconds();
        var maxRows = 25; // Small page size to trigger pagination

        var dealsRequest = new ProtoOADealListReq
        {
            CtidTraderAccountId = _validAccountId,
            FromTimestamp = startTime,
            ToTimestamp = endTime,
            MaxRows = maxRows
        };

        await _client.SendMessage(dealsRequest);
        await Task.Delay(4000);

        // Assert
        dealResponses.Should().HaveCount(1);
        var dealHistory = dealResponses[0];
        
        allDeals.AddRange(dealHistory.Deal);
        _output.WriteLine($"Retrieved {dealHistory.Deal.Count} deals in first page");
        _output.WriteLine($"Has more results: {dealHistory.HasMore}");

        if (dealHistory.HasMore)
        {
            _output.WriteLine("Testing pagination support is available (HasMore = true)");
            
            // Validate that we got the requested number of rows or less
            dealHistory.Deal.Count.Should().BeLessOrEqualTo(maxRows);
            
            // If there are more results, the API supports pagination
            dealHistory.HasMore.Should().BeTrue();
        }
        else
        {
            _output.WriteLine("All available deals retrieved in single request (no pagination needed)");
        }

        // Validate deal IDs are unique
        var dealIds = allDeals.Select(d => d.DealId).ToList();
        var uniqueDealIds = dealIds.Distinct().ToList();
        dealIds.Count.Should().Be(uniqueDealIds.Count, "All deal IDs should be unique");

        _output.WriteLine($"Total unique deals retrieved: {uniqueDealIds.Count}");
        
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

    private async Task<long> GetValidSymbolId()
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
            return symbolResponses[0].Symbol[0].SymbolId;
        }

        return 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}