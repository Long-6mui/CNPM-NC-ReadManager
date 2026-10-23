using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using ReadManager.Api.Data;
using ReadManager.Api.DTOs.Orders;
using ReadManager.Api.DTOs.Payments;
using ReadManager.Api.DTOs.Stories;
using ReadManager.Api.Entities;

namespace ReadManager.Api.Services;

public enum PaymentCallbackOutcome
{
    Paid,              // thanh toán thành công + đã cấp quyền
    PaidGrantFailed,   // đã thu tiền nhưng cấp quyền lỗi → Admin chạy lại (PB25)
    Duplicate,         // callback gửi lặp, đã xử lý từ trước
    Failed,            // thanh toán thất bại
    Cancelled,         // người dùng hủy ở cổng thanh toán
    Pending,           // cổng báo "đang chờ" → chưa làm gì
    InvalidSignature,  // sai chữ ký
    OrderNotFound,
    AmountMismatch     // số tiền không khớp đơn
}

public record PaymentCallbackResult(
    PaymentCallbackOutcome Outcome,
    int OrderId,
    string Message,
    string? OrderStatus = null,
    bool EntitlementGranted = false);

// BACKEND 4 — THANH TOÁN (PB19, PB21, PB24, PB25)
// Việc tạo đơn / đổi trạng thái đơn là của BE3 (IOrderService) — ở đây chỉ GỌI, không viết lại.
public interface IPaymentService
{
    // Cổng thanh toán báo kết quả: xác minh chữ ký + số tiền, chống xử lý lặp, thành công thì Paid + cấp quyền
    Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackDto dto);

    // Người mua hủy đơn đang chờ thanh toán. null = không thấy đơn; InvalidOperationException = không hủy được
    Task<OrderDto?> CancelOrderAsync(int orderId, int userId);

    // PB25 — Admin chạy lại cấp quyền cho đơn đã thu tiền nhưng cấp quyền lỗi (không thu thêm tiền, không cấp trùng)
    Task<RegrantResultDto?> RegrantAsync(int orderId);

    // PB25 — giao dịch thành công nhưng chưa cấp quyền xong
    Task<PagedResultDto<PaymentTransactionDto>> GetNeedsRecoveryAsync(int page, int pageSize);

    // Chỉ để thử ở Development: tạo callback có chữ ký hợp lệ như cổng thật
    PaymentCallbackDto CreateSignedCallback(int orderId, decimal amount, string status, string? transactionId);

    // Worker nền: tự thử lại cấp quyền cho giao dịch đã thu tiền (tối đa maxAttempts lần). Trả về số giao dịch cấp được.
    Task<int> RetryPendingGrantsAsync(int batchSize, int maxAttempts);
}

public class PaymentService : IPaymentService
{
    public const string StatusSuccess = "Success";
    public const string StatusFailed = "Failed";
    public const string StatusCancelled = "Cancelled";
    public const string StatusPending = "Pending";

    public const string GrantNotRequired = "NotRequired";
    public const string GrantPending = "Pending";
    public const string GrantGranted = "Granted";
    public const string GrantFailed = "Failed";

    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IEntitlementService _entitlements;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        IOrderService orders,
        IEntitlementService entitlements,
        IOptions<PaymentOptions> options,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _orders = orders;
        _entitlements = entitlements;
        _options = options.Value;
        _logger = logger;
    }

    // ============================================================
    //  CALLBACK: xác minh → chống lặp → thành công / thất bại
    // ============================================================
    public async Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackDto dto)
    {
        var result = await ProcessCallbackAsync(dto);

        // Ghi nhật ký mọi callback HỢP LỆ (đã qua chữ ký). Callback sai chữ ký chỉ ghi ILogger,
        // KHÔNG ghi database để kẻ gian không làm đầy bảng bằng request giả.
        if (result.Outcome != PaymentCallbackOutcome.InvalidSignature)
        {
            await LogEventAsync(result.OrderId, $"CALLBACK_{result.Outcome.ToString().ToUpperInvariant()}",
                $"tx={dto.TransactionId}; status={dto.Status}; amount={dto.Amount}; {result.Message}");
        }

        return result;
    }

    private async Task<PaymentCallbackResult> ProcessCallbackAsync(PaymentCallbackDto dto)
    {
        // 1) XÁC MINH CHỮ KÝ — sai chữ ký = không phải cổng thanh toán gửi → bỏ qua, KHÔNG đụng database
        var payload = PaymentSignature.BuildPayload(dto.OrderId, dto.TransactionId, dto.Amount, dto.Status);
        if (!PaymentSignature.Verify(_options.SecretKey, payload, dto.Signature))
        {
            _logger.LogWarning("Callback sai chữ ký (đơn {OrderId}, mã giao dịch {TransactionId}).",
                dto.OrderId, dto.TransactionId);
            return Result(PaymentCallbackOutcome.InvalidSignature, dto.OrderId, "Chữ ký không hợp lệ.");
        }

        // 2) XÁC MINH ĐƠN + SỐ TIỀN — đối chiếu với số tiền đã chụp trong đơn lúc tạo
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == dto.OrderId);
        if (order is null)
            return Result(PaymentCallbackOutcome.OrderNotFound, dto.OrderId, "Không tìm thấy đơn hàng.");

        if (dto.Amount != order.Amount)
        {
            _logger.LogWarning("Callback sai số tiền: đơn {OrderId} cần {Expected} nhưng nhận {Actual}.",
                order.OrderId, order.Amount, dto.Amount);
            return Result(PaymentCallbackOutcome.AmountMismatch, order.OrderId,
                "Số tiền không khớp với đơn hàng.", order.Status);
        }

        // 3) "Đang chờ" chưa phải kết quả cuối → không ghi gì, không đổi gì
        if (dto.Status == StatusPending)
            return Result(PaymentCallbackOutcome.Pending, order.OrderId,
                "Giao dịch đang chờ kết quả cuối — chưa thay đổi gì.", order.Status);

        // 4) CHỐNG XỬ LÝ LẶP — mã giao dịch này đã ghi nhận rồi?
        var existing = await FindTransactionAsync(dto.TransactionId);
        if (existing is not null)
            return await HandleDuplicateAsync(existing, order);

        // 5) GHI GIAO DỊCH. Unique index (Provider, GatewayTransactionId) chặn trường hợp
        //    2 callback giống nhau đến CÙNG LÚC: chỉ 1 cái ghi được.
        var isDoublePayment = dto.Status == StatusSuccess
            && await _db.PaymentTransactions.AnyAsync(t => t.OrderId == order.OrderId && t.Status == StatusSuccess);

        var tx = new PaymentTransaction
        {
            OrderId = order.OrderId,
            Provider = _options.Provider,
            GatewayTransactionId = dto.TransactionId,
            Amount = dto.Amount,
            Status = dto.Status,
            GrantStatus = dto.Status == StatusSuccess ? GrantPending : GrantNotRequired,
            Note = isDoublePayment
                ? "Đơn đã có giao dịch thành công trước đó — giao dịch này là thu trùng, cần hoàn tiền."
                : null,
            CreatedAt = DateTime.UtcNow
        };
        _db.PaymentTransactions.Add(tx);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            _db.Entry(tx).State = EntityState.Detached;
            var winner = await FindTransactionAsync(dto.TransactionId);
            if (winner is null) throw;
            return await HandleDuplicateAsync(winner, order);
        }

        // 6) THEO KẾT QUẢ
        switch (dto.Status)
        {
            case StatusSuccess:
                {
                    // Đơn → Paid (BE3 lo; đơn đã Expired/Cancelled mà tiền vẫn về thì vẫn ghi nhận Paid)
                    await _orders.MarkPaidAsync(order.OrderId, tx.CreatedAt);
                    var granted = await TryGrantAsync(tx.PaymentTransactionId, order.OrderId);
                    var statusNow = await CurrentOrderStatusAsync(order.OrderId);
                    return granted
                        ? Result(PaymentCallbackOutcome.Paid, order.OrderId,
                            "Thanh toán thành công, đã cấp quyền đọc.", statusNow, true)
                        : Result(PaymentCallbackOutcome.PaidGrantFailed, order.OrderId,
                            "Đã ghi nhận thanh toán nhưng cấp quyền bị lỗi — Admin cần chạy lại.", statusNow);
                }

            case StatusFailed:
                // PB24: thất bại → KHÔNG cấp quyền. Chỉ đơn đang Pending mới chuyển Failed.
                await _orders.MarkFailedAsync(order.OrderId);
                return Result(PaymentCallbackOutcome.Failed, order.OrderId,
                    "Thanh toán không thành công — không cấp quyền.",
                    await CurrentOrderStatusAsync(order.OrderId));

            default:
                // StatusCancelled: người dùng hủy ở cổng thanh toán → KHÔNG cấp quyền
                await SetOrderStatusIfPendingAsync(order.OrderId, OrderService.Cancelled);
                return Result(PaymentCallbackOutcome.Cancelled, order.OrderId,
                    "Giao dịch đã bị hủy — không cấp quyền.",
                    await CurrentOrderStatusAsync(order.OrderId));
        }
    }

    // Callback gửi lặp: KHÔNG ghi giao dịch mới, KHÔNG đổi trạng thái đơn lần nữa.
    // Riêng giao dịch THÀNH CÔNG mà lần trước cấp quyền chưa xong → thử cấp lại (tự chữa lỗi).
    private async Task<PaymentCallbackResult> HandleDuplicateAsync(PaymentTransaction existing, Order order)
    {
        var granted = existing.Status == StatusSuccess && existing.GrantStatus == GrantGranted;

        if (existing.Status == StatusSuccess && existing.GrantStatus != GrantGranted)
        {
            await _orders.MarkPaidAsync(order.OrderId, existing.CreatedAt);
            granted = await TryGrantAsync(existing.PaymentTransactionId, order.OrderId);
        }

        return Result(PaymentCallbackOutcome.Duplicate, order.OrderId,
            "Callback này đã được xử lý trước đó.",
            await CurrentOrderStatusAsync(order.OrderId), granted);
    }

    // ============================================================
    //  CẤP QUYỀN (an toàn khi gọi nhiều lần)
    // ============================================================
    // Lưu quyền đọc + đánh dấu giao dịch "Granted" trong CÙNG 1 lần SaveChanges
    // → hoặc cả hai cùng thành công, hoặc cả hai cùng không. Lỗi thì ghi lại để Admin chạy lại (PB25).
    private async Task<bool> TryGrantAsync(int paymentTransactionId, int orderId)
    {
        try
        {
            var tx = await _db.PaymentTransactions.FirstAsync(t => t.PaymentTransactionId == paymentTransactionId);
            var order = await _db.Orders.AsNoTracking().FirstAsync(o => o.OrderId == orderId);

            // Đơn này đã có quyền rồi thì KHÔNG cấp thêm (chống cấp trùng)
            if (!await _db.Entitlements.AnyAsync(e => e.OrderId == orderId))
                _db.Entitlements.Add(await _entitlements.BuildForOrderAsync(order));

            tx.GrantStatus = GrantGranted;
            tx.GrantAttempts++;
            tx.GrantError = null;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Request khác vừa cấp quyền cho đúng đơn này → coi như đã cấp
            _db.ChangeTracker.Clear();
            await MarkGrantAsync(paymentTransactionId, GrantGranted, null);
            return true;
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogError(ex, "Cấp quyền cho đơn {OrderId} bị lỗi (giao dịch {TransactionId}).",
                orderId, paymentTransactionId);
            await MarkGrantAsync(paymentTransactionId, GrantFailed, Truncate(ex.Message, 500));
            await LogEventAsync(orderId, "GRANT_FAILED", ex.Message);
            return false;
        }

        await LogEventAsync(orderId, "GRANT_OK", $"transaction={paymentTransactionId}");
        return true;
    }

    private Task<int> MarkGrantAsync(int paymentTransactionId, string grantStatus, string? error)
        => _db.PaymentTransactions
            .Where(t => t.PaymentTransactionId == paymentTransactionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.GrantStatus, grantStatus)
                .SetProperty(t => t.GrantAttempts, t => t.GrantAttempts + 1)
                .SetProperty(t => t.GrantError, error));

    // ============================================================
    //  PB25 — ADMIN CHẠY LẠI CẤP QUYỀN
    // ============================================================
    public async Task<RegrantResultDto?> RegrantAsync(int orderId)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order is null) return null;

        // CHỈ chạy lại khi có giao dịch THÀNH CÔNG (tiền đã về). Không có thì tuyệt đối không cấp quyền.
        var txs = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.OrderId == orderId && t.Status == StatusSuccess)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.PaymentTransactionId)
            .ToListAsync();

        if (txs.Count == 0)
            throw new InvalidOperationException(
                "Đơn này chưa có giao dịch thanh toán thành công nên không thể cấp quyền.");

        var alreadyGranted = await _db.Entitlements.AnyAsync(e => e.OrderId == orderId);

        // Chạy lại KHÔNG gọi cổng thanh toán → không thu thêm tiền.
        await _orders.MarkPaidAsync(orderId, txs[0].CreatedAt);

        var granted = true;
        for (var i = 0; i < txs.Count; i++)
        {
            if (i == 0 || txs[i].GrantStatus != GrantGranted)
                granted &= await TryGrantAsync(txs[i].PaymentTransactionId, orderId);
        }

        var entitlement = await _db.Entitlements.AsNoTracking().FirstOrDefaultAsync(e => e.OrderId == orderId);

        return new RegrantResultDto
        {
            OrderId = orderId,
            Granted = granted,
            AlreadyGranted = alreadyGranted,
            EntitlementId = entitlement?.EntitlementId,
            StartsAt = entitlement?.StartsAt,
            ExpiresAt = entitlement?.ExpiresAt,
            Message = alreadyGranted
                ? "Đơn này đã được cấp quyền từ trước — không cấp thêm."
                : granted
                    ? "Đã chạy lại và cấp quyền thành công."
                    : "Cấp quyền vẫn bị lỗi — xem GrantError trong danh sách needs-recovery."
        };
    }

    public async Task<PagedResultDto<PaymentTransactionDto>> GetNeedsRecoveryAsync(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.Status == StatusSuccess && t.GrantStatus != GrantGranted);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.PaymentTransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new PaymentTransactionDto
            {
                PaymentTransactionId = t.PaymentTransactionId,
                OrderId = t.OrderId,
                UserId = t.Order.UserId,
                ItemName = t.Order.ItemName,
                Amount = t.Amount,
                Provider = t.Provider,
                GatewayTransactionId = t.GatewayTransactionId,
                Status = t.Status,
                GrantStatus = t.GrantStatus,
                GrantAttempts = t.GrantAttempts,
                GrantError = t.GrantError,
                Note = t.Note,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new PagedResultDto<PaymentTransactionDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ============================================================
    //  HỦY ĐƠN (PB24: "hủy")
    // ============================================================
    public async Task<OrderDto?> CancelOrderAsync(int orderId, int userId)
    {
        // Chỉ chủ đơn mới hủy được (đơn của người khác → coi như không tìm thấy)
        var current = await _orders.GetByIdAsync(orderId, userId, isAdmin: false);
        if (current is null) return null;

        if (current.Status == OrderService.Cancelled)
            return current;   // bấm hủy 2 lần không lỗi

        if (!await SetOrderStatusIfPendingAsync(orderId, OrderService.Cancelled))
            throw new InvalidOperationException(current.Status == OrderService.Paid
                ? "Đơn đã thanh toán nên không thể hủy."
                : $"Đơn đang ở trạng thái {current.Status}, không thể hủy.");

        return await _orders.GetByIdAsync(orderId, userId, isAdmin: false);
    }

    // ============================================================
    //  GIẢ LẬP CỔNG THANH TOÁN (chỉ Development — controller chặn ở môi trường khác)
    // ============================================================
    public PaymentCallbackDto CreateSignedCallback(int orderId, decimal amount, string status, string? transactionId)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException(
                "Chưa cấu hình Payment:SecretKey (đặt bằng dotnet user-secrets).");

        var txId = string.IsNullOrWhiteSpace(transactionId)
            ? $"MOCK-{Guid.NewGuid():N}"
            : transactionId.Trim();

        var payload = PaymentSignature.BuildPayload(orderId, txId, amount, status);
        return new PaymentCallbackDto
        {
            OrderId = orderId,
            TransactionId = txId,
            Amount = amount,
            Status = status,
            Signature = PaymentSignature.Sign(_options.SecretKey, payload)
        };
    }

    // ============================================================
    //  WORKER NỀN: tự thử lại cấp quyền (PB25)
    // ============================================================
    public async Task<int> RetryPendingGrantsAsync(int batchSize, int maxAttempts)
    {
        // Bỏ qua giao dịch quá mới (callback có thể đang cấp quyền dở) và giao dịch đã thử đủ số lần
        // (để Admin xử lý tay bằng endpoint regrant — endpoint đó không bị giới hạn số lần).
        var cutoff = DateTime.UtcNow.AddMinutes(-1);

        var batch = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.Status == StatusSuccess
                        && t.GrantStatus != GrantGranted
                        && t.GrantAttempts < maxAttempts
                        && t.CreatedAt < cutoff)
            .OrderBy(t => t.GrantAttempts).ThenBy(t => t.CreatedAt)   // ít lần thử nhất trước → không bị kẹt bởi bản ghi lỗi mãi
            .Take(batchSize)
            .Select(t => new { t.PaymentTransactionId, t.OrderId, t.CreatedAt })
            .ToListAsync();

        var recovered = 0;
        foreach (var item in batch)
        {
            try
            {
                await _orders.MarkPaidAsync(item.OrderId, item.CreatedAt);
                if (await TryGrantAsync(item.PaymentTransactionId, item.OrderId))
                    recovered++;
            }
            catch (Exception ex)
            {
                // Lỗi ngoài bước cấp quyền: vẫn tính 1 lần thử để không lặp vô hạn
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Worker: lỗi khi thử lại cấp quyền cho đơn {OrderId}.", item.OrderId);
                await MarkGrantAsync(item.PaymentTransactionId, GrantFailed, Truncate(ex.Message, 500));
            }
        }

        return recovered;
    }

    // ============================================================
    //  NHẬT KÝ (bảng PaymentEvents)
    // ============================================================
    private async Task LogEventAsync(int? orderId, string eventType, string? detail)
    {
        var evt = new PaymentEvent
        {
            OrderId = orderId,
            EventType = eventType,
            Detail = detail is null ? null : Truncate(detail, 1000),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.PaymentEvents.Add(evt);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Ghi nhật ký không được làm hỏng luồng thanh toán
            _db.Entry(evt).State = EntityState.Detached;
            _logger.LogWarning(ex, "Không ghi được PaymentEvent {EventType} cho đơn {OrderId}.", eventType, orderId);
        }
    }

    // ============================================================
    //  HÀM PHỤ
    // ============================================================
    private Task<PaymentTransaction?> FindTransactionAsync(string gatewayTransactionId)
        => _db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Provider == _options.Provider
                                      && t.GatewayTransactionId == gatewayTransactionId);

    // Chỉ đơn đang Pending mới đổi được (đơn đã Paid không bao giờ bị hạ xuống)
    private async Task<bool> SetOrderStatusIfPendingAsync(int orderId, string newStatus)
    {
        var now = DateTime.UtcNow;
        var changed = await _db.Orders
            .Where(o => o.OrderId == orderId && o.Status == OrderService.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, newStatus)
                .SetProperty(o => o.UpdatedAt, now));
        return changed > 0;
    }

    private Task<string> CurrentOrderStatusAsync(int orderId)
        => _db.Orders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => o.Status)
            .FirstAsync();

    private static PaymentCallbackResult Result(
        PaymentCallbackOutcome outcome, int orderId, string message,
        string? orderStatus = null, bool granted = false)
        => new(outcome, orderId, message, orderStatus, granted);

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max];

    private static bool IsDuplicateKey(Exception ex)
        => (ex as MySqlException ?? ex.InnerException as MySqlException)?.Number == 1062;
}