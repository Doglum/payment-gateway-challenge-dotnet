using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;

using Moq;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Models.Validation;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 12,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "GBP",
        Amount = 1050,
        Cvv = "123"
    };

    private static HttpClient BuildClient(
        IPaymentsRepository? repo = null,
        IPostPaymentRequestValidator? validator = null,
        IBankClient? bankClient = null)
    {
        repo ??= new PaymentsRepository();
        validator ??= new PostPaymentRequestValidator();
        bankClient ??= Mock.Of<IBankClient>();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IPaymentsRepository>(repo);
                    services.AddSingleton<IPostPaymentRequestValidator>(validator);
                    services.AddSingleton<IBankClient>(bankClient);
                }))
            .CreateClient();
    }

    // ---------------------------------------------------------------------------
    // GET /api/Payments/{id}
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetPayment_ReturnsPayment_WhenItExists()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 12,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050
        };

        var repo = new PaymentsRepository();
        repo.Add(payment);

        // Act
        var response = await BuildClient(repo: repo)
            .GetAsync($"/api/Payments/{payment.Id}");
        var body = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(payment.Id, body.Id);
        Assert.Equal(payment.Amount, body.Amount);
        Assert.Equal(payment.Currency, body.Currency);
        Assert.Equal(payment.CardNumberLastFour, body.CardNumberLastFour);
        Assert.Equal(payment.Status, body.Status);
    }

    [Fact]
    public async Task GetPayment_Returns404_WhenPaymentDoesNotExist()
    {
        // Arrange
        var client = BuildClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_DoesNotReturnAuthorizationCode()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = "8877",
            ExpiryMonth = 12,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            AuthorizationCode = Guid.NewGuid().ToString()
        };

        var repo = new PaymentsRepository();
        repo.Add(payment);

        // Act
        var response = await BuildClient(repo: repo)
            .GetAsync($"/api/Payments/{payment.Id}");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(json.RootElement.TryGetProperty("authorizationCode", out _),
            "Response should not contain authorizationCode");
    }

    // ---------------------------------------------------------------------------
    // POST /api/Payments — validation (Rejected path)
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789012345678901")]
    [InlineData("abcd1234abcd12")]
    public async Task CreatePayment_Returns400_WhenCardNumberIsInvalid(string? cardNumber)
    {
        // Arrange
        var request = ValidRequest();
        request.CardNumber = cardNumber!;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task CreatePayment_Returns400_WhenExpiryMonthIsInvalid(int month)
    {
        // Arrange
        var request = ValidRequest();
        request.ExpiryMonth = month;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns400_WhenExpiryDateIsInThePast()
    {
        // Arrange
        var request = ValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;
        request.ExpiryMonth = 1;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns400_WhenExpiryMonthHasPassedThisYear()
    {
        // Arrange
        var request = ValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year;
        request.ExpiryMonth = DateTime.UtcNow.Month - 1;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("GBPX")]
    [InlineData("XYZ")]
    public async Task CreatePayment_Returns400_WhenCurrencyIsInvalid(string currency)
    {
        // Arrange
        var request = ValidRequest();
        request.Currency = currency;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    public async Task CreatePayment_Returns400_WhenCvvIsInvalid(string cvv)
    {
        // Arrange
        var request = ValidRequest();
        request.Cvv = cvv;

        // Act
        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // POST /api/Payments — bank responses (Authorized / Declined)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreatePayment_Returns201WithAuthorizedStatus_WhenBankAuthorizes()
    {
        // Arrange
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse
            {
                Authorized = true,
                AuthorizationCode = Guid.NewGuid().ToString()
            });

        // Act
        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Authorized, body.Status);
        Assert.NotNull(body.AuthorizationCode);
        Assert.Equal("8877", body.CardNumberLastFour);
    }

    [Fact]
    public async Task CreatePayment_Returns201WithDeclinedStatus_WhenBankDeclines()
    {
        // Arrange
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false });

        // Act
        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Declined, body.Status);
        Assert.Null(body.AuthorizationCode);
    }

    [Fact]
    public async Task CreatePayment_StoresPayment_SoItCanBeRetrieved()
    {
        // Arrange
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse
            {
                Authorized = true,
                AuthorizationCode = Guid.NewGuid().ToString()
            });

        var repo = new PaymentsRepository();
        var client = BuildClient(repo: repo, bankClient: bankClient.Object);

        // Act
        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();
        var getResponse = await client.GetAsync($"/api/Payments/{created!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, retrieved!.Id);
        Assert.Equal(created.Amount, retrieved.Amount);
    }

    // ---------------------------------------------------------------------------
    // POST /api/Payments — bank error handling
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreatePayment_Returns503_WhenBankIsUnavailable()
    {
        // Arrange
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));

        // Act
        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns500_WhenBankThrowsUnexpectedException()
    {
        // Arrange
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("unexpected", null, HttpStatusCode.InternalServerError));

        // Act
        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}