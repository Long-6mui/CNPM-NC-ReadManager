namespace ReadManager.Api.Services;


public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Mock";
    public string SecretKey { get; set; } = string.Empty;
}