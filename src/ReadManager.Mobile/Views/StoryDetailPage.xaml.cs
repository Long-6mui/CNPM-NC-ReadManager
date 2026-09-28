using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class StoryDetailPage : ContentPage
{
    public StoryDetailPage(StoryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
