using Microsoft.Maui.Graphics;

namespace ReadManager.Mobile.Models;

// Lịch sử xu — GET api/reader/coin-history?page=&pageSize= (ReaderActivityController.History)
public class CoinTransaction
{
    public int Id { get; set; }
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // ----- Hiển thị -----
    public bool IsIncome => Amount >= 0;
    public string AmountText => Amount >= 0 ? $"+{Amount:N0}" : $"{Amount:N0}";
    public Color AmountColor => Amount >= 0 ? Color.FromArgb("#12A150") : Color.FromArgb("#DC2626");
    public string Icon => Type switch
    {
        "ChapterReward" => "📖",
        "FreezeCard" => "🧊",
        _ when Description.Contains("chuỗi", StringComparison.OrdinalIgnoreCase) => "🔥",
        _ => Amount >= 0 ? "🪙" : "🎟"
    };
    public string TimeText => TimeAgo.Of(CreatedAt);
}

public class CoinHistoryPage
{
    public List<CoinTransaction> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
