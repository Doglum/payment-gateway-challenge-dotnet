using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Models.Validation
{
    public interface IPostPaymentRequestValidator
    {
        ValidationResult Validate(PostPaymentRequest request);
    }
}
