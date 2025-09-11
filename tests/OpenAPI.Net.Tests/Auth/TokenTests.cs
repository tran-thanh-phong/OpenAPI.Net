using FluentAssertions;
using OpenAPI.Net.Auth;
using OpenAPI.Net.Tests.TestUtilities;
using System.Text.Json;
using Xunit;
using System;

namespace OpenAPI.Net.Tests.Auth;

public class TokenTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateInstanceWithNullProperties()
    {
        // Arrange & Act
        var token = new Token();

        // Assert
        token.Should().NotBeNull();
        token.AccessToken.Should().BeNull();
        token.RefreshToken.Should().BeNull();
        token.TokenType.Should().BeNull();
        token.ErrorCode.Should().BeNull();
        token.ErrorDescription.Should().BeNull();
        token.ExpiresIn.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void Properties_SetAndGet_ShouldWorkCorrectly()
    {
        // Arrange
        var token = new Token();
        var expiresIn = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        token.AccessToken = TestConstants.TestAccessToken;
        token.RefreshToken = "test_refresh_token";
        token.TokenType = "Bearer";
        token.ExpiresIn = expiresIn;
        token.ErrorCode = "invalid_request";
        token.ErrorDescription = "Test error description";

        // Assert
        token.AccessToken.Should().Be(TestConstants.TestAccessToken);
        token.RefreshToken.Should().Be("test_refresh_token");
        token.TokenType.Should().Be("Bearer");
        token.ExpiresIn.Should().Be(expiresIn);
        token.ErrorCode.Should().Be("invalid_request");
        token.ErrorDescription.Should().Be("Test error description");
    }

    [Fact]
    public void JsonSerialization_ValidToken_ShouldSerializeCorrectly()
    {
        // Arrange
        var token = TestConstants.CreateTestToken();

        // Act
        var json = JsonSerializer.Serialize(token);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("accessToken");
        json.Should().Contain("refreshToken");
        json.Should().Contain("tokenType");
        json.Should().Contain("expiresIn");
    }

    [Fact]
    public void JsonDeserialization_ValidJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var originalToken = TestConstants.CreateTestToken();
        var json = JsonSerializer.Serialize(originalToken);

        // Act
        var deserializedToken = JsonSerializer.Deserialize<Token>(json);

        // Assert
        deserializedToken.Should().NotBeNull();
        deserializedToken!.AccessToken.Should().Be(originalToken.AccessToken);
        deserializedToken.RefreshToken.Should().Be(originalToken.RefreshToken);
        deserializedToken.TokenType.Should().Be(originalToken.TokenType);
    }

    [Fact]
    public void JsonDeserialization_ExpiresInAsSeconds_ShouldConvertToDateTimeOffset()
    {
        // Arrange
        const long expiresInSeconds = 3600; // 1 hour
        var json = $@"{{
            ""accessToken"": ""{TestConstants.TestAccessToken}"",
            ""expiresIn"": {expiresInSeconds},
            ""tokenType"": ""Bearer""
        }}";

        var beforeDeserialization = DateTimeOffset.UtcNow;

        // Act
        var token = JsonSerializer.Deserialize<Token>(json);

        // Assert
        token.Should().NotBeNull();
        token!.AccessToken.Should().Be(TestConstants.TestAccessToken);
        token.TokenType.Should().Be("Bearer");
        
        // ExpiresIn should be approximately now + 1 hour
        var expectedExpiry = beforeDeserialization.AddSeconds(expiresInSeconds);
        token.ExpiresIn.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void JsonSerialization_ExpiresIn_ShouldSerializeAsSecondsFromNow()
    {
        // Arrange
        var token = new Token
        {
            AccessToken = TestConstants.TestAccessToken,
            ExpiresIn = DateTimeOffset.UtcNow.AddHours(1),
            TokenType = "Bearer"
        };

        // Act
        var json = JsonSerializer.Serialize(token);
        var deserializedJson = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        deserializedJson.TryGetProperty("expiresIn", out var expiresInElement).Should().BeTrue();
        var expiresInValue = expiresInElement.GetInt64();
        
        // Should be approximately 3600 seconds (1 hour)
        expiresInValue.Should().BeInRange(3500, 3700);
    }

    [Fact]
    public void JsonDeserialization_WithErrorFields_ShouldDeserializeErrorInfo()
    {
        // Arrange
        var json = @"{
            ""errorCode"": ""invalid_grant"",
            ""description"": ""The provided authorization grant is invalid""
        }";

        // Act
        var token = JsonSerializer.Deserialize<Token>(json);

        // Assert
        token.Should().NotBeNull();
        token!.ErrorCode.Should().Be("invalid_grant");
        token.ErrorDescription.Should().Be("The provided authorization grant is invalid");
        token.AccessToken.Should().BeNull();
    }

    [Fact]
    public void JsonPropertyNames_ShouldMatchExpectedApiFormat()
    {
        // Arrange
        var token = new Token
        {
            AccessToken = "test_access_token",
            RefreshToken = "test_refresh_token",
            TokenType = "Bearer",
            ExpiresIn = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        var json = JsonSerializer.Serialize(token);
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert - verify the JSON property names match the expected API format
        jsonElement.TryGetProperty("accessToken", out _).Should().BeTrue();
        jsonElement.TryGetProperty("refreshToken", out _).Should().BeTrue();
        jsonElement.TryGetProperty("tokenType", out _).Should().BeTrue();
        jsonElement.TryGetProperty("expiresIn", out _).Should().BeTrue();
        
        // These should NOT exist (camelCase, not PascalCase)
        jsonElement.TryGetProperty("AccessToken", out _).Should().BeFalse();
        jsonElement.TryGetProperty("RefreshToken", out _).Should().BeFalse();
    }

    [Fact]
    public void JsonDeserialization_EmptyJson_ShouldCreateTokenWithDefaults()
    {
        // Arrange
        var json = "{}";

        // Act
        var token = JsonSerializer.Deserialize<Token>(json);

        // Assert
        token.Should().NotBeNull();
        token!.AccessToken.Should().BeNull();
        token.RefreshToken.Should().BeNull();
        token.TokenType.Should().BeNull();
        token.ErrorCode.Should().BeNull();
        token.ErrorDescription.Should().BeNull();
        token.ExpiresIn.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void JsonDeserialization_NullValues_ShouldHandleGracefully()
    {
        // Arrange
        var json = @"{
            ""accessToken"": null,
            ""refreshToken"": null,
            ""tokenType"": null,
            ""errorCode"": null,
            ""description"": null
        }";

        // Act
        var token = JsonSerializer.Deserialize<Token>(json);

        // Assert
        token.Should().NotBeNull();
        token!.AccessToken.Should().BeNull();
        token.RefreshToken.Should().BeNull();
        token.TokenType.Should().BeNull();
        token.ErrorCode.Should().BeNull();
        token.ErrorDescription.Should().BeNull();
    }

    [Theory]
    [InlineData(0)] // Immediate expiry
    [InlineData(3600)] // 1 hour
    [InlineData(86400)] // 24 hours
    [InlineData(604800)] // 1 week
    public void TokenExpiryConverter_DifferentExpiryTimes_ShouldConvertCorrectly(long secondsFromNow)
    {
        // Arrange
        var json = $@"{{
            ""accessToken"": ""test_token"",
            ""expiresIn"": {secondsFromNow}
        }}";

        var beforeDeserialization = DateTimeOffset.UtcNow;

        // Act
        var token = JsonSerializer.Deserialize<Token>(json);

        // Assert
        token.Should().NotBeNull();
        var expectedExpiry = beforeDeserialization.AddSeconds(secondsFromNow);
        token!.ExpiresIn.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateTestToken_Helper_ShouldCreateValidToken()
    {
        // Act
        var token = TestConstants.CreateTestToken();

        // Assert
        token.Should().NotBeNull();
        token.AccessToken.Should().Be(TestConstants.TestAccessToken);
        token.TokenType.Should().Be("Bearer");
        token.RefreshToken.Should().Be("test_refresh_token");
        token.ExpiresIn.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}