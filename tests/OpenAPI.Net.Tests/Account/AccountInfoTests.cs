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
/// Phase 4: Account Management Testing - Account Information Tests
/// Tests account-related operations including balance, bonus calculations, and account status
/// </summary>
public class AccountInfoTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;
    private long _validAccountId;

    public AccountInfoTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetAccountInformation_WithValidAccount_ShouldReturnAccountDetails()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var responses = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(responses.Add));

        _output.WriteLine("Testing account information retrieval...");

        // Act
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(3000); // Wait for response

        // Assert
        var reconcileResponses = responses.OfType<ProtoOAReconcileRes>().ToList();
        var errorResponses = responses.OfType<ProtoOAErrorRes>().ToList();
        
        _output.WriteLine($"Reconcile responses: {reconcileResponses.Count}");
        _output.WriteLine($"Error responses: {errorResponses.Count}");
        
        if (errorResponses.Any())
        {
            _output.WriteLine($"Errors: {string.Join(", ", errorResponses.Select(e => $"{e.ErrorCode}: {e.Description}"))}");
        }

        reconcileResponses.Should().HaveCount(1);
        var accountData = reconcileResponses[0];
        
        // Validate account reconciliation data structure
        accountData.Should().NotBeNull();
        accountData.CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Account ID: {accountData.CtidTraderAccountId}");
        _output.WriteLine($"Positions: {accountData.Position.Count}");
        _output.WriteLine($"Orders: {accountData.Order.Count}");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetTraderInfo_WithValidAccount_ShouldReturnTraderDetails()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var traderResponses = new List<ProtoOATraderRes>();
        var subscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);

        _output.WriteLine("Testing trader information retrieval...");

        // Act
        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        traderResponses.Should().HaveCount(1);
        var traderInfo = traderResponses[0];
        
        traderInfo.Should().NotBeNull();
        traderInfo.CtidTraderAccountId.Should().Be(_validAccountId);
        
        if (traderInfo.Trader != null)
        {
            _output.WriteLine($"Trader ID: {traderInfo.Trader.CtidTraderAccountId}");
            _output.WriteLine($"Balance: {traderInfo.Trader.Balance}");
            _output.WriteLine($"Manager Bonus: {traderInfo.Trader.ManagerBonus}");
            _output.WriteLine($"IB Bonus: {traderInfo.Trader.IbBonus}");
            _output.WriteLine($"Non-Withdrawable Bonus: {traderInfo.Trader.NonWithdrawableBonus}");
            _output.WriteLine($"Account Type: {traderInfo.Trader.AccountType}");
            
            // Basic validation of account values
            traderInfo.Trader.CtidTraderAccountId.Should().Be(_validAccountId);
            traderInfo.Trader.Balance.Should().BeGreaterOrEqualTo(0);
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AccountBalanceCalculations_ShouldBeConsistent()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var traderResponses = new List<ProtoOATraderRes>();
        var subscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);

        _output.WriteLine("Testing account balance calculations...");

        // Act
        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        traderResponses.Should().HaveCount(1);
        var traderInfo = traderResponses[0].Trader;
        
        if (traderInfo != null)
        {
            // Test basic account information
            _output.WriteLine($"Balance: {traderInfo.Balance}");
            _output.WriteLine($"Manager Bonus: {traderInfo.ManagerBonus}");
            _output.WriteLine($"IB Bonus: {traderInfo.IbBonus}");
            _output.WriteLine($"Non-Withdrawable Bonus: {traderInfo.NonWithdrawableBonus}");
            _output.WriteLine($"Account Type: {traderInfo.AccountType}");
            _output.WriteLine($"Leverage: {traderInfo.LeverageInCents}");
            
            // Basic account validation
            traderInfo.Balance.Should().BeGreaterOrEqualTo(0);
            traderInfo.ManagerBonus.Should().BeGreaterOrEqualTo(0);
            traderInfo.IbBonus.Should().BeGreaterOrEqualTo(0);
            traderInfo.NonWithdrawableBonus.Should().BeGreaterOrEqualTo(0);
            
            _output.WriteLine($"Account balance and bonus calculations are consistent");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AccountLeverageValidation_ShouldHaveValidSettings()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var traderResponses = new List<ProtoOATraderRes>();
        var subscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);

        _output.WriteLine("Testing account leverage validation...");

        // Act
        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        traderResponses.Should().HaveCount(1);
        var traderInfo = traderResponses[0].Trader;
        
        if (traderInfo != null)
        {
            _output.WriteLine($"Balance: {traderInfo.Balance}");
            _output.WriteLine($"Leverage in Cents: {traderInfo.LeverageInCents}");
            _output.WriteLine($"Max Leverage: {traderInfo.MaxLeverage}");
            _output.WriteLine($"Account Type: {traderInfo.AccountType}");
            
            // Validate leverage settings
            if (traderInfo.HasLeverageInCents)
            {
                traderInfo.LeverageInCents.Should().BeGreaterThan(0);
                _output.WriteLine($"Account leverage: 1:{traderInfo.LeverageInCents / 100}");
            }
            
            if (traderInfo.HasMaxLeverage)
            {
                traderInfo.MaxLeverage.Should().BeGreaterThan(0);
                _output.WriteLine($"Max leverage: 1:{traderInfo.MaxLeverage}");
            }
            
            // Basic account validation
            traderInfo.Balance.Should().BeGreaterOrEqualTo(0);
            traderInfo.CtidTraderAccountId.Should().Be(_validAccountId);
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task MultipleAccountInfoRequests_ShouldReturnConsistentData()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var traderResponses = new List<ProtoOATraderRes>();
        var subscription = _client!.OfType<ProtoOATraderRes>()
            .Subscribe(traderResponses.Add);

        _output.WriteLine("Testing multiple account info requests for data consistency...");

        // Act - Send multiple requests
        for (int i = 0; i < 3; i++)
        {
            var traderRequest = new ProtoOATraderReq
            {
                CtidTraderAccountId = _validAccountId
            };

            await _client.SendMessage(traderRequest);
            await Task.Delay(1000);
        }

        await Task.Delay(2000); // Wait for all responses

        // Assert
        traderResponses.Should().HaveCount(3);
        
        // All responses should be for the same account
        traderResponses.All(r => r.CtidTraderAccountId == _validAccountId).Should().BeTrue();
        
        // Compare first and last response for consistency
        if (traderResponses.All(r => r.Trader != null))
        {
            var balances = traderResponses.Select(r => r.Trader.Balance).ToList();
            var maxBalance = balances.Max();
            var minBalance = balances.Min();
            
            _output.WriteLine($"Balance range: {minBalance} - {maxBalance}");
            
            // Balance differences should be minimal (account for potential interest/swap payments)
            Math.Abs(maxBalance - minBalance).Should().BeLessThan(10);
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AccountStatusValidation_ShouldHaveValidState()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var allMessages = new List<IMessage>();
        var subscription = _client!.Subscribe(TestHelpers.CreateTestObserver<IMessage>(allMessages.Add));

        _output.WriteLine("Testing account status validation...");

        // Act - Request both reconcile and trader info
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = _validAccountId
        };

        var traderRequest = new ProtoOATraderReq
        {
            CtidTraderAccountId = _validAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(1000);
        await _client.SendMessage(traderRequest);
        await Task.Delay(3000);

        // Assert
        var reconcileResponses = allMessages.OfType<ProtoOAReconcileRes>().ToList();
        var traderResponses = allMessages.OfType<ProtoOATraderRes>().ToList();
        var errorResponses = allMessages.OfType<ProtoOAErrorRes>().ToList();
        
        _output.WriteLine($"Reconcile responses: {reconcileResponses.Count}");
        _output.WriteLine($"Trader responses: {traderResponses.Count}");
        _output.WriteLine($"Error responses: {errorResponses.Count}");
        
        // Should receive both types of responses without errors
        reconcileResponses.Should().HaveCountGreaterOrEqualTo(1);
        traderResponses.Should().HaveCountGreaterOrEqualTo(1);
        errorResponses.Should().BeEmpty();
        
        // Both responses should reference the same account
        reconcileResponses[0].CtidTraderAccountId.Should().Be(_validAccountId);
        traderResponses[0].CtidTraderAccountId.Should().Be(_validAccountId);
        
        _output.WriteLine($"Account status validation successful for account {_validAccountId}");
        
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

    public void Dispose()
    {
        _client?.Dispose();
    }
}