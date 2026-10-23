using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReadManager.Api.Services;

// BE4 — WORKER NỀN: mỗi 5 phút tự thử lại cấp quyền cho giao dịch đã thu tiền nhưng cấp quyền lỗi (PB25).
// Mỗi giao dịch thử tối đa MaxAttempts lần; quá số lần đó thì để Admin xử lý tay (endpoint regrant).
public class EntitlementRetryWorker : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EntitlementRetryWorker> _logger;

    public EntitlementRetryWorker(IServiceScopeFactory scopeFactory, ILogger<EntitlementRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);   // chờ app khởi động xong

            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    // Worker sống suốt đời app còn DbContext thì theo từng lượt → phải tạo scope riêng mỗi lần quét
                    using var scope = _scopeFactory.CreateScope();
                    var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                    var recovered = await payments.RetryPendingGrantsAsync(BatchSize, MaxAttempts);
                    if (recovered > 0)
                        _logger.LogInformation("Worker: đã tự cấp lại quyền cho {Count} giao dịch.", recovered);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Worker cấp quyền gặp lỗi ở lượt quét này.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Web App đang tắt — thoát êm
        }
    }
}