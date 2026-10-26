using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    PaidGrantFailed,   // đã ghi nhận tiền nhưng cấp quyền lỗi → Admin chạy lại
    Duplicate,         // callback gửi lặp → trả kết quả cũ, không xử lý lại
    Ignored,           // hợp lệ nhưng bỏ qua (vd: báo thất bại sau khi đã thành công)
    Failed,            // thanh toán thất bại
    Cancelled,         // người dùng hủy ở cổng thanh toán
    Pending,           // cổng báo "đang chờ" → chưa làm gì
    InvalidSignature,  // sai chữ ký
    PaymentNotFound,   // mã yêu cầu không có trong hệ thống
    Rejected           // sai nhà cung cấp / tiền tệ / đơn / số tiền → không ghi nhận thành công
}

public record PaymentCallbackResult(
    PaymentCallbackOutcome Outcome,
    int OrderId,
    string Message,
    string? Reason = null,
    string? OrderStatus = null,
    bool EntitlementGranted = false);

// BACKEND 4 — THANH TOÁN (PB19, PB21, PB24, PB25) :
//   Payments (mỗi lượt thanh toán) · PaymentEvents (mỗi callback) · StoryPurchases / UserSubscriptions (quyền đọc)
public interface IPaymentService
{
    // Bắt đầu 1 lượt thanh toán cho đơn Pending của chính mình. null = không thấy đơn; InvalidOperationException = đơn không còn chờ thanh toán
    Task<StartPaymentResultDto?> StartAsync(int orderId, int userId);

    // Cổng thanh toán báo kết quả: xác minh → chống lặp → ghi nhận + cấp quyền trong 1 transaction
    Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackDto dto);

    // Chỉ để thử ở Development: tạo callback có chữ ký hợp lệ rồi đi qua đúng đường xử lý thật
    Task<PaymentCallbackResult?> SimulateAsync(int orderId, int userId, string status, string? transactionId, string? eventId);

    // Đã thu tiền (đã xác minh) nhưng chưa có quyền đọc
    Task<PagedResultDto<PaymentRecoveryDto>> GetNeedsRecoveryAsync(int page, int pageSize);

    // Admin chạy lại cấp quyền: không thu tiền lại, không cấp trùng. null = không thấy đơn
    Task<RegrantResultDto?> RegrantAsync(int orderId);
}

public class PaymentService : IPaymentService
{
    public const string Currency = "VND";

    // Trạng thái trong callback của cổng
    public const string CbSuccess = "Success";
    public const string CbFailed = "Failed";
    public const string CbCancelled = "Cancelled";
    public const string CbPending = "Pending";

    // Trạng thái bảng Payments / PaymentEvents (đúng CHECK của DB)
    public const string PaySucceeded = "Succeeded";
    public const string PayFailed = "Failed";
    public const string PayCancelled = "Cancelled";
    public const string PayPending = "Pending";
    public const string EvtPending = "Pending";
    public const string EvtProcessed = "Processed";
    public const string EvtFailed = "Failed";
    public const string EvtIgnored = "Ignored";

    private const int MaxPageSize = 50;

    private readonly AppDbContext _db;
    private readonly IOrderService _orders;
    private readonly IEntitlementGrantService _grants;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        IOrderService orders,
        IEntitlementGrantService grants,
        IOptions<PaymentOptions> options,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _orders = orders;
        _grants = grants;
        _options = options.Value;
        _logger = logger;
    }

    // ============================================================
    //  BẮT ĐẦU THANH TOÁN
    // ============================================================
    public async Task<StartPaymentResultDto?> StartAsync(int orderId, int userId)
    {
        // Chỉ chủ đơn; hàm này cũng tự chuyển đơn quá hạn thành Expired
        var order = await _orders.GetByIdAsync(orderId, userId, isAdmin: false);
        if (order is null) return null;

        if (order.Status != OrderService.Pending)
            throw new InvalidOperationException($"Đơn đang ở trạng thái {order.Status}, không thể thanh toán.");

        // Dùng lại lượt đang chờ nếu có; không thì mở lượt mới
        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId && p.Status == PayPending)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync();

        if (payment is null)
        {
            payment = new Payment
            {
                OrderId = orderId,
                Provider = _options.Provider,
                MerchantRequestId = $"RM{orderId}-{Guid.NewGuid():N}",
                Status = PayPending,
                CreatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
        }

        return new StartPaymentResultDto
        {
            PaymentId = payment.PaymentId,
            MerchantRequestId = payment.MerchantRequestId,
            Provider = payment.Provider,
            OrderId = orderId,
            Amount = order.Amount,
            Currency = Currency
        };
    }

    // ============================================================
    //  CALLBACK
    // ============================================================
    public async Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackDto dto)
    {
        // 1) XÁC MINH CHỮ KÝ — sai = không phải cổng thanh toán gửi → bỏ qua, KHÔNG ghi database
        if (!PaymentSignature.Verify(_options.SecretKey, PaymentSignature.BuildPayload(dto), dto.Signature))
        {
            _logger.LogWarning("Callback sai chữ ký (lượt {MerchantRequestId}).", dto.MerchantRequestId);
            return Result(PaymentCallbackOutcome.InvalidSignature, dto.OrderId, "Chữ ký không hợp lệ.");
        }

        // 2) TÌM LƯỢT THANH TOÁN theo mã yêu cầu mình đã phát hành
        var payment = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.MerchantRequestId == dto.MerchantRequestId);
        if (payment is null)
            return Result(PaymentCallbackOutcome.PaymentNotFound, dto.OrderId, "Không tìm thấy lượt thanh toán.");

        // 3) "Đang chờ" chưa phải kết quả cuối → không ghi gì
        if (dto.Status == CbPending)
            return Result(PaymentCallbackOutcome.Pending, payment.OrderId,
                "Giao dịch đang chờ kết quả cuối — chưa thay đổi gì.", null, await OrderStatusAsync(payment.OrderId));

        // 4) XỬ LÝ trong 1 transaction (có khóa đơn)
        try
        {
            return await ProcessInTransactionAsync(dto, payment, grant: true, grantError: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && dto.Status == CbSuccess)
        {
            // Cấp quyền (hoặc việc gì đó trong transaction) lỗi → hoàn tác toàn bộ rồi GHI NHẬN TIỀN không kèm quyền,
            // để giao dịch không bị mất và Admin chạy lại cấp quyền sau (không thu tiền lại).
            _db.ChangeTracker.Clear();
            _logger.LogError(ex, "Xử lý callback thành công cho lượt {PaymentId} bị lỗi — ghi nhận tiền, chưa cấp quyền.",
                payment.PaymentId);
            return await ProcessInTransactionAsync(dto, payment, grant: false, grantError: ex.Message);
        }
    }

    private async Task<PaymentCallbackResult> ProcessInTransactionAsync(
        PaymentCallbackDto dto, Payment payment, bool grant, string? grantError)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        // KHÓA ĐƠN: 2 callback đến cùng lúc sẽ xếp hàng chờ nhau, không xử lý song song
        var orderId = payment.OrderId;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT `OrderId` FROM `Orders` WHERE `OrderId` = {orderId} FOR UPDATE");

        var order = await _db.Orders.FirstAsync(o => o.OrderId == orderId);
        var pay = await _db.Payments.FirstAsync(p => p.PaymentId == payment.PaymentId);
        var now = DateTime.UtcNow;

        // CHỐNG LẶP: ghi sự kiện trước. Unique (PaymentId, ProviderEventId) chặn callback trùng,
        // kể cả 2 callback giống nhau đến cùng lúc.
        var evt = new PaymentEvent
        {
            PaymentId = pay.PaymentId,
            ProviderEventId = dto.EventId,
            Payload = BuildEventPayload(dto),
            ReceivedAt = now,
            ProcessingStatus = EvtPending
        };
        _db.PaymentEvents.Add(evt);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            return await BuildDuplicateResultAsync(payment.PaymentId);   // trả kết quả cũ, không xử lý lại
        }

        // XÁC MINH nhà cung cấp, tiền tệ, đơn, số tiền — lệch thì KHÔNG ghi nhận thành công
        string? reject = null;
        if (!string.Equals(dto.Provider, pay.Provider, StringComparison.Ordinal)) reject = "WRONG_PROVIDER";
        else if (!string.Equals(dto.Currency, Currency, StringComparison.Ordinal)) reject = "WRONG_CURRENCY";
        else if (dto.OrderId != order.OrderId) reject = "ORDER_MISMATCH";
        else if (dto.Amount != order.Amount) reject = "AMOUNT_MISMATCH";

        if (reject is not null)
        {
            _logger.LogWarning("Callback bị từ chối ({Reason}) cho lượt {PaymentId}.", reject, pay.PaymentId);
            return await FinishWithoutChangeAsync(tx, evt, order, EvtIgnored, reject, PaymentCallbackOutcome.Rejected);
        }

        // ---------- THÀNH CÔNG ----------
        if (dto.Status == CbSuccess)
        {
            if (pay.Status == PaySucceeded)
            {
                // Lượt này đã thành công từ trước (sự kiện khác mã) → không ghi nhận / gia hạn lần nữa
                var sameTransaction = string.Equals(pay.ProviderTransactionId, dto.TransactionId, StringComparison.Ordinal);
                return await FinishWithoutChangeAsync(tx, evt, order, EvtIgnored,
                    sameTransaction ? "DUPLICATE_TRANSACTION" : "TRANSACTION_MISMATCH",
                    sameTransaction ? PaymentCallbackOutcome.Duplicate : PaymentCallbackOutcome.Rejected);
            }

            pay.Status = PaySucceeded;
            pay.ProviderTransactionId = dto.TransactionId;
            pay.ReceivedAmount = dto.Amount;
            pay.FailureCode = null;
            pay.VerifiedAt = now;
            pay.SucceededAt = now;

            // Đơn → Paid. Đơn đã Expired/Cancelled/Failed mà tiền vẫn về thì vẫn ghi nhận, không mất giao dịch.
            if (order.Status != OrderService.Paid)
            {
                order.Status = OrderService.Paid;
                order.PaidAt = now;
                order.UpdatedAt = now;
            }

            if (grant)
            {
                await _grants.AddGrantAsync(order, now);
                evt.ProcessingStatus = EvtProcessed;
            }
            else
            {
                evt.ProcessingStatus = EvtFailed;
                evt.ErrorMessage = Truncate(grantError ?? "GRANT_FAILED", 1000);
            }
            evt.ProcessedAt = now;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) when (IsDuplicateKey(ex))
            {
                // Mã giao dịch của cổng đã gắn với 1 lượt thanh toán khác
                await tx.RollbackAsync();
                _db.ChangeTracker.Clear();
                _logger.LogWarning(ex, "Mã giao dịch {TransactionId} đã được dùng cho lượt khác.", dto.TransactionId);
                return Result(PaymentCallbackOutcome.Rejected, orderId,
                    "Mã giao dịch đã được dùng cho lượt thanh toán khác.", "TRANSACTION_ALREADY_USED", order.Status);
            }

            await tx.CommitAsync();

            return grant
                ? Result(PaymentCallbackOutcome.Paid, orderId, "Thanh toán thành công, đã cấp quyền đọc.",
                    null, OrderService.Paid, true)
                : Result(PaymentCallbackOutcome.PaidGrantFailed, orderId,
                    "Đã ghi nhận thanh toán nhưng cấp quyền bị lỗi — Admin cần chạy lại.",
                    "GRANT_FAILED", OrderService.Paid, false);
        }

        // ---------- THẤT BẠI / HỦY: KHÔNG BAO GIỜ CẤP QUYỀN ----------
        if (pay.Status == PaySucceeded)
        {
            // Báo thất bại đến muộn sau khi đã thành công → không hạ trạng thái
            return await FinishWithoutChangeAsync(tx, evt, order, EvtIgnored, "ALREADY_SUCCEEDED",
                PaymentCallbackOutcome.Ignored);
        }

        var cancelled = dto.Status == CbCancelled;
        pay.Status = cancelled ? PayCancelled : PayFailed;
        pay.FailureCode = string.IsNullOrWhiteSpace(dto.FailureCode)
            ? (cancelled ? "CANCELLED" : "GATEWAY_FAILED")
            : dto.FailureCode;
        pay.VerifiedAt = now;

        if (order.Status == OrderService.Pending)
        {
            order.Status = cancelled ? OrderService.Cancelled : OrderService.Failed;
            order.UpdatedAt = now;
        }

        evt.ProcessingStatus = EvtProcessed;
        evt.ProcessedAt = now;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Result(cancelled ? PaymentCallbackOutcome.Cancelled : PaymentCallbackOutcome.Failed, orderId,
            cancelled ? "Giao dịch đã bị hủy — không cấp quyền." : "Thanh toán không thành công — không cấp quyền.",
            null, order.Status, false);
    }

    // Ghi sự kiện (Ignored) rồi kết thúc, không đổi Payment / Order
    private async Task<PaymentCallbackResult> FinishWithoutChangeAsync(
        IDbContextTransaction tx, PaymentEvent evt, Order order,
        string eventStatus, string reason, PaymentCallbackOutcome outcome)
    {
        evt.ProcessingStatus = eventStatus;
        evt.ErrorMessage = reason;
        evt.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var granted = await _grants.HasGrantAsync(order.OrderId);
        return Result(outcome, order.OrderId, MessageFor(reason), reason, order.Status, granted);
    }

    // Callback lặp: trả lại kết quả hiện tại của lượt thanh toán, không xử lý gì thêm
    private async Task<PaymentCallbackResult> BuildDuplicateResultAsync(int paymentId)
    {
        var info = await _db.Payments.AsNoTracking()
            .Where(p => p.PaymentId == paymentId)
            .Select(p => new { p.OrderId, OrderStatus = p.Order.Status })
            .FirstAsync();

        var granted = await _grants.HasGrantAsync(info.OrderId);
        return Result(PaymentCallbackOutcome.Duplicate, info.OrderId,
            "Callback này đã được xử lý trước đó.", "DUPLICATE_EVENT", info.OrderStatus, granted);
    }

    // ============================================================
    //  GIẢ LẬP CỔNG THANH TOÁN (chỉ Development — controller chặn ở môi trường khác)
    // ============================================================
    public async Task<PaymentCallbackResult?> SimulateAsync(
        int orderId, int userId, string status, string? transactionId, string? eventId)
    {
        var order = await _orders.GetByIdAsync(orderId, userId, isAdmin: false);
        if (order is null) return null;

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Chưa cấu hình Payment:SecretKey (đặt bằng dotnet user-secrets).");

        // Dùng lượt thanh toán gần nhất của đơn; chưa có thì mở lượt mới (cần đơn còn Pending)
        var payment = await _db.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync();

        if (payment is null)
        {
            var started = await StartAsync(orderId, userId);
            payment = await _db.Payments.AsNoTracking().FirstAsync(p => p.PaymentId == started!.PaymentId);
        }

        var callback = new PaymentCallbackDto
        {
            Provider = payment.Provider,
            EventId = string.IsNullOrWhiteSpace(eventId) ? $"EVT-{Guid.NewGuid():N}" : eventId.Trim(),
            MerchantRequestId = payment.MerchantRequestId,
            OrderId = orderId,
            TransactionId = string.IsNullOrWhiteSpace(transactionId) ? $"TXN-{Guid.NewGuid():N}" : transactionId.Trim(),
            Amount = order.Amount,
            Currency = Currency,
            Status = status
        };
        callback.Signature = PaymentSignature.Sign(_options.SecretKey, PaymentSignature.BuildPayload(callback));

        return await HandleCallbackAsync(callback);
    }

    // ============================================================
    //  PHỤC HỒI: đã thu tiền nhưng chưa có quyền
    // ============================================================
    public async Task<PagedResultDto<PaymentRecoveryDto>> GetNeedsRecoveryAsync(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Chỉ xét lượt thanh toán ĐÃ XÁC MINH thành công. Đơn Paid cũ/demo không có Payment thì KHÔNG tự đưa vào đây.
        var query = _db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaySucceeded
                        && !_db.StoryPurchases.Any(s => s.OrderId == p.OrderId)
                        && !_db.UserSubscriptions.Any(s => s.OrderId == p.OrderId));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.SucceededAt).ThenBy(p => p.PaymentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentRecoveryDto
            {
                PaymentId = p.PaymentId,
                OrderId = p.OrderId,
                UserId = p.Order.UserId,
                OrderType = p.Order.OrderType,
                ItemName = p.Order.ItemName,
                OrderStatus = p.Order.Status,
                Amount = p.ReceivedAmount,
                ProviderTransactionId = p.ProviderTransactionId,
                SucceededAt = p.SucceededAt,
                LastError = _db.PaymentEvents
                    .Where(e => e.PaymentId == p.PaymentId && e.ProcessingStatus == EvtFailed)
                    .OrderByDescending(e => e.PaymentEventId)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return new PagedResultDto<PaymentRecoveryDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RegrantResultDto?> RegrantAsync(int orderId)
    {
        if (!await _db.Orders.AnyAsync(o => o.OrderId == orderId))
            return null;

        // CHỈ chạy lại khi có thanh toán THÀNH CÔNG đã xác minh. Không có thì tuyệt đối không cấp quyền.
        var pay = await _db.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId && p.Status == PaySucceeded)
            .OrderBy(p => p.PaymentId)
            .FirstOrDefaultAsync();

        if (pay is null)
            throw new InvalidOperationException(
                "Đơn này chưa có thanh toán thành công đã xác minh nên không thể cấp quyền.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT `OrderId` FROM `Orders` WHERE `OrderId` = {orderId} FOR UPDATE");

        var order = await _db.Orders.FirstAsync(o => o.OrderId == orderId);
        var now = DateTime.UtcNow;
        var paidAt = pay.SucceededAt ?? now;   // tính từ lúc thanh toán thật, người mua không mất ngày dùng

        if (order.Status != OrderService.Paid)
        {
            order.Status = OrderService.Paid;
            order.PaidAt = paidAt;
            order.UpdatedAt = now;
        }

        // Không gọi cổng thanh toán → không thu thêm tiền. Đơn đã có quyền thì không thêm gì.
        var created = await _grants.AddGrantAsync(order, paidAt);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return new RegrantResultDto
        {
            OrderId = orderId,
            Granted = true,
            AlreadyGranted = !created,
            Message = created
                ? "Đã chạy lại và cấp quyền thành công."
                : "Đơn này đã được cấp quyền từ trước — không cấp thêm."
        };
    }

    // ============================================================
    //  HÀM PHỤ
    // ============================================================
    private Task<string> OrderStatusAsync(int orderId)
        => _db.Orders.AsNoTracking()
            .Where(o => o.OrderId == orderId)
            .Select(o => o.Status)
            .FirstAsync();

    // Lưu nội dung callback để đối soát — KHÔNG lưu chữ ký/khóa bí mật
    private static string BuildEventPayload(PaymentCallbackDto d)
        => Truncate(JsonSerializer.Serialize(new
        {
            d.Provider,
            d.EventId,
            d.MerchantRequestId,
            d.OrderId,
            d.TransactionId,
            d.Amount,
            d.Currency,
            d.Status,
            d.FailureCode
        }), 4000);

    private static string MessageFor(string reason) => reason switch
    {
        "WRONG_PROVIDER" => "Sai nhà cung cấp thanh toán.",
        "WRONG_CURRENCY" => "Sai đơn vị tiền tệ.",
        "ORDER_MISMATCH" => "Callback không thuộc đơn hàng này.",
        "AMOUNT_MISMATCH" => "Số tiền không khớp với đơn hàng.",
        "DUPLICATE_TRANSACTION" => "Giao dịch này đã được ghi nhận trước đó.",
        "TRANSACTION_MISMATCH" => "Lượt này đã thành công với mã giao dịch khác — cần đối soát.",
        "ALREADY_SUCCEEDED" => "Lượt thanh toán đã thành công trước đó — bỏ qua báo thất bại muộn.",
        _ => reason
    };

    private static PaymentCallbackResult Result(
        PaymentCallbackOutcome outcome, int orderId, string message,
        string? reason = null, string? orderStatus = null, bool granted = false)
        => new(outcome, orderId, message, reason, orderStatus, granted);

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max];

    private static bool IsDuplicateKey(Exception ex)
        => (ex as MySqlException ?? ex.InnerException as MySqlException)?.Number == 1062;
}