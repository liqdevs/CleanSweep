using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Startup;

namespace CleanSweep;

public partial class StartupDialog : Window
{
    private readonly List<StartupItem> _items = new();
    private readonly Dictionary<string, (CheckBox cb, TextBlock badge, StartupItem item)> _rowControls = new();

    public StartupDialog()
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);
        LoadItems();
        Retranslate();
    }

    private void LoadItems()
    {
        _items.Clear();
        _rowControls.Clear();
        ItemsPanel.Children.Clear();
        ItemsPanel.Children.Add(EmptyText);

        var items = StartupManager.ListStartupItems();
        _items.AddRange(items);

        if (_items.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        EmptyText.Visibility = Visibility.Collapsed;

        foreach (var item in _items)
        {
            var rowBorder = new Border
            {
                Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cb = new CheckBox
            {
                Style = (Style)Application.Current.FindResource("StartupToggleStyle"),
                IsChecked = item.Enabled,
                IsEnabled = !item.NeedsAdmin,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(cb, 0);

            var textStack = new StackPanel
            {
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textStack, 1);

            var nameText = new TextBlock
            {
                Text = item.Name,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("TextBrush")
            };

            var cmdText = new TextBlock
            {
                Text = item.Command,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            };

            var badgeText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                Foreground = (Brush)Application.Current.FindResource("TextFaintBrush")
            };

            textStack.Children.Add(nameText);
            textStack.Children.Add(cmdText);
            textStack.Children.Add(badgeText);

            grid.Children.Add(cb);
            grid.Children.Add(textStack);
            rowBorder.Child = grid;

            cb.Checked += (s, e) => OnToggle(item, cb, true);
            cb.Unchecked += (s, e) => OnToggle(item, cb, false);

            _rowControls[item.Id] = (cb, badgeText, item);
            ItemsPanel.Children.Add(rowBorder);
        }
    }

    private void OnToggle(StartupItem item, CheckBox cb, bool enabled)
    {
        if (item.Enabled == enabled) return;

        bool success = StartupManager.SetStartupEnabled(item, enabled);
        if (success)
        {
            item.Enabled = enabled;
        }
        else
        {
            cb.IsChecked = !enabled;
            MessageBox.Show(
                this,
                I18n.Tr("startup_toggle_failed"),
                I18n.Tr("startup_title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void Retranslate()
    {
        Title = I18n.Tr("startup_title");
        TitleText.Text = I18n.Tr("startup_title");
        HintText.Text = I18n.Tr("startup_hint");
        EmptyText.Text = I18n.Tr("startup_empty");
        CloseButton.Content = I18n.Tr("settings_close");

        foreach (var (_, (cb, badge, item)) in _rowControls)
        {
            string badgeStr = I18n.Tr($"startup_source_{item.Source}");
            if (item.NeedsAdmin)
            {
                badgeStr += " · " + I18n.Tr("startup_admin_badge");
            }
            badge.Text = badgeStr;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
