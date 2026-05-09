using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentsRepository
{
    public List<PostPaymentResponse> Payments = new();
    
    public void Add(PostPaymentResponse payment)
    {
        Payments.Add(payment);
    }

    public GetPaymentResponse? Get(Guid id)
    {
        var paymentDetails = Payments.FirstOrDefault(p => p.Id == id);

        //converting to getPaymentResponse as we don't want to return authorization code as it's potentially sensitive and not specified as needed
        if (paymentDetails != null)
        {
            return new GetPaymentResponse
            {
                Id = paymentDetails.Id,
                Status = paymentDetails.Status,
                CardNumberLastFour = paymentDetails.CardNumberLastFour,
                ExpiryMonth = paymentDetails.ExpiryMonth,
                ExpiryYear = paymentDetails.ExpiryYear,
                Currency = paymentDetails.Currency,
                Amount = paymentDetails.Amount
            };
        }

        return null;
    }
}