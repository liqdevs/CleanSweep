using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CleanSweep.Core.Localization;

namespace CleanSweep;

public enum AppMessageKind
{
    Success,
    Warning,
    Error
}

public partial class AppMessageDialog : Window
{
    public AppMessageDialog(string title, string message, AppMessageKind kind = AppMessageKind.Success)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = I18n.Tr("message_ok");

        string brushKey = kind switch
        {
            AppMessageKind.Warning => "WarningBrush",
            AppMessageKind.Error => "DangerBrush",
            _ => "AccentBrush"
        };
        IconBorder.Background = (Brush)Application.Current.FindResource(brushKey);
        IconText.Text = kind switch
        {
            AppMessageKind.Warning => "!",
            AppMessageKind.Error => "×",
            _ => "✓"
        };
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
