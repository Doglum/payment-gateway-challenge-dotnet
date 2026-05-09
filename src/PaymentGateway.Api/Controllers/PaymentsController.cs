using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Models.Validation;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsRepository _paymentsRepository;
    private readonly IPostPaymentRequestValidator _postPaymentRequestValidator;

    public PaymentsController(PaymentsRepository paymentsRepository, 
                              IPostPaymentRequestValidator postPaymentRequestValidator)
    {
        _paymentsRepository = paymentsRepository;
        _postPaymentRequestValidator = postPaymentRequestValidator;
    }

    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetPaymentAsync))]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment == null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(payment);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> CreatePaymentAsync([FromBody] PostPaymentRequest request)
    {
        var validationResult = _postPaymentRequestValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return BadRequest(new { status = "rejected", errors = validationResult.Errors });
        }

        var response = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized, //TODO placeholder, should be based on response received
            CardNumberLastFour = request.CardNumberLastFour,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount
        };

        _paymentsRepository.Add(response);

        return CreatedAtAction(nameof(GetPaymentAsync), new { id = response.Id }, response);
    }
}