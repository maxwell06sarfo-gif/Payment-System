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
        // Instantiates an isolated, in-memory instance of the web application pipeline
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
        // Arrange: Generate unique user credentials for the test pass
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

        // Act - Part 1: Submit a registration request to the in-memory server
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Assert - Part 1: Ensure the account was successfully created
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // Act - Part 2: Attempt to authenticate with the newly created account
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);

        // Assert - Part 2: Verify that authentication is successful and returns a JWT token
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResultStub>();
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.Token), "The authentication engine failed to issue a valid JWT string.");
    }

    // Direct inline stub to safely deserialize the dynamic anonymous token payload object
    private record LoginResultStub(string Token);
}
