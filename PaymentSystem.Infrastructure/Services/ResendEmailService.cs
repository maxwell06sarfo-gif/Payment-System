using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using PaymentSystem.Core.Services;

namespace PaymentSystem.Infrastructure.Services;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public ResendEmailService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Resend:ApiKey"] ?? string.Empty;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)) return;

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var payload = new
        {
            from = "Billing <billing@yourdomain.com>",
            to = new[] { to },
            subject = subject,
            html = body
        };

        await _httpClient.PostAsJsonAsync("https://api.resend.com/emails", payload);
    }
}