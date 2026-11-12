using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TypeForge.Core.Branding;

namespace TypeForge.App.Controls;

public sealed partial class EmptyStateView : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(EmptyStateKind),
        typeof(EmptyStateView),
        new PropertyMetadata(EmptyStateKind.NoStats, OnKindChanged));

    public EmptyStateView()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => ApplyThemePanel();
        Loaded += (_, _) =>
        {
            Render();
            ApplyThemePanel();
        };
    }

    public EmptyStateKind Kind
    {
        get => (EmptyStateKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Raised when the primary action button is clicked.</summary>
    public event EventHandler<EmptyStateKind>? ActionRequested;

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateView view && view.IsLoaded)
        {
            view.Render();
        }
    }

    private void Render()
    {
        EmptyStateContent content = EmptyStateCatalog.Get(Kind);
        Illustration.Source = new BitmapImage(new Uri($"ms-appx:///Assets/Illustrations/{content.AssetStem}.png"));
        TitleText.Text = content.Title;
        MessageText.Text = content.Message;
        ActionButton.Content = content.ActionLabel;
        ActionButton.Visibility = content.ActionLabel is null ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(content.ActionLabel))
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ActionButton, content.ActionLabel);
        }
    }

    // Art is dark-only. In Light theme, seat it on a dark #0D0D0D rounded panel so it stays readable.
    private void ApplyThemePanel()
    {
        bool light = ActualTheme == ElementTheme.Light;
        Panel.Background = light ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0D, 0x0D, 0x0D)) : null;
        Panel.Padding = light ? new Thickness(16) : new Thickness(0);
    }

    private void OnActionClick(object sender, RoutedEventArgs e) => ActionRequested?.Invoke(this, Kind);
}
