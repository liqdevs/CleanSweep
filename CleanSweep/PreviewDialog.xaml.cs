using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Utils;

namespace CleanSweep;

public partial class PreviewDialog : Window
{
    private readonly List<PreviewItem> _previewItems;
    private readonly long _totalSize;

    public PreviewDialog(List<PreviewItem> previewItems, long totalSize)
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);
        _previewItems = previewItems;
        _totalSize = totalSize;

        PopulateItems();
        Retranslate();
    }

    private void PopulateItems()
    {
        ItemsPanel.Children.Clear();
        ItemsPanel.Children.Add(EmptyText);

        if (_previewItems.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        EmptyText.Visibility = Visibility.Collapsed;

        var greenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CBF66"));
        var yellowBrush = (Brush)Application.Current.FindResource("WarningBrush");
        var dangerBrush = (Brush)Application.Current.FindResource("DangerBrush");

        greenBrush.Freeze();

        foreach (var item in _previewItems)
        {
            var rowBorder = new Border
            {
                Background = (Brush)Application.Current.FindResource("SurfaceBrush"),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dotBrush = item.Risk switch
            {
                "yellow" => yellowBrush,
                "red" => dangerBrush,
                _ => greenBrush
            };

            var dot = new TextBlock
            {
                Text = "●",
                FontSize = 14,
                Foreground = dotBrush,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(dot, 0);

            var pathText = new TextBlock
            {
                Text = item.Path,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = (Brush)Application.Current.FindResource("TextBrush"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(pathText, 1);

            string riskDesc = I18n.Tr($"preview_risk_{item.Risk}");
            if (!string.IsNullOrEmpty(item.ImportanceKey))
            {
                riskDesc += " · " + I18n.Tr(item.ImportanceKey);
            }
            if (item.IsRecent)
            {
                riskDesc += " · " + I18n.Tr(item.AllowRecentDeletion
                    ? "preview_recent_warning"
                    : "preview_recent_skipped");
            }
            string itemType = item.IsDirectory ? I18n.Tr("preview_folder") + " · " : string.Empty;
            string sizeText = item.IsDirectory ? FileSystemUtils.HumanSize(item.SizeBytes) : FileSystemUtils.HumanSize(item.SizeBytes);
            var metaText = new TextBlock
            {
                Text = $"{itemType}{FileSystemUtils.HumanSize(item.SizeBytes)} · {riskDesc}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(metaText, 2);

            grid.Children.Add(dot);
            grid.Children.Add(pathText);
            grid.Children.Add(metaText);
            rowBorder.Child = grid;

            ItemsPanel.Children.Add(rowBorder);
        }
    }

    public void Retranslate()
    {
        Title = I18n.Tr("preview_title");
        TitleText.Text = I18n.Tr("preview_title");

        var summaryArgs = new Dictionary<string, string>
        {
            ["count"] = _previewItems.Count.ToString(),
            ["size"] = FileSystemUtils.HumanSize(_totalSize)
        };
        SummaryText.Text = I18n.Tr("preview_summary", summaryArgs)
            + "\n"
            + I18n.Tr("preview_importance_note");

        LegendGreenText.Text = I18n.Tr("preview_risk_green");
        LegendYellowText.Text = I18n.Tr("preview_risk_yellow");
        LegendRedText.Text = I18n.Tr("preview_risk_red");

        EmptyText.Text = I18n.Tr("preview_empty");

        CancelButton.Content = I18n.Tr("preview_cancel");
        ContinueButton.Content = I18n.Tr("preview_continue");
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnContinueClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
