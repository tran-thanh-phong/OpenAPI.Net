using OpenAPI.Net.Auth;
using System;

namespace OpenAPI.Net.Tests.Utilities;

public static class TestConstants
{
    // Test API Credentials (provided by user)
    public const string TestClientId = "";
    public const string TestClientSecret = "";
    public const string TestAccessToken = "";
    
    // Test API Endpoints
    public const string TestDemoHost = "demo.ctraderapi.com";
    public const string TestLiveHost = "live.ctraderapi.com";
    public const int TestApiPort = 5035;
    
    // Test Account Data
    public const long TestAccountId = 15084071; // Demo account
    public const long TestSymbolId = 1; // EURUSD
    public const long TestPositionId = 123456;
    public const long TestOrderId = 789012;
    
    // Test Timeouts and Intervals
    public static readonly TimeSpan DefaultTestTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);
    
    // Test Message Data
    public const string TestClientMessageId = "TestMessage_123";
    public const string TestOrderLabel = "TestOrder";
    public const string TestOrderComment = "Test order comment";
    
    // Test Volume and Prices (in raw API format)
    public const long TestVolume = 100000; // 0.01 lot
    public const double TestPrice = 1.12345;
    public const double TestStopLoss = 1.12000;
    public const double TestTakeProfit = 1.13000;
    
    // Test App Configuration
    public static App CreateTestApp(string redirectUri = "http://localhost:8080/callback")
    {
        return new App(TestClientId, TestClientSecret, redirectUri);
    }
    
    // Test Token Configuration
    public static Token CreateTestToken()
    {
        return new Token
        {
            AccessToken = TestAccessToken,
            TokenType = "Bearer",
            ExpiresIn = DateTimeOffset.UtcNow.AddHours(1),
            RefreshToken = "test_refresh_token"
        };
    }
}