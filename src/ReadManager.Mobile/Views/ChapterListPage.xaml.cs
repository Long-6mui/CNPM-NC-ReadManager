using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class ChapterListPage : ContentPage
{
    public ChapterListPage(ChapterListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
