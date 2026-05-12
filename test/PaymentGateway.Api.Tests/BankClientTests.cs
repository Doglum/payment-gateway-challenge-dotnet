using System.Net;
using System.Text.Json;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;
using RichardSzalay.MockHttp;

namespace PaymentGateway.Api.Tests;

public class BankClientTests
{
    private PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2025,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static BankClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:8080");
        return new BankClient(httpClient);
    }

    // ---------------------------------------------------------------------------
    // Request mapping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPaymentAsync_MapsRequestFieldsCorrectly()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var request = ValidRequest();

        mockHttp.Expect(HttpMethod.Post, "http://localhost:8080/payments")
            .WithPartialContent("2222405343248877")
            .WithPartialContent("04/2025")           
            .WithPartialContent("GBP")
            .WithPartialContent("100")
            .WithPartialContent("123")
            .Respond(HttpStatusCode.OK, "application/json",
                JsonSerializer.Serialize(new { authorized = true, authorization_code = "abc-123" }));

        var client = BuildClient(mockHttp);

        // Act
        await client.ProcessPaymentAsync(request);

        // Assert
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task ProcessPaymentAsync_FormatsExpiryDateAsTwoDigitMonth()
    {
        var mockHttp = new MockHttpMessageHandler();
        var request = ValidRequest();
        request.ExpiryMonth = 4;

        mockHttp.Expect(HttpMethod.Post, "http://localhost:8080/payments")
            .WithPartialContent("04/2025")
            .Respond(HttpStatusCode.OK, "application/json",
                JsonSerializer.Serialize(new { authorized = true, authorization_code = "abc-123" }));

        var client = BuildClient(mockHttp);

        // Act
        await client.ProcessPaymentAsync(request);

        // Assert
        mockHttp.VerifyNoOutstandingExpectation();
    }

    // ---------------------------------------------------------------------------
    // Response mapping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPaymentAsync_ReturnsAuthorizedResponse_WhenBankAuthorizes()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var authCode = Guid.NewGuid().ToString();

        mockHttp.When(HttpMethod.Post, "http://localhost:8080/payments")
            .Respond(HttpStatusCode.OK, "application/json",
                JsonSerializer.Serialize(new { authorized = true, authorization_code = authCode }));

        var client = BuildClient(mockHttp);

        // Act
        var result = await client.ProcessPaymentAsync(ValidRequest());

        // Assert
        Assert.True(result.Authorized);
        Assert.Equal(authCode, result.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ReturnsUnauthorizedResponse_WhenBankDeclines()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Post, "http://localhost:8080/payments")
            .Respond(HttpStatusCode.OK, "application/json",
                JsonSerializer.Serialize(new { authorized = false, authorization_code = (string?)null }));

        var client = BuildClient(mockHttp);

        // Act
        var result = await client.ProcessPaymentAsync(ValidRequest());

        // Assert
        Assert.False(result.Authorized);
        Assert.Null(result.AuthorizationCode);
    }

    // ---------------------------------------------------------------------------
    // Error handling
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ProcessPaymentAsync_ThrowsHttpRequestException_WhenBankReturnsBadRequest()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Post, "http://localhost:8080/payments")
            .Respond(HttpStatusCode.BadRequest);

        var client = BuildClient(mockHttp);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ProcessPaymentAsync(ValidRequest()));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task ProcessPaymentAsync_ThrowsHttpRequestException_WhenBankReturns503()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When(HttpMethod.Post, "http://localhost:8080/payments")
            .Respond(HttpStatusCode.ServiceUnavailable);

        var client = BuildClient(mockHttp);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ProcessPaymentAsync(ValidRequest()));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }
}