using System.ComponentModel;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StreakPage : ContentPage
{
    private const string PulseName = "frame-pulse";
    private readonly StreakViewModel _viewModel;

    public StreakPage(StreakViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
        UpdatePulse();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.AbortAnimation(PulseName);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreakViewModel.Info)) UpdatePulse();
    }

    // Khung "Lửa Kim Cương" có animation nhấp nháy (phóng to / thu nhỏ nhẹ)
    private void UpdatePulse()
    {
        this.AbortAnimation(PulseName);
        AvatarPreview.Scale = 1;
        if (!_viewModel.Info.IsDiamondFrame) return;

        var anim = new Animation
        {
            { 0.0, 0.5, new Animation(v => AvatarPreview.Scale = v, 1.0, 1.1, Easing.SinInOut) },
            { 0.5, 1.0, new Animation(v => AvatarPreview.Scale = v, 1.1, 1.0, Easing.SinInOut) }
        };
        anim.Commit(this, PulseName, rate: 16, length: 1400, easing: null, finished: null, repeat: () => true);
    }
}
