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
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IPostPaymentRequestValidator _postPaymentRequestValidator;
    private readonly IBankClient _bankClient;

    public PaymentsController(IPaymentsRepository paymentsRepository,
                              IPostPaymentRequestValidator postPaymentRequestValidator,
                              IBankClient bankClient)
    {
        _paymentsRepository = paymentsRepository;
        _postPaymentRequestValidator = postPaymentRequestValidator;
        _bankClient = bankClient;
    }

    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetPaymentAsync))]
    public async Task<ActionResult<PostPaymentResponse?>> GetPaymentAsync(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment == null)
        {
            return NotFound();
        }

        return Ok(payment);
    }

    [HttpPost]
    [ActionName(nameof(CreatePaymentAsync))]
    public async Task<ActionResult<PostPaymentResponse>> CreatePaymentAsync([FromBody] PostPaymentRequest request)
    {
        var validationResult = _postPaymentRequestValidator.Validate(request);

        if (!validationResult.IsValid)
        {
            return BadRequest(new { status = "rejected", errors = validationResult.Errors });
        }

        BankPaymentResponse bankResult;
        try
        {
            bankResult = await _bankClient.ProcessPaymentAsync(request);
        }
        catch (HttpRequestException ex) 
        {
            switch(ex.StatusCode)
            {
                case System.Net.HttpStatusCode.BadRequest:
                    return BadRequest(new { status = "rejected", errors = new[] { "Invalid payment details" } });
                case System.Net.HttpStatusCode.ServiceUnavailable:
                    return StatusCode(503, new { status = "error", errors = new[] { "Bank service is currently unavailable" } });
                default:
                    return StatusCode(500, new { status = "error", errors = new[] { "An unexpected error occurred while processing the payment" } });
            }
        }
        
        //storing all non error requests, information on a decline might be useful
        var response = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined, 
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency.ToUpperInvariant(),
            Amount = request.Amount,
            AuthorizationCode = bankResult.Authorized ? bankResult.AuthorizationCode : null
        };

        _paymentsRepository.Add(response);

        return CreatedAtAction(nameof(GetPaymentAsync), new { id = response.Id }, response);
    }
}