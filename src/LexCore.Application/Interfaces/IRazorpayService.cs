namespace LexCore.Application.Interfaces;

public interface IRazorpayService
{
    Task<(string subscriptionId, string paymentLink)> CreateSubscriptionAsync(Guid firmId, string planId, string customerEmail, string customerName);
    Task<bool> CancelSubscriptionAsync(string subscriptionId);
    bool VerifyWebhookSignature(string payload, string signature, string secret);
}
