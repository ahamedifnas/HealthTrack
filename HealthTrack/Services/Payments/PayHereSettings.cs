namespace HealthTrack.Services.Payments;

public class PayHereSettings
{
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantSecret { get; set; } = string.Empty;
    public string Mode { get; set; } = "Sandbox";
    public string CheckoutUrl { get; set; } = "https://sandbox.payhere.lk/pay/checkout";
    public string Currency { get; set; } = "LKR";
}
