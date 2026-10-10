using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Utils;

namespace CleanSweep.Controls;

public partial class CategoryRowControl : UserControl
{
    public enum DisplayState
    {
        Pending,
        Normal,
        Empty,
        Error,
        Cleaned
    }

    public Category Category { get; }
    private DisplayState _state = DisplayState.Pending;
    public event EventHandler<bool>? SelectionToggled;

    public CategoryRowControl(Category category)
    {
        InitializeComponent();
        Category = category;

        IconControl.CategoryKey = category.Key;
        CategoryCheckBox.IsChecked = category.Selected;

        Retranslate();
    }

    public void UpdateScannedState()
    {
        if (!string.IsNullOrEmpty(Category.Error))
        {
            _state = DisplayState.Error;
            CategoryCheckBox.IsChecked = false;
            CategoryCheckBox.IsEnabled = false;
        }
        else if (!Category.Exists)
        {
            _state = DisplayState.Empty;
            CategoryCheckBox.IsChecked = false;
            CategoryCheckBox.IsEnabled = false;
        }
        else
        {
            _state = DisplayState.Normal;
            CategoryCheckBox.IsEnabled = true;
        }

        RefreshSizeLabel();
    }

    public void MarkCleaned()
    {
        _state = DisplayState.Cleaned;
        CategoryCheckBox.IsEnabled = false;
        CategoryCheckBox.IsChecked = false;
        RefreshSizeLabel();
    }

    public void Retranslate()
    {
        NameText.Text = I18n.Tr($"cat_{Category.Key}_name");
        DescText.Text = I18n.Tr($"cat_{Category.Key}_desc");
        RefreshSizeLabel();
    }

    private void RefreshSizeLabel()
    {
        switch (_state)
        {
            case DisplayState.Error:
                string errorKey = Category.Error ?? "access_denied";
                SizeText.Text = errorKey == "access_denied" ? I18n.Tr("access_denied") : errorKey;
                SizeText.Foreground = (Brush)Application.Current.FindResource("WarningBrush");
                break;

            case DisplayState.Empty:
                SizeText.Text = I18n.Tr("empty_label");
                SizeText.Foreground = (Brush)Application.Current.FindResource("TextFaintBrush");
                break;

            case DisplayState.Cleaned:
                SizeText.Text = I18n.Tr("clean_label");
                SizeText.Foreground = (Brush)Application.Current.FindResource("AccentBrush");
                break;

            case DisplayState.Normal:
                SizeText.Text = FileSystemUtils.HumanSize(Category.SizeBytes);
                SizeText.Foreground = (Brush)Application.Current.FindResource("AccentBrush");
                break;

            default:
                SizeText.Text = "…";
                SizeText.Foreground = (Brush)Application.Current.FindResource("TextDimBrush");
                break;
        }
    }

    private void OnCheckedStateChanged(object sender, RoutedEventArgs e)
    {
        bool isChecked = CategoryCheckBox.IsChecked == true;
        Category.Selected = isChecked;
        SelectionToggled?.Invoke(this, isChecked);
    }
}

