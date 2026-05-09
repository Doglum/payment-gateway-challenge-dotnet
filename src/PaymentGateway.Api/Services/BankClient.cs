using System.Net;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services
{
    public class BankClient : IBankClient
    {
        private readonly HttpClient _httpClient;

        public BankClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<BankPaymentResponse> ProcessPaymentAsync(PostPaymentRequest request)
        {
            var bankRequest = new
            {
                card_number = request.CardNumber,
                expiry_date = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
                currency = request.Currency,
                amount = request.Amount,
                cvv = request.Cvv
            };

            var json = JsonSerializer.Serialize(bankRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync("/payments", content);
            httpResponse.EnsureSuccessStatusCode();
            var responseBody = await httpResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BankPaymentResponse>(responseBody)!;
        }
    }
}
