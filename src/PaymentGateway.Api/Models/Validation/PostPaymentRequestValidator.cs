using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Models.Validation
{
    public class PostPaymentRequestValidator : IPostPaymentRequestValidator
    {
        public ValidationResult Validate(PostPaymentRequest request)
        {
            var result = new ValidationResult();

            // null check, exit early if request body is missing
            if (request == null)
            {
                result.Errors.Add("Request body is required");
                return result;
            }

            //check expiry month is valid before attempting expiry date validation
            if (request.ExpiryMonth < 1 || request.ExpiryMonth > 12)
            {
                result.Errors.Add("ExpiryMonth must be between 1 and 12");
            }

            // check expiry date is in the future, assuming last day of month is expiry date
            var now = DateTime.UtcNow;
            try
            {
                var expiryDate = new DateTime(
                    request.ExpiryYear,
                    request.ExpiryMonth,
                    DateTime.DaysInMonth(request.ExpiryYear, request.ExpiryMonth),
                    23, 59, 59,
                    DateTimeKind.Utc
                );
                if (expiryDate <= now)
                {
                    result.Errors.Add("Card expiry must be in the future");
                }
            }
            catch
            {
                result.Errors.Add("Invalid expiry date format");
            }

            // check currency is one of allowed values
            var allowedCurrencies = new[] { "GBP", "USD", "EUR" };
            if (!allowedCurrencies.Contains(request.Currency.ToUpperInvariant()))
            {
                result.Errors.Add("Currency must be one of GBP, USD or EUR");
            }

            // amount must be positive
            if (request.Amount <= 0)
            {
                result.Errors.Add("Amount must be a positive integer");
            }

            // CVV must be 3-4 digits
            if (request.Cvv.Length < 3 || request.Cvv.Length > 4 || !AllCharsAreDigits(request.Cvv))
            {
                result.Errors.Add("CVV must be 3 or 4 digits");
            }

            // card number must be between 14-19 digits
            if (request.CardNumber.Length < 14 || request.CardNumber.Length > 19 || !AllCharsAreDigits(request.CardNumber))
            {
                result.Errors.Add("CardNumberLastFour must be between 14-19 digits");
            }

            return result;
        }

        private static bool AllCharsAreDigits(string str)
        {
            return str.All(char.IsDigit);
        }
    }
}
