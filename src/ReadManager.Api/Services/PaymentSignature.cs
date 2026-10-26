using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ReadManager.Api.DTOs.Payments;

namespace ReadManager.Api.Services;

// Chữ ký HMAC-SHA256 của callback.
// Chỉ cổng thanh toán và server mình biết SecretKey → người ngoài không thể giả callback "thành công".
public static class PaymentSignature
{
    // Chuỗi đem đi ký: cố định thứ tự trường; số tiền luôn 2 chữ số thập phân để 2 bên ra cùng 1 chuỗi
    public static string BuildPayload(PaymentCallbackDto d)
        => string.Create(CultureInfo.InvariantCulture,
            $"provider={d.Provider}&eventId={d.EventId}&merchantRequestId={d.MerchantRequestId}&orderId={d.OrderId}&transactionId={d.TransactionId}&amount={d.Amount:0.00}&currency={d.Currency}&status={d.Status}&failureCode={d.FailureCode}");

    public static string Sign(string secretKey, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(string secretKey, string payload, string? signature)
    {
        // Chưa cấu hình khóa hoặc không có chữ ký → coi như sai (an toàn mặc định)
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(signature))
            return false;

        var expected = Encoding.UTF8.GetBytes(Sign(secretKey, payload));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual);   // so sánh không lộ thông tin qua thời gian
    }
}