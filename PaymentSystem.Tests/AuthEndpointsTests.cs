using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
using PaymentSystem.Core.DTOs;

namespace PaymentSystem.Tests;

public class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // Stand up a fully in-memory instance of the web host.
        // "Testing" environment keeps the EF in-memory database active
        // and suppresses any Supabase or Stripe network calls.
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Supabase:UseRestStore"] = "false"
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Register_And_Login_Pipeline_Returns_Valid_Token()
    {
        // Use a fresh GUID-suffixed address so repeated test runs never collide
        // on the unique email constraint, even if the in-memory DB is reused.
        var testEmail = $"testuser_{System.Guid.NewGuid()}@example.com";

        var registerPayload = new UserRegisterRequest(
            testEmail,
            "SecurePassword123!",
            "Integration Test User"
        );

        var loginPayload = new UserLoginRequest(
            testEmail,
            "SecurePassword123!"
        );

        // Part 1 — registration
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Part 2 — login with the account we just created
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResultStub>();
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token), "Authentication succeeded but no JWT was returned.");
    }

    // Minimal stub to deserialise the token from the anonymous response object.
    private record LoginResultStub(string Token);
}
