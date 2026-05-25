using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentSystem.Core.Entities;
using PaymentSystem.Core.Enums;

namespace PaymentSystem.Infrastructure.Data;

public class SupabaseDataStore : IAppDataStore
{
    private readonly HttpClient _http;
    private readonly ILogger<SupabaseDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _restUrl;
    private readonly string _serviceRoleKey;

    public SupabaseDataStore(HttpClient http, IConfiguration configuration, ILogger<SupabaseDataStore> logger)
    {
        var supabaseUrl = configuration["Supabase:Url"]?.TrimEnd('/');
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(_serviceRoleKey))
        {
            throw new InvalidOperationException("Supabase URL and service-role key are required when Supabase storage is enabled.");
        }

        _http = http;
        _logger = logger;
        _restUrl = $"{supabaseUrl}/rest/v1";
    }

    public async Task<bool> UserExistsByEmailAsync(string email, CancellationToken ct)
    {
        var users = await GetAsync<List<SupabaseUserRow>>(
            $"users?email=eq.{Escape(email)}&select=id&limit=1",
            ct);

        return users.Count > 0;
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct)
    {
        var users = await GetAsync<List<SupabaseUserRow>>(
            $"users?email=eq.{Escape(email)}&select=*&limit=1",
            ct);

        return users.Count == 0 ? null : MapUser(users[0]);
    }

    public async Task<User?> GetUserWithSubscriptionsAsync(Guid userId, CancellationToken ct)
    {
        // Optimized: Fetch User and nested Subscriptions in a single round-trip.
        var users = await GetAsync<List<SupabaseUserRow>>(
            $"users?id=eq.{userId}&select=*,subscriptions(*)&limit=1",
            ct);

        if (users.Count == 0)
        {
            return null;
        }
        var row = users[0];
        var user = MapUser(row);
        user.Subscriptions = row.Subscriptions
            .Select(MapSubscription)
            .OrderByDescending(s => s.StartsAt)
            .ToList();

        return user;
    }

    public async Task AddUserAsync(User user, CancellationToken ct)
    {
        var inserted = await SendAsync<List<SupabaseUserRow>>(
            HttpMethod.Post,
            "users",
            new
            {
                id = user.Id,
                email = user.Email,
                password_hash = user.PasswordHash,
                full_name = user.FullName,
                stripe_customer_id = user.StripeCustomerId,
                created_at = user.CreatedAt
            },
            ct,
            preferRepresentation: true);

        if (inserted.Count > 0)
        {
            ApplyUserRow(user, inserted[0]);
        }
    }

    public Task UpdateUserAsync(User user, CancellationToken ct)
    {
        return SendAsync<object>(
            HttpMethod.Patch,
            $"users?id=eq.{user.Id}",
            new
            {
                email = user.Email,
                password_hash = user.PasswordHash,
                full_name = user.FullName,
                stripe_customer_id = user.StripeCustomerId
            },
            ct);
    }

    public Task MarkActiveSubscriptionsReplacedAsync(Guid userId, CancellationToken ct)
    {
        return SendAsync<object>(
            HttpMethod.Patch,
            $"subscriptions?user_id=eq.{userId}&status=eq.{SubscriptionStatus.Active}",
            new { status = SubscriptionStatus.Replaced },
            ct);
    }

    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct)
    {
        var inserted = await SendAsync<List<SupabaseSubscriptionRow>>(
            HttpMethod.Post,
            "subscriptions",
            ToSubscriptionPayload(subscription),
            ct,
            preferRepresentation: true);

        if (inserted.Count > 0)
        {
            ApplySubscriptionRow(subscription, inserted[0]);
        }
    }

    public async Task<int> ExpireAllPassedSubscriptionsAsync(DateTime now, CancellationToken ct)
    {
        var result = await SendAsync<List<object>>(
            HttpMethod.Patch,
            $"subscriptions?status=not.in.({SubscriptionStatus.Expired},{SubscriptionStatus.Canceled})&ends_at=lt.{Escape(now.ToString("O"))}",
            new { status = SubscriptionStatus.Expired },
            ct,
            preferRepresentation: true);

        return result.Count;
    }

    public async Task<int> SendBulkExpirationNotificationsAsync(DateTime now, DateTime window, CancellationToken ct)
    {
        var dateThreshold = Escape(now.AddDays(-1).ToString("O"));
        var query = $"subscriptions?status=eq.{SubscriptionStatus.Active}&ends_at=gte.{Escape(now.ToString("O"))}&ends_at=lte.{Escape(window.ToString("O"))}"
                  + $"&or=(last_expiration_notification_at.is.null,last_expiration_notification_at.lt.{dateThreshold})";

        var result = await SendAsync<List<object>>(
            HttpMethod.Patch,
            query,
            new { last_expiration_notification_at = now },
            ct,
            preferRepresentation: true);

        return result.Count;
    }

    public async Task ActivateLatestPendingCheckoutAsync(
        Guid userId,
        string? stripeSubscriptionId,
        CancellationToken ct)
    {
        var rows = await GetAsync<List<SupabaseSubscriptionRow>>(
            $"subscriptions?user_id=eq.{userId}&status=eq.{SubscriptionStatus.PendingCheckout}&select=*&order=starts_at.desc&limit=1",
            ct);

        if (rows.Count == 0)
        {
            return;
        }

        // Payment confirmed — retire any still-active subscription before promoting this one.
        await MarkActiveSubscriptionsReplacedAsync(userId, ct);

        await SendAsync<object>(
            HttpMethod.Patch,
            $"subscriptions?id=eq.{rows[0].Id}",
            new
            {
                status = SubscriptionStatus.Active,
                stripe_subscription_id = stripeSubscriptionId ?? rows[0].StripeSubscriptionId
            },
            ct);
    }

    public async Task<bool> HasWebhookBeenProcessedAsync(string eventId, CancellationToken ct)
    {
        var events = await GetAsync<List<object>>(
            $"processed_webhook_events?id=eq.{Escape(eventId)}&select=id&limit=1",
            ct);
        return events.Count > 0;
    }

    public Task MarkWebhookAsProcessedAsync(string eventId, CancellationToken ct)
    {
        return SendAsync<object>(
            HttpMethod.Post,
            "processed_webhook_events",
            new { id = eventId, processed_at = DateTime.UtcNow },
            ct);
    }

    public Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct)
    {
        var payload = new
        {
            id = token.Id,
            token = token.Token,
            userId = token.UserId,
            expiresAt = token.ExpiresAt,
            createdAt = token.CreatedAt,
            revokedAt = token.RevokedAt
        };
        return SendAsync<object>(HttpMethod.Post, "refresh_tokens", payload, ct);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token, CancellationToken ct)
    {
        var tokens = await GetAsync<List<SupabaseRefreshTokenRow>>($"refresh_tokens?token=eq.{Escape(token)}&select=*&limit=1", ct);
        return tokens.Count == 0 ? null : MapRefreshToken(tokens[0]);
    }

    private async Task PatchSubscriptionsByIdsAsync(
        IEnumerable<Guid> subscriptionIds,
        object payload,
        CancellationToken ct)
    {
        var ids = subscriptionIds.Select(id => id.ToString()).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        await SendAsync<object>(
            HttpMethod.Patch,
            $"subscriptions?id=in.({string.Join(",", ids)})",
            payload,
            ct);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_restUrl}/{path}");
        return await SendAsync<T>(request, ct);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object payload,
        CancellationToken ct,
        bool preferRepresentation = false)
    {
        using var request = new HttpRequestMessage(method, $"{_restUrl}/{path}")
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };

        request.Headers.TryAddWithoutValidation(
            "Prefer",
            preferRepresentation ? "return=representation" : "return=minimal");

        return await SendAsync<T>(request, ct);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        // Auth headers are applied per-request rather than on the shared HttpClient
        // so we never risk leaking credentials across concurrent requests.
        request.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Log the actual error for the developer to see in Render logs
            _logger.LogError("Supabase API Error: {StatusCode} - {Content}", response.StatusCode, content);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Surface a generic message outward — the raw Supabase error must not
                // leak to callers as it could aid reconnaissance of the data schema.
                throw new InvalidOperationException("Data storage service is currently unavailable.");
            }
            throw new InvalidOperationException(
                "An error occurred while communicating with the data store.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return typeof(T) == typeof(object) ? (T)new object() : Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(content, _jsonOptions)
            ?? throw new InvalidOperationException("Supabase returned an empty response.");
    }

    private static object ToSubscriptionPayload(Subscription subscription)
    {
        return new
        {
            id = subscription.Id,
            user_id = subscription.UserId,
            tier = subscription.Tier.ToString(),
            duration = subscription.Duration.ToString(),
            stripe_subscription_id = subscription.StripeSubscriptionId,
            status = subscription.Status.ToString(),
            price = subscription.Price,
            currency = subscription.Currency,
            starts_at = subscription.StartsAt,
            ends_at = subscription.EndsAt,
            is_auto_renew_enabled = subscription.IsAutoRenewEnabled,
            last_expiration_notification_at = subscription.LastExpirationNotificationAt
        };
    }

    private static User MapUser(SupabaseUserRow row)
    {
        var user = new User();
        ApplyUserRow(user, row);
        return user;
    }

    private static void ApplyUserRow(User user, SupabaseUserRow row)
    {
        user.Id = row.Id;
        user.Email = row.Email;
        user.PasswordHash = row.PasswordHash;
        user.FullName = row.FullName;
        user.StripeCustomerId = row.StripeCustomerId;
        user.CreatedAt = row.CreatedAt;
    }

    private static Subscription MapSubscription(SupabaseSubscriptionRow row)
    {
        var subscription = new Subscription();
        ApplySubscriptionRow(subscription, row);
        return subscription;
    }

    private static void ApplySubscriptionRow(Subscription subscription, SupabaseSubscriptionRow row)
    {
        subscription.Id = row.Id;
        subscription.UserId = row.UserId;
        subscription.Tier = Enum.Parse<SubscriptionTier>(row.Tier);
        subscription.Duration = Enum.Parse<SubscriptionDuration>(row.Duration);
        subscription.StripeSubscriptionId = row.StripeSubscriptionId;
        subscription.Status = Enum.Parse<SubscriptionStatus>(row.Status);
        subscription.Price = row.Price;
        subscription.Currency = row.Currency;
        subscription.StartsAt = row.StartsAt;
        subscription.EndsAt = row.EndsAt;
        subscription.IsAutoRenewEnabled = row.IsAutoRenewEnabled;
        subscription.LastExpirationNotificationAt = row.LastExpirationNotificationAt;
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private sealed class SupabaseUserRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("stripe_customer_id")]
        public string? StripeCustomerId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("subscriptions")]
        public List<SupabaseSubscriptionRow> Subscriptions { get; set; } = new();
    }

    private sealed class SupabaseSubscriptionRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("user_id")]
        public Guid UserId { get; set; }

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public string Duration { get; set; } = string.Empty;

        [JsonPropertyName("stripe_subscription_id")]
        public string? StripeSubscriptionId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("starts_at")]
        public DateTime StartsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime EndsAt { get; set; }

        [JsonPropertyName("is_auto_renew_enabled")]
        public bool IsAutoRenewEnabled { get; set; }

        [JsonPropertyName("last_expiration_notification_at")]
        public DateTime? LastExpirationNotificationAt { get; set; }
    }

    private sealed class SupabaseRefreshTokenRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("revokedAt")]
        public DateTime? RevokedAt { get; set; }
    }

    private static RefreshToken MapRefreshToken(SupabaseRefreshTokenRow row)
    {
        return new RefreshToken
        {
            Id = row.Id,
            Token = row.Token,
            UserId = row.UserId,
            ExpiresAt = row.ExpiresAt,
            CreatedAt = row.CreatedAt,
            RevokedAt = row.RevokedAt
        };
    }
}
