using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypeForge.App.ViewModels;

namespace TypeForge.App.Views;

public sealed partial class TestPage : Page
{
    public TestViewModel ViewModel { get; }

    public TestPage()
    {
        ViewModel = App.GetService<TestViewModel>();
        InitializeComponent();
    }

    private void StartTimed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string duration })
        {
            ViewModel.StartTimedTestCommand.Execute(duration);
        }
    }

    private void StartCustom_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartCustomTestCommand.Execute(null);
    }
}
