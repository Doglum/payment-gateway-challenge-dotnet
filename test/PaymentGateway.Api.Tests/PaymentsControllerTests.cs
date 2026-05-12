using System.Net;
using System.Net.Http.Json;
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
    // helper method to generate a standard valid request
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
        // use concrete by default, logic can be tested easily without many issues
        repo ??= new PaymentsRepository();
        validator ??= new PostPaymentRequestValidator();
        // use mock by default, avoids slamming mountebank
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

        var response = await BuildClient(repo: repo)
            .GetAsync($"/api/Payments/{payment.Id}");
        var body = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

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
        var response = await BuildClient()
            .GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_DoesNotReturnAuthorizationCode()
    {
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

        var response = await BuildClient(repo: repo)
            .GetAsync($"/api/Payments/{payment.Id}");
        var body = await response.Content.ReadFromJsonAsync<GetPaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(body!.GetType().GetProperty("AuthorizationCode")?.GetValue(body));
    }

    // ---------------------------------------------------------------------------
    // POST /api/Payments — validation (Rejected path)
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]                     // missing
    [InlineData("")]                       // empty
    [InlineData("123")]                    // too short (< 14 digits)
    [InlineData("123456789012345678901")]  // too long (> 19 digits)
    [InlineData("abcd1234abcd12")]         // non-numeric
    public async Task CreatePayment_Returns400_WhenCardNumberIsInvalid(string? cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber!;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]   // below range
    [InlineData(13)]  // above range
    public async Task CreatePayment_Returns400_WhenExpiryMonthIsInvalid(int month)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns400_WhenExpiryDateIsInThePast()
    {
        var request = ValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;
        request.ExpiryMonth = 1;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns400_WhenExpiryMonthHasPassedThisYear()
    {
        var request = ValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year;
        request.ExpiryMonth = DateTime.UtcNow.Month - 1;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]    // not set
    [InlineData("")]      // blank
    [InlineData("US")]    // too short
    [InlineData("GBPX")]  // too long
    [InlineData("XYZ")]   // not in allowed list
    public async Task CreatePayment_Returns400_WhenCurrencyIsInvalid(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]     // not set
    [InlineData("")]       // blank
    [InlineData("12")]     // too short
    [InlineData("12345")]  // too long
    [InlineData("12a")]    // non-numeric
    public async Task CreatePayment_Returns400_WhenCvvIsInvalid(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // POST /api/Payments — bank responses (Authorized / Declined)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreatePayment_Returns201WithAuthorizedStatus_WhenBankAuthorizes()
    {
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse
            {
                Authorized = true,
                AuthorizationCode = Guid.NewGuid().ToString()
            });

        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Authorized, body.Status);
        Assert.NotNull(body.AuthorizationCode);
        Assert.Equal("8877", body.CardNumberLastFour);  // last 4 of ValidRequest card
    }

    [Fact]
    public async Task CreatePayment_Returns201WithDeclinedStatus_WhenBankDeclines()
    {
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ReturnsAsync(new BankPaymentResponse { Authorized = false });

        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());
        var body = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Declined, body.Status);
        Assert.Null(body.AuthorizationCode);  // not returned on decline
    }

    [Fact]
    public async Task CreatePayment_StoresPayment_SoItCanBeRetrieved()
    {
        // Proves the full round-trip: POST stores it, GET retrieves it
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

        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var created = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>();

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/Payments/{created!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>();

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
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable));

        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayment_Returns500_WhenBankThrowsUnexpectedException()
    {
        var bankClient = new Mock<IBankClient>();
        bankClient
            .Setup(b => b.ProcessPaymentAsync(It.IsAny<PostPaymentRequest>()))
            .ThrowsAsync(new HttpRequestException("unexpected", null, HttpStatusCode.InternalServerError));

        var response = await BuildClient(bankClient: bankClient.Object)
            .PostAsJsonAsync("/api/Payments", ValidRequest());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}