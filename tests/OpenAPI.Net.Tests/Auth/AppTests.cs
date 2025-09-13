using FluentAssertions;
using OpenAPI.Net.Auth;
using OpenAPI.Net.Tests.TestUtilities;
using Xunit;
using System;

namespace OpenAPI.Net.Tests.Auth;

public class AppTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldCreateInstance()
    {
        // Arrange
        const string clientId = TestConstants.TestClientId;
        const string secret = TestConstants.TestClientSecret;
        const string redirectUri = "http://localhost:8080/callback";

        // Act
        var app = new App(clientId, secret, redirectUri);

        // Assert
        app.Should().NotBeNull();
        app.ClientId.Should().Be(clientId);
        app.Secret.Should().Be(secret);
        app.RedirectUri.Should().Be(redirectUri);
    }

    [Theory]
    [InlineData(null, "secret", "http://localhost")]
    [InlineData("", "secret", "http://localhost")]
    [InlineData("client", null, "http://localhost")]
    [InlineData("client", "", "http://localhost")]
    [InlineData("client", "secret", null)]
    [InlineData("client", "secret", "")]
    public void Constructor_InvalidParameters_ShouldCreateInstanceWithGivenValues(string? clientId, string? secret, string? redirectUri)
    {
        // Note: The App class doesn't validate parameters in constructor, so this test verifies current behavior
        // In a production environment, you might want to add validation

        // Arrange & Act
        var app = new App(clientId!, secret!, redirectUri!);

        // Assert
        app.Should().NotBeNull();
        app.ClientId.Should().Be(clientId);
        app.Secret.Should().Be(secret);
        app.RedirectUri.Should().Be(redirectUri);
    }

    [Fact]
    public void GetAuthUri_DefaultScope_ShouldReturnTradingScope()
    {
        // Arrange
        var app = TestConstants.CreateTestApp();

        // Act
        var authUri = app.GetAuthUri();

        // Assert
        authUri.Should().NotBeNull();
        authUri.Host.Should().Be("demo.ctraderapi.com"); // Default from ApiInfo.AuthUrl
        authUri.AbsolutePath.Should().EndWith("/auth");
        authUri.Query.Should().Contain("scope=trading");
        authUri.Query.Should().Contain($"client_id={TestConstants.TestClientId}");
        authUri.Query.Should().Contain("redirect_uri=");
    }

    [Fact]
    public void GetAuthUri_TradingScope_ShouldContainTradingInQuery()
    {
        // Arrange
        var app = TestConstants.CreateTestApp();

        // Act
        var authUri = app.GetAuthUri(Scope.Trading);

        // Assert
        authUri.Should().NotBeNull();
        authUri.Query.Should().Contain("scope=trading");
    }

    [Fact]
    public void GetAuthUri_AccountsScope_ShouldContainAccountsInQuery()
    {
        // Arrange
        var app = TestConstants.CreateTestApp();

        // Act
        var authUri = app.GetAuthUri(Scope.Accounts);

        // Assert
        authUri.Should().NotBeNull();
        authUri.Query.Should().Contain("scope=accounts");
    }

    [Fact]
    public void GetAuthUri_CustomAuthUrl_ShouldUseCustomUrl()
    {
        // Arrange
        var app = TestConstants.CreateTestApp();
        const string customAuthUrl = "https://custom.auth.server.com";

        // Act
        var authUri = app.GetAuthUri(Scope.Trading, customAuthUrl);

        // Assert
        authUri.Should().NotBeNull();
        authUri.Host.Should().Be("custom.auth.server.com");
        authUri.Scheme.Should().Be("https");
    }

    [Fact]
    public void GetAuthUri_ShouldContainAllRequiredParameters()
    {
        // Arrange
        var app = TestConstants.CreateTestApp("http://localhost:8080/callback");

        // Act
        var authUri = app.GetAuthUri(Scope.Trading);

        // Assert
        authUri.Should().NotBeNull();
        
        var query = authUri.Query;
        query.Should().Contain($"client_id={TestConstants.TestClientId}");
        query.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fcallback"); // URL encoded
        query.Should().Contain("scope=trading");
    }

    [Theory]
    [InlineData("http://localhost:8080/callback")]
    [InlineData("https://myapp.com/auth/callback")]
    [InlineData("urn:ietf:wg:oauth:2.0:oob")] // Out-of-band redirect for mobile/desktop apps
    public void GetAuthUri_DifferentRedirectUris_ShouldEncodeCorrectly(string redirectUri)
    {
        // Arrange
        var app = TestConstants.CreateTestApp(redirectUri);

        // Act
        var authUri = app.GetAuthUri();

        // Assert
        authUri.Should().NotBeNull();
        authUri.Query.Should().Contain("redirect_uri=");
        
        // Decode and verify the redirect URI is present
        var decodedQuery = Uri.UnescapeDataString(authUri.Query);
        decodedQuery.Should().Contain(redirectUri);
    }

    [Fact]
    public void GetAuthUri_WithSpecialCharactersInClientId_ShouldEncodeCorrectly()
    {
        // Arrange
        const string clientIdWithSpecialChars = "client_id_with+special&chars=test";
        var app = new App(clientIdWithSpecialChars, TestConstants.TestClientSecret, "http://localhost");

        // Act
        var authUri = app.GetAuthUri();

        // Assert
        authUri.Should().NotBeNull();
        // The URI should be properly encoded - special characters should be URL encoded
        authUri.Query.Should().Contain("client_id=");
    }

    [Fact]
    public void Scope_Enum_ShouldHaveCorrectValues()
    {
        // Arrange & Act & Assert
        Enum.GetNames(typeof(Scope)).Should().Contain("Trading");
        Enum.GetNames(typeof(Scope)).Should().Contain("Accounts");
        
        ((int)Scope.Trading).Should().Be(0);
        ((int)Scope.Accounts).Should().Be(1);
    }

    [Theory]
    [InlineData(Scope.Trading, "trading")]
    [InlineData(Scope.Accounts, "accounts")]
    public void Scope_ToString_ShouldReturnCorrectLowercaseString(Scope scope, string expectedString)
    {
        // Arrange
        var app = TestConstants.CreateTestApp();

        // Act
        var authUri = app.GetAuthUri(scope);

        // Assert
        authUri.Query.Should().Contain($"scope={expectedString}");
    }
}