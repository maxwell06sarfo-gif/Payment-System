namespace PaymentSystem.Core.Entities;

public class ProcessedWebhookEvent
{
    public string Id { get; set; } = string.Empty; // Stripe Event ID
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}