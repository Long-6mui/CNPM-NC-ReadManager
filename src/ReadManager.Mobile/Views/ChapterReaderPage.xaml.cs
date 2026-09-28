using ReadManager.Mobile.ViewModels;

namespace ReadManager.Mobile.Views;

public partial class ChapterReaderPage : ContentPage
{
    public ChapterReaderPage(ChapterReaderViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
