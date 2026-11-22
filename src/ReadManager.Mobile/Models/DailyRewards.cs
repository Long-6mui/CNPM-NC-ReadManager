using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using ReadManager.Mobile.Services;

namespace ReadManager.Mobile.Models;

// GET api/me/daily-rewards — điểm danh 7 ngày trong tuần + nhiệm vụ hôm nay (DailyRewardsController)
public class DailyRewards
{
    public string Today { get; set; } = string.Empty;
    public long Coins { get; set; }
    public bool CheckedInToday { get; set; }
    public int WeekClaimed { get; set; }
    public List<CheckInDay> Days { get; set; } = new();
    public List<DailyMission> Missions { get; set; } = new();
}

// Một ô điểm danh. Status: claimed | available (hôm nay, chưa nhận) | missed | upcoming
public class CheckInDay
{
    public string Date { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Status { get; set; } = "upcoming";

    public bool IsClaimed => Status == "claimed";
    public bool IsAvailable => Status == "available";
    public bool IsMissed => Status == "missed";
    public bool IsToday => Label == "Hôm nay";

    // Ô hôm nay chưa nhận: hiện "+10 xu"; đã nhận: ✓; còn lại: hộp quà
    public string Icon => IsClaimed ? "✅" : IsAvailable ? "🪙" : IsMissed ? "🎁" : "🎁";
    public string AmountText => IsClaimed ? "Đã nhận" : $"+{Amount}";
    public double IconOpacity => IsMissed ? 0.35 : IsClaimed ? 0.9 : 1;

    public Color CellColor => IsAvailable
        ? ThemeService.Get("WebWarnBg")
        : ThemeService.Get("WebSurface2");
    public Color CellStroke => IsAvailable ? Color.FromArgb("#F59E0B") : Colors.Transparent;
    public Color LabelColor => IsToday ? Color.FromArgb("#F59E0B") : ThemeService.Get("WebMuted");
}

// Một nhiệm vụ hằng ngày
public partial class DailyMission : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int Target { get; set; } = 1;
    public int Reward { get; set; }
    public bool Completed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClaim))]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    [NotifyPropertyChangedFor(nameof(ButtonColor))]
    [NotifyPropertyChangedFor(nameof(ButtonTextColor))]
    private bool claimed;

    public string ProgressText => $"{Progress}/{Target}";
    public string RewardText => $"+{Reward} xu";
    public double ProgressValue => Target <= 0 ? 0 : Math.Clamp(Progress / (double)Target, 0, 1);
    public bool CanClaim => Completed && !Claimed;
    public string ButtonText => Claimed ? "Đã nhận" : Completed ? "Nhận" : ProgressText;
    public Color ButtonColor => CanClaim ? Color.FromArgb("#E11D48") : ThemeService.Get("WebSurface2");
    public Color ButtonTextColor => CanClaim ? Colors.White : ThemeService.Get("WebMuted");
    public string Icon => Key switch
    {
        "read1" => "📖",
        "read3" => "📚",
        "comment" => "💬",
        _ => "⭐"
    };
}

// POST check-in / claim → số xu cộng thêm + số dư
public class RewardClaimResult
{
    public int Amount { get; set; }
    public long Coins { get; set; }
}
