namespace HealthTrack.Services.Payments;

public interface IPayHerePaymentService
{
    string MerchantId { get; }
    string CheckoutUrl { get; }
    string Currency { get; }
    string GenerateOrderId();
    string GenerateCheckoutHash(string orderId, decimal amount, string currency);
    bool VerifyIpnSignature(string orderId, string payhereAmount, string payhereCurrency, string statusCode, string receivedMd5Sig);
}
