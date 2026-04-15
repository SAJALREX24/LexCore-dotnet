namespace LexCore.Application.Interfaces;

public interface IRazorpayService
{
    bool VerifyWebhookSignature(string payload, string signature, string secret);
}
