using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CleanSweep.Controls;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Scanning;
using CleanSweep.Core.Settings;
using CleanSweep.Core.Utils;

namespace CleanSweep;

public partial class MainWindow : Window
{
    private enum AppState
    {
        Idle,
        Scanning,
        Scanned,
        Cleaning,
        Cleaned
    }

    private string _drive;
    private AppState _state = AppState.Idle;
    private DiskStatisticsDialog? _diskStatisticsDialog;
    private MaintenanceToolsDialog? _maintenanceToolsDialog;
    private List<Category> _categories = new();
    private readonly Dictionary<string, CategoryRowControl> _rows = new(StringComparer.OrdinalIgnoreCase);

    private long _lastScanTotal;
    private long _freedBytes;
    private readonly List<string> _cleanErrors = new();
    private int _cleanErrorsOmitted;

    public MainWindow()
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);

        I18n.SetLanguage(AppSettings.LoadSavedLanguage());
        _drive = AppSettings.LoadSavedDrive();
        RetranslateUi();
    }

    // ------------------------------------------------------------------ UI Refresh

    private void RefreshDiskLabel()
    {
        try
        {
            var (total, _, free) = FileSystemUtils.DiskUsage(_drive);
            var args = new Dictionary<string, string>
            {
                ["free"] = FileSystemUtils.HumanSize(free),
                ["total"] = FileSystemUtils.HumanSize(total)
            };
            DiskLabel.Text = I18n.Tr("disk_free", args);
        }
        catch
        {
            DiskLabel.Text = string.Empty;
        }
    }

    private void UpdateScanButtonText()
    {
        ScanBtn.Content = _state switch
        {
            AppState.Scanning => I18n.Tr("scanning"),
            AppState.Scanned or AppState.Cleaned or AppState.Cleaning => I18n.Tr("scan_button_again"),
            _ => I18n.Tr("scan_button")
        };
    }

    private void UpdateRingDisplay()
    {
        var accentBrush = (Brush)Application.Current.FindResource("AccentBrush");
        var dangerBrush = (Brush)Application.Current.FindResource("DangerBrush");

        switch (_state)
        {
            case AppState.Idle:
                MainScanRing.Progress = 0.0;
                MainScanRing.RingBrush = accentBrush;
                MainScanRing.CenterText = I18n.Tr("ready_center");
                MainScanRing.SubText = I18n.Tr("ready_sub");
                break;

            case AppState.Scanning:
                MainScanRing.CenterText = I18n.Tr("scanning");
                MainScanRing.SubText = string.Empty;
                break;

            case AppState.Scanned:
                MainScanRing.RingBrush = accentBrush;
                if (_lastScanTotal > 0)
                {
                    MainScanRing.CenterText = FileSystemUtils.HumanSize(_lastScanTotal);
                    MainScanRing.SubText = I18n.Tr("junk_found_sub");
                }
                else
                {
                    MainScanRing.CenterText = I18n.Tr("no_junk_center");
                    MainScanRing.SubText = I18n.Tr("no_junk_sub");
                }
                break;

            case AppState.Cleaning:
                MainScanRing.RingBrush = dangerBrush;
                MainScanRing.CenterText = I18n.Tr("cleaning");
                MainScanRing.SubText = string.Empty;
                break;

            case AppState.Cleaned:
                MainScanRing.RingBrush = accentBrush;
                MainScanRing.CenterText = FileSystemUtils.HumanSize(_freedBytes);
                MainScanRing.SubText = I18n.Tr("freed_sub");
                break;
        }
    }

    private void UpdateFooter()
    {
        var selected = _categories.Where(c => c.Selected && c.Exists && string.IsNullOrEmpty(c.Error)).ToList();
        long total = selected.Sum(c => c.SizeBytes);

        if (selected.Count == 0)
        {
            FooterLabel.Text = I18n.Tr("nothing_selected");
            CleanBtn.IsEnabled = false;
        }
        else
        {
            string word = I18n.PluralCategoryWord(I18n.CurrentLanguage, selected.Count);
            string prefix = I18n.Tr("selected_prefix");
            FooterLabel.Text = $"{prefix}: {selected.Count} {word} · {FileSystemUtils.HumanSize(total)}";
            CleanBtn.IsEnabled = true;
        }
    }

    private void RetranslateUi()
    {
        UpdateScanButtonText();
        UpdateRingDisplay();
        RefreshDiskLabel();

        HintLabel.Text = I18n.Tr("hint_permanent");
        CleanBtn.Content = I18n.Tr("clean_button");
        StartupBtn.ToolTip = I18n.Tr("startup_button_tooltip");
        DiskStatsBtn.Content = I18n.Tr("disk_stats_title");
        MaintenanceToolsBtn.Content = I18n.Tr("maintenance_tools");
        SettingsBtn.ToolTip = I18n.Tr("settings_title");

        foreach (var row in _rows.Values)
        {
            row.Retranslate();
        }

        UpdateFooter();
    }

    private void ResetToIdle()
    {
        _rows.Clear();
        CategoriesPanel.Children.Clear();
        _categories.Clear();
        FooterBar.Visibility = Visibility.Collapsed;
        CleanBtn.IsEnabled = false;
        _state = AppState.Idle;
        UpdateScanButtonText();
        UpdateRingDisplay();
    }

    // ------------------------------------------------------------------ Header Actions

    private void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new StartupDialog
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(I18n.CurrentLanguage, _drive)
        {
            Owner = this
        };

        dialog.LanguageChanged += lang =>
        {
            RetranslateUi();
        };

        dialog.DriveChanged += drive =>
        {
            if (!string.Equals(_drive, drive, StringComparison.OrdinalIgnoreCase))
            {
                _drive = drive;
                RefreshDiskLabel();
                if (_state is AppState.Scanned or AppState.Cleaned)
                {
                    ResetToIdle();
                }
            }
        };

        dialog.ShowDialog();
    }

    private void OnDiskStatsClicked(object sender, RoutedEventArgs e)
    {
        if (_diskStatisticsDialog is { IsVisible: true } openDialog)
        {
            openDialog.Activate();
            return;
        }

        var dialog = new DiskStatisticsDialog(_drive)
        {
            Owner = this
        };
        _diskStatisticsDialog = dialog;
        dialog.Closed += (_, _) => _diskStatisticsDialog = null;
        dialog.Show();
    }

    private void OnMaintenanceToolsClicked(object sender, RoutedEventArgs e)
    {
        if (_maintenanceToolsDialog is { IsVisible: true } openDialog)
        {
            openDialog.Activate();
            return;
        }

        var dialog = new MaintenanceToolsDialog
        {
            Owner = this
        };
        _maintenanceToolsDialog = dialog;
        dialog.Closed += (_, _) => _maintenanceToolsDialog = null;
        dialog.Show();
    }

    // ------------------------------------------------------------------ Scanning

    private async void OnScanClicked(object sender, RoutedEventArgs e)
    {
        _rows.Clear();
        CategoriesPanel.Children.Clear();
        FooterBar.Visibility = Visibility.Collapsed;
        CleanBtn.IsEnabled = false;

        _categories = CategoryScanner.BuildCategories(_drive);

        foreach (var category in _categories)
        {
            var row = new CategoryRowControl(category);
            row.SelectionToggled += (_, _) => UpdateFooter();
            CategoriesPanel.Children.Add(row);
            _rows[category.Key] = row;
        }

        ScanBtn.IsEnabled = false;
        SettingsBtn.IsEnabled = false;
        DiskStatsBtn.IsEnabled = false;
        _state = AppState.Scanning;
        UpdateScanButtonText();

        MainScanRing.RingBrush = (Brush)Application.Current.FindResource("AccentBrush");
        MainScanRing.Progress = 0.0;
        UpdateRingDisplay();

        int scannedCount = 0;
        int totalCount = _categories.Count;

        await Task.Run(() =>
        {
            foreach (var category in _categories)
            {
                CategoryScanner.ScanCategory(category);
                scannedCount++;

                double currentProgress = scannedCount / (double)Math.Max(totalCount, 1);

                Dispatcher.Invoke(() =>
                {
                    if (_rows.TryGetValue(category.Key, out var rowControl))
                    {
                        rowControl.UpdateScannedState();
                    }
                    MainScanRing.Progress = currentProgress;
                    UpdateFooter();
                });
            }
        });

        ScanBtn.IsEnabled = true;
        SettingsBtn.IsEnabled = true;
        DiskStatsBtn.IsEnabled = true;
        _state = AppState.Scanned;
        _lastScanTotal = _categories.Where(c => string.IsNullOrEmpty(c.Error)).Sum(c => c.SizeBytes);

        UpdateScanButtonText();
        UpdateRingDisplay();
        FooterBar.Visibility = Visibility.Visible;
        UpdateFooter();
    }

    // ------------------------------------------------------------------ Cleaning

    private async void OnCleanClicked(object sender, RoutedEventArgs e)
    {
        var selected = _categories.Where(c => c.Selected && c.Exists && string.IsNullOrEmpty(c.Error)).ToList();
        if (selected.Count == 0) return;

        long totalSize = selected.Sum(c => c.SizeBytes);

        ScanBtn.IsEnabled = false;
        CleanBtn.IsEnabled = false;
        SettingsBtn.IsEnabled = false;
        DiskStatsBtn.IsEnabled = false;
        CategoriesPanel.IsEnabled = false;

        List<PreviewItem> previewItems;
        try
        {
            previewItems = await Task.Run(() =>
            {
                var items = new List<PreviewItem>();
                foreach (var category in selected)
                {
                    int remainingItems = PreviewDialog.MaximumVisibleItems - items.Count;
                    if (remainingItems <= 0)
                    {
                        break;
                    }
                    items.AddRange(CategoryCleaner.BuildPreviewItems(category, remainingItems));
                }
                return items;
            });
        }
        catch (Exception ex)
        {
            CategoriesPanel.IsEnabled = true;
            ScanBtn.IsEnabled = true;
            SettingsBtn.IsEnabled = true;
            DiskStatsBtn.IsEnabled = true;
            UpdateFooter();
            new AppMessageDialog(I18n.Tr("done_title"), ex.Message, AppMessageKind.Error)
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        var previewDialog = new PreviewDialog(
            previewItems,
            totalSize,
            selected.Sum(category => category.ItemCount))
        {
            Owner = this
        };

        if (previewDialog.ShowDialog() != true)
        {
            CategoriesPanel.IsEnabled = true;
            ScanBtn.IsEnabled = true;
            SettingsBtn.IsEnabled = true;
            DiskStatsBtn.IsEnabled = true;
            UpdateFooter();
            return;
        }

        _state = AppState.Cleaning;
        UpdateScanButtonText();

        MainScanRing.RingBrush = (Brush)Application.Current.FindResource("DangerBrush");
        MainScanRing.Progress = 0.0;
        UpdateRingDisplay();

        int cleanTotal = selected.Count;
        int cleanDone = 0;
        _freedBytes = 0;
        _cleanErrors.Clear();
        _cleanErrorsOmitted = 0;

        await Task.Run(() =>
        {
            foreach (var category in selected)
            {
                var result = CategoryCleaner.CleanCategory(category);
                cleanDone++;
                _freedBytes += result.FreedBytes;

                foreach (var err in result.Errors)
                {
                    if (_cleanErrors.Count < 100)
                    {
                        var args = new Dictionary<string, string> { ["detail"] = err.Detail };
                        _cleanErrors.Add(I18n.Tr(err.Key, args));
                    }
                    else
                    {
                        _cleanErrorsOmitted++;
                    }
                }
                _cleanErrorsOmitted += result.OmittedErrorsCount;

                double progress = cleanDone / (double)Math.Max(cleanTotal, 1);

                Dispatcher.Invoke(() =>
                {
                    if (_rows.TryGetValue(category.Key, out var rowControl))
                    {
                        rowControl.MarkCleaned();
                    }
                    MainScanRing.Progress = progress;
                });
            }
        });

        ScanBtn.IsEnabled = true;
        SettingsBtn.IsEnabled = true;
        DiskStatsBtn.IsEnabled = true;
        CategoriesPanel.IsEnabled = true;
        _state = AppState.Cleaned;

        MainScanRing.RingBrush = (Brush)Application.Current.FindResource("AccentBrush");
        UpdateScanButtonText();
        UpdateRingDisplay();
        RefreshDiskLabel();
        UpdateFooter();

        string message = I18n.Tr("done_text", new Dictionary<string, string>
        {
            ["size"] = FileSystemUtils.HumanSize(_freedBytes)
        });

        if (_cleanErrors.Count > 0 || _cleanErrorsOmitted > 0)
        {
            if (_cleanErrors.Count > 0)
            {
                var shownErrors = _cleanErrors.Take(5).ToList();
                message += I18n.Tr("skipped_intro") + string.Join("\n", shownErrors);
                int errorsNotShown = _cleanErrorsOmitted + _cleanErrors.Count - shownErrors.Count;
                if (errorsNotShown > 0)
                {
                    message += "\n" + I18n.Tr("cleanup_more_errors_omitted", new Dictionary<string, string>
                    {
                        ["count"] = errorsNotShown.ToString("N0")
                    });
                }
            }
            else if (_cleanErrorsOmitted > 0)
            {
                message += "\n" + I18n.Tr("cleanup_more_errors_omitted", new Dictionary<string, string>
                {
                    ["count"] = _cleanErrorsOmitted.ToString("N0")
                });
            }
        }

        new AppMessageDialog(
            I18n.Tr("done_title"),
            message,
            _cleanErrors.Count > 0 || _cleanErrorsOmitted > 0
                ? AppMessageKind.Warning
                : AppMessageKind.Success)
        {
            Owner = this
        }.ShowDialog();
    }
}