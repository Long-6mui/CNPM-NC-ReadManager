using System.ComponentModel;
using ReadManager.Mobile.Behaviors;
using ReadManager.Mobile.Services;
using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StoryListPage : ContentPage
{
    private readonly StoryListViewModel _viewModel;
    private bool _firstAppear = true;
    private bool _preferencesDirty;

    public StoryListPage(StoryListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Hero "sách bay": lấy tối đa 5 truyện mới cập nhật; bấm sách -> mở truyện, bấm "Tìm truyện" -> trang tìm kiếm
        _viewModel.Updated.CollectionChanged += (_, _) => RefreshHero();
        Hero.StoryTapped += (_, story) => _viewModel.GoToDetailCommand.Execute(story);
        Hero.BrowseTapped += (_, _) => _viewModel.GoToSearchCommand.Execute(null);

        // Bảng xếp hạng tải xong -> hiện dần (giống hiệu ứng chuyển tab của web)
        _viewModel.PropertyChanged += OnViewModelChanged;

        // Vừa lưu sở thích (sau khi đăng ký / ở trang Tài khoản) -> mục "Đề xuất cho bạn" cập nhật ngay
        RecommendationService.PreferencesChanged += (_, _) => _preferencesDirty = true;
    }

    private void RefreshHero() => Dispatcher.Dispatch(() => Hero.SetStories(_viewModel.Updated));

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StoryListViewModel.IsRankingBusy) && !_viewModel.IsRankingBusy && _viewModel.HasRanking)
            _ = FadeInAsync(RankingBlock);
    }

    private static async Task FadeInAsync(VisualElement view)
    {
        view.Opacity = 0.2;
        view.TranslationY = 10;
        await Task.WhenAll(view.FadeTo(1, 320, Easing.CubicOut), view.TranslateTo(0, 0, 360, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Hero.Stop();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RefreshHero();
        Hero.Start();
        _ = Header.RefreshAsync();

        if (_firstAppear)
        {
            _firstAppear = false;
            _ = Motion.PageEnterAsync(Body);
        }

        _preferencesDirty = false;
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Sở thích được lưu trong lúc trang đang tải -> tải lại mục đề xuất một lần nữa
        if (_preferencesDirty)
        {
            _preferencesDirty = false;
            await _viewModel.LoadRecommendationsAsync();
        }
    }
}
