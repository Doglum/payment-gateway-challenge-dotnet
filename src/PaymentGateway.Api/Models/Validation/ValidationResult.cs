namespace PaymentGateway.Api.Models.Validation
{
    public class ValidationResult
    {
        public List<string> Errors { get; set; } = [];
        public bool IsValid => !Errors.Any();
    }
}
