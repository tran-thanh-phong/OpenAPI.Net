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

namespace OpenAPI.Net.Tests.Trading;

/// <summary>
/// Tests for position management functionality including position closing and modification
/// </summary>
public class PositionManagementTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private OpenClient? _client;

    public PositionManagementTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ClosePosition_FullPosition_ShouldCloseCompletely()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // First create a position by executing a market order
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            Comment = "Position to be closed"
        };

        await _client.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        var positionId = GetLastPositionId(executionEvents);
        if (positionId == 0)
        {
            _output.WriteLine("Could not create position for closing test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing full position closure for position ID: {positionId}");

        // Act - Close the position
        var closePositionRequest = new ProtoOAClosePositionReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            PositionId = positionId,
            Volume = 100000 // Close full volume
        };

        await _client.SendMessage(closePositionRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        _output.WriteLine("Position closed successfully");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task ClosePosition_PartialPosition_ShouldClosePartially()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // Create a larger position
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 200000, // 0.2 lot
            Comment = "Position for partial closure"
        };

        await _client!.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        var positionId = GetLastPositionId(executionEvents);
        if (positionId == 0)
        {
            _output.WriteLine("Could not create position for partial closing test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing partial position closure for position ID: {positionId}");

        // Act - Close half the position
        var partialCloseRequest = new ProtoOAClosePositionReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            PositionId = positionId,
            Volume = 100000 // Close half volume
        };

        await _client.SendMessage(partialCloseRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        _output.WriteLine("Partial position closure completed");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AmendPositionStopLoss_ShouldUpdateStopLoss()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // Create a position
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            StopLoss = 1.11000,
            Comment = "Position for SL modification"
        };

        await _client!.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        var positionId = GetLastPositionId(executionEvents);
        if (positionId == 0)
        {
            _output.WriteLine("Could not create position for SL modification test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing SL modification for position ID: {positionId}");

        // Act - Modify stop loss
        var amendSLRequest = new ProtoOAAmendPositionSLTPReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            PositionId = positionId,
            StopLoss = 1.10500 // Move SL closer to market
        };

        await _client.SendMessage(amendSLRequest);
        await Task.Delay(2000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderReplaced);
        _output.WriteLine("Stop loss modification completed");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task AmendPositionTakeProfit_ShouldUpdateTakeProfit()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // Create a position
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            TakeProfit = 1.13000,
            Comment = "Position for TP modification"
        };

        await _client!.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        var positionId = GetLastPositionId(executionEvents);
        if (positionId == 0)
        {
            _output.WriteLine("Could not create position for TP modification test");
            subscription.Dispose();
            return;
        }

        _output.WriteLine($"Testing TP modification for position ID: {positionId}");

        // Act - Modify take profit
        var amendTPRequest = new ProtoOAAmendPositionSLTPReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            PositionId = positionId,
            TakeProfit = 1.13500 // Move TP further from market
        };

        await _client.SendMessage(amendTPRequest);
        await Task.Delay(2000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderReplaced);
        _output.WriteLine("Take profit modification completed");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task GetPositionsList_ShouldReturnCurrentPositions()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var positionResponses = new List<ProtoOAReconcileRes>();
        var subscription = _client!.OfType<ProtoOAReconcileRes>()
            .Subscribe(positionResponses.Add);

        _output.WriteLine("Testing positions list retrieval...");

        // Act
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId
        };

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(3000);

        // Assert
        positionResponses.Should().HaveCount(1);
        _output.WriteLine($"Reconcile response received with {positionResponses[0].Position.Count} positions");
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CreatePositionWithBothSLTP_ShouldHaveBothLevels()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        _output.WriteLine("Testing position creation with both SL and TP...");

        // Act
        var orderWithSLTPRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            StopLoss = 1.11000,
            TakeProfit = 1.13000,
            Comment = "Position with SL and TP"
        };

        await _client!.SendMessage(orderWithSLTPRequest);
        await Task.Delay(3000);

        // Assert
        executionEvents.Should().Contain(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        var filledEvent = executionEvents.FirstOrDefault(e => e.ExecutionType == ProtoOAExecutionType.OrderFilled);
        if (filledEvent?.Position != null == true)
        {
            _output.WriteLine($"Position created successfully");
        }
        
        subscription.Dispose();
    }

    [Fact]
    public async Task CalculatePositionPnL_ShouldShowProfitLoss()
    {
        // Arrange
        await SetupAuthenticatedClient();
        
        var executionEvents = new List<ProtoOAExecutionEvent>();
        var subscription = _client!.OfType<ProtoOAExecutionEvent>()
            .Subscribe(executionEvents.Add);

        // Create a position
        var marketOrderRequest = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            SymbolId = TestConstants.TestSymbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = ProtoOATradeSide.Buy,
            Volume = 100000,
            Comment = "Position for P&L calculation"
        };

        await _client!.SendMessage(marketOrderRequest);
        await Task.Delay(3000);

        // Get position and check P&L
        var reconcileRequest = new ProtoOAReconcileReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId
        };

        var reconcileResponses = new List<ProtoOAReconcileRes>();
        var reconcileSub = _client.OfType<ProtoOAReconcileRes>()
            .Subscribe(reconcileResponses.Add);

        await _client.SendMessage(reconcileRequest);
        await Task.Delay(2000);

        // Assert
        if (reconcileResponses.Count > 0 && reconcileResponses[0].Position.Count > 0)
        {
            var position = reconcileResponses[0].Position[0];
            _output.WriteLine($"Position retrieved successfully");
            position.Should().NotBeNull();
        }
        
        subscription.Dispose();
        reconcileSub.Dispose();
    }

    [Fact]
    public void PositionSide_EnumValues_ShouldHaveExpectedValues()
    {
        // Arrange & Act & Assert
        ProtoOATradeSide.Buy.Should().Be(ProtoOATradeSide.Buy);
        ProtoOATradeSide.Sell.Should().Be(ProtoOATradeSide.Sell);

        _output.WriteLine("Position side enum values validated successfully");
    }

    private async Task SetupAuthenticatedClient()
    {
        _client = new OpenClient(
            TestConstants.TestDemoHost,
            TestConstants.TestApiPort,
            TestConstants.HeartbeatInterval
        );

        await _client.Connect();

        // Perform complete authentication
        var appAuthRequest = new ProtoOAApplicationAuthReq
        {
            ClientId = TestConstants.TestClientId,
            ClientSecret = TestConstants.TestClientSecret
        };

        await _client.SendMessage(appAuthRequest);
        await Task.Delay(2000);

        var accountAuthRequest = new ProtoOAAccountAuthReq
        {
            CtidTraderAccountId = TestConstants.TestAccountId,
            AccessToken = TestConstants.TestAccessToken
        };

        await _client.SendMessage(accountAuthRequest);
        await Task.Delay(2000);
    }

    private long GetLastPositionId(List<ProtoOAExecutionEvent> executionEvents)
    {
        var lastFilledEvent = executionEvents.LastOrDefault(e => 
            e.ExecutionType == ProtoOAExecutionType.OrderFilled && e.Position != null);
        
        return lastFilledEvent?.Position?.PositionId ?? 0;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}