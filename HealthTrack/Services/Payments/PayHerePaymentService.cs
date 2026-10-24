using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services.Payments;

public class PayHerePaymentService : IPayHerePaymentService
{
    private readonly PayHereSettings _settings;

    public PayHerePaymentService(IOptions<PayHereSettings> settings)
    {
        _settings = settings.Value;
    }

    public string MerchantId => _settings.MerchantId;
    public string CheckoutUrl => _settings.CheckoutUrl;
    public string Currency => _settings.Currency;

    public string GenerateOrderId()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"HT-{datePart}-{randomPart}";
    }

    public string GenerateCheckoutHash(string orderId, decimal amount, string currency)
    {
        var formattedAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);
        var hashedSecret = ComputeMd5(_settings.MerchantSecret).ToUpperInvariant();
        var rawString = $"{_settings.MerchantId}{orderId}{formattedAmount}{currency}{hashedSecret}";
        return ComputeMd5(rawString).ToUpperInvariant();
    }

    public bool VerifyIpnSignature(string orderId, string payhereAmount, string payhereCurrency, string statusCode, string receivedMd5Sig)
    {
        if (string.IsNullOrWhiteSpace(receivedMd5Sig)) return false;

        var hashedSecret = ComputeMd5(_settings.MerchantSecret).ToUpperInvariant();
        var rawString = $"{_settings.MerchantId}{orderId}{payhereAmount}{payhereCurrency}{statusCode}{hashedSecret}";
        var localSignature = ComputeMd5(rawString).ToUpperInvariant();

        return localSignature.Equals(receivedMd5Sig.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeMd5(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }
}
