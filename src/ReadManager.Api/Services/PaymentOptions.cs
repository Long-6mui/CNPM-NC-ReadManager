namespace ReadManager.Api.Services;

// Đọc từ mục "Payment" trong cấu hình.
// SecretKey KHÔNG ghi vào appsettings.json (sẽ bị commit lên Git) — đặt bằng User Secrets:
//   dotnet user-secrets set "Payment:SecretKey" "doi-thanh-chuoi-bi-mat-cua-ban" --project src/ReadManager.Api
public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Mock";
    public string SecretKey { get; set; } = string.Empty;
}