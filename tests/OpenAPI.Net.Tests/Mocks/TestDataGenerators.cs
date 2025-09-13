using System;
using System.Collections.Generic;

namespace OpenAPI.Net.Tests.Mocks;

/// <summary>
/// Simplified test data generators focusing on basic functionality
/// </summary>
public static class TestDataGenerators
{
    private static readonly Random _random = new();

    // Order Test Data Generators
    public static ProtoOANewOrderReq CreateMarketOrderRequest(
        long accountId = 1001,
        long symbolId = 1,
        ProtoOATradeSide tradeSide = ProtoOATradeSide.Buy,
        long volume = 100000)
    {
        return new ProtoOANewOrderReq
        {
            CtidTraderAccountId = accountId,
            SymbolId = symbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = tradeSide,
            Volume = volume,
            Comment = $"Test market order {_random.Next(1000, 9999)}"
        };
    }

    public static ProtoOANewOrderReq CreateLimitOrderRequest(
        long accountId = 1001,
        long symbolId = 1,
        ProtoOATradeSide tradeSide = ProtoOATradeSide.Buy,
        long volume = 100000,
        double limitPrice = 1.12000)
    {
        return new ProtoOANewOrderReq
        {
            CtidTraderAccountId = accountId,
            SymbolId = symbolId,
            OrderType = ProtoOAOrderType.Limit,
            TradeSide = tradeSide,
            Volume = volume,
            LimitPrice = limitPrice,
            Comment = $"Test limit order {_random.Next(1000, 9999)}"
        };
    }

    public static ProtoOANewOrderReq CreateOrderWithStopLoss(
        long accountId = 1001,
        long symbolId = 1,
        ProtoOATradeSide tradeSide = ProtoOATradeSide.Buy,
        long volume = 100000,
        double stopLoss = 1.11000)
    {
        return new ProtoOANewOrderReq
        {
            CtidTraderAccountId = accountId,
            SymbolId = symbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = tradeSide,
            Volume = volume,
            StopLoss = stopLoss,
            Comment = $"Test order with SL {_random.Next(1000, 9999)}"
        };
    }

    // Random Value Generators
    public static long GenerateOrderId() => _random.NextInt64(1000000, 9999999);
    public static long GeneratePositionId() => _random.NextInt64(1000000, 9999999);
    public static long GenerateDealId() => _random.NextInt64(1000000, 9999999);
    public static long GenerateValidSymbolId() => _random.Next(1, 100);
    public static long GenerateValidVolume() => _random.Next(1, 10) * 100000;

    public static double GeneratePrice() => Math.Round(1.0 + _random.NextDouble() * 0.5, 5);
    public static double GenerateCommission() => Math.Round(_random.NextDouble() * 5, 2);

    public static ProtoOAOrderType GenerateOrderType()
    {
        var orderTypes = new[] 
        { 
            ProtoOAOrderType.Market, 
            ProtoOAOrderType.Limit, 
            ProtoOAOrderType.Stop, 
            ProtoOAOrderType.StopLimit 
        };
        return orderTypes[_random.Next(orderTypes.Length)];
    }

    public static ProtoOATradeSide GenerateTradeSide()
    {
        return _random.Next(2) == 0 ? ProtoOATradeSide.Buy : ProtoOATradeSide.Sell;
    }

    // Edge Cases and Invalid Data
    public static ProtoOANewOrderReq CreateInvalidOrderRequest(string invalidType = "zero_volume")
    {
        var order = CreateMarketOrderRequest();
        
        switch (invalidType.ToLower())
        {
            case "zero_volume":
                order.Volume = 0;
                break;
            case "negative_volume":
                order.Volume = -100000;
                break;
            case "invalid_symbol":
                order.SymbolId = 999999999;
                break;
            default:
                order.Volume = 0;
                break;
        }
        
        return order;
    }

    public static List<ProtoOANewOrderReq> CreateEdgeCaseOrders()
    {
        return new List<ProtoOANewOrderReq>
        {
            CreateInvalidOrderRequest("zero_volume"),
            CreateInvalidOrderRequest("negative_volume"),
            CreateInvalidOrderRequest("invalid_symbol")
        };
    }
}