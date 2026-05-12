using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using Xunit;

namespace PaymentGateway.Api.Tests;

public class PaymentsRepositoryTests
{
    private PostPaymentResponse BuildPayment(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = "8877",
        ExpiryMonth = 12,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 1050,
        AuthorizationCode = Guid.NewGuid().ToString()
    };

    // ---------------------------------------------------------------------------
    // Add
    // ---------------------------------------------------------------------------

    [Fact]
    public void Add_StoresPayment_SoItCanBeRetrieved()
    {
        // Arrange
        var repo = new PaymentsRepository();
        var payment = BuildPayment();

        // Act
        repo.Add(payment);

        // Assert
        Assert.Single(repo.Payments);
    }

    [Fact]
    public void Add_ThrowsInvalidOperationException_WhenDuplicateIdAdded()
    {
        // Arrange
        var repo = new PaymentsRepository();
        var payment = BuildPayment();
        repo.Add(payment);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => repo.Add(payment));
        Assert.Contains(payment.Id.ToString(), ex.Message);
    }

    [Fact]
    public void Add_AllowsMultiplePayments_WithDifferentIds()
    {
        // Arrange
        var repo = new PaymentsRepository();

        // Act
        repo.Add(BuildPayment());
        repo.Add(BuildPayment());
        repo.Add(BuildPayment());

        // Assert
        Assert.Equal(3, repo.Payments.Count);
    }

    // ---------------------------------------------------------------------------
    // Get
    // ---------------------------------------------------------------------------

    [Fact]
    public void Get_ReturnsPayment_WhenItExists()
    {
        // Arrange
        var repo = new PaymentsRepository();
        var payment = BuildPayment();
        repo.Add(payment);

        // Act
        var result = repo.Get(payment.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(payment.Id, result.Id);
        Assert.Equal(payment.Status, result.Status);
        Assert.Equal(payment.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
        Assert.Equal(payment.Currency, result.Currency);
        Assert.Equal(payment.Amount, result.Amount);
    }

    [Fact]
    public void Get_ReturnsNull_WhenPaymentDoesNotExist()
    {
        // Arrange
        var repo = new PaymentsRepository();

        // Act
        var result = repo.Get(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsNull_AfterDifferentPaymentAdded()
    {
        // Arrange
        var repo = new PaymentsRepository();
        repo.Add(BuildPayment());

        // Act
        var result = repo.Get(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsGetPaymentResponse_NotPostPaymentResponse()
    {
        // Arrange
        var repo = new PaymentsRepository();
        var payment = BuildPayment();
        repo.Add(payment);

        // Act
        var result = repo.Get(payment.Id);

        // Assert
        Assert.IsType<GetPaymentResponse>(result);
    }

    [Fact]
    public void Get_DoesNotExposeAuthorizationCode()
    {
        // Arrange
        var repo = new PaymentsRepository();
        var payment = BuildPayment();
        repo.Add(payment);

        // Act
        var result = repo.Get(payment.Id);

        // Assert
        var property = result!.GetType().GetProperty("AuthorizationCode");
        Assert.Null(property);
    }
}