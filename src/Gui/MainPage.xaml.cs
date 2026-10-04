using Transponder.Features.Demo.ViewModels;

namespace Gui;

public partial class MainPage
{
    public MainPage(DemoViewModel viewModel)
    {
        BindingContext = ViewModel = viewModel;
        InitializeComponent();
    }

    public DemoViewModel ViewModel { get; }
}
