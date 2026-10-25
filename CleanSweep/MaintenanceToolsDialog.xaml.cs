using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Scanning;
using CleanSweep.Core.Utils;
using Microsoft.Win32;

namespace CleanSweep;

public partial class MaintenanceToolsDialog : Window
{
    private const int MaximumVisibleResults = 500;

    private sealed record DuplicateCandidate(DuplicateFile File, string Hash);

    private readonly List<(CheckBox CheckBox, DuplicateCandidate Candidate)> _duplicateCandidates = new();
    private readonly List<(CheckBox CheckBox, string Path)> _emptyFolderCandidates = new();
    private EmptyFolderScanResult? _emptyFolderResult;
    private string? _emptyFolderScanRoot;
    private bool _operationRunning;

    public MaintenanceToolsDialog()
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);
        DuplicateRootBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        EmptyFolderRootBox.Text = DuplicateRootBox.Text;
        Retranslate();
    }

    private void Retranslate()
    {
        Title = I18n.Tr("maintenance_title");
        TitleText.Text = I18n.Tr("maintenance_title");
        SubtitleText.Text = I18n.Tr("maintenance_subtitle");
        DuplicatesTabText.Text = I18n.Tr("maintenance_duplicates");
        EmptyFoldersTabText.Text = I18n.Tr("maintenance_empty_folders");
        DuplicatesHint.Text = I18n.Tr("maintenance_duplicate_hint");
        EmptyFoldersHint.Text = I18n.Tr("maintenance_empty_hint");
        BrowseDuplicatesButton.Content = I18n.Tr("maintenance_browse");
        BrowseEmptyButton.Content = I18n.Tr("maintenance_browse");
        ScanDuplicatesButton.Content = I18n.Tr("maintenance_scan");
        ScanEmptyButton.Content = I18n.Tr("maintenance_scan");
        SelectDuplicateCopiesButton.Content = I18n.Tr("maintenance_select_copies");
        DeleteDuplicateButton.Content = I18n.Tr("maintenance_delete_selected");
        DeleteEmptyButton.Content = I18n.Tr("maintenance_delete_empty");
        CloseButton.Content = I18n.Tr("maintenance_close");
    }

    private void OnBrowseDuplicatesClicked(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(DuplicateRootBox);
    }

    private void OnBrowseEmptyClicked(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(EmptyFolderRootBox);
    }

    private void BrowseForFolder(TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = I18n.Tr("maintenance_browse"),
            InitialDirectory = Directory.Exists(target.Text)
                ? target.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private async void OnScanDuplicatesClicked(object sender, RoutedEventArgs e)
    {
        if (_operationRunning)
        {
            return;
        }

        string root = DuplicateRootBox.Text.Trim();
        _duplicateCandidates.Clear();
        DuplicateResultsPanel.Children.Clear();
        DeleteDuplicateButton.IsEnabled = false;
        SelectDuplicateCopiesButton.IsEnabled = false;
        _operationRunning = true;
        SetScanningControls(true);
        DuplicateStatus.Text = I18n.Tr("maintenance_duplicates_scanning", new Dictionary<string, string>
        {
            ["count"] = "0",
            ["path"] = root
        });

        try
        {
            var progress = new Progress<DuplicateScanProgress>(update =>
            {
                DuplicateStatus.Text = I18n.Tr("maintenance_duplicates_scanning", new Dictionary<string, string>
                {
                    ["count"] = update.FilesScanned.ToString("N0"),
                    ["path"] = update.CurrentPath
                });
            });
            DuplicateScanResult result = await Task.Run(
                () => DuplicateFileScanner.Scan(new[] { root }, progress: progress));
            PopulateDuplicateResults(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            DuplicateStatus.Text = string.Empty;
            ShowMessage(I18n.Tr("maintenance_title"), I18n.Tr("maintenance_scan_error", new Dictionary<string, string>
            {
                ["error"] = ex.Message
            }), AppMessageKind.Error);
        }
        finally
        {
            _operationRunning = false;
            SetScanningControls(false);
        }
    }

    private void PopulateDuplicateResults(DuplicateScanResult result)
    {
        int totalCandidates = result.Groups.Sum(group => Math.Max(0, group.Files.Count - 1));
        var candidates = new List<DuplicateCandidate>(Math.Min(totalCandidates, MaximumVisibleResults));
        foreach (DuplicateFileGroup group in result.Groups)
        {
            for (int index = 1; index < group.Files.Count && candidates.Count < MaximumVisibleResults; index++)
            {
                candidates.Add(new DuplicateCandidate(group.Files[index], group.Hash));
            }
            if (candidates.Count >= MaximumVisibleResults)
            {
                break;
            }
        }

        foreach (DuplicateCandidate candidate in candidates)
        {
            var checkBox = CreateResultCheckBox(
                candidate.File.Path,
                $"{FileSystemUtils.HumanSize(candidate.File.SizeBytes)} · {candidate.File.LastWriteTimeUtc.ToLocalTime():g}");
            checkBox.Checked += OnDuplicateSelectionChanged;
            checkBox.Unchecked += OnDuplicateSelectionChanged;
            _duplicateCandidates.Add((checkBox, candidate));
            DuplicateResultsPanel.Children.Add(checkBox);
        }

        if (totalCandidates == 0)
        {
            AddEmptyResult(DuplicateResultsPanel);
            DuplicateStatus.Text = I18n.Tr("maintenance_duplicates_none", new Dictionary<string, string>
            {
                ["files"] = result.FilesScanned.ToString("N0"),
                ["errors"] = result.AccessErrors.ToString("N0")
            });
            return;
        }

        SelectDuplicateCopiesButton.IsEnabled = candidates.Count > 0;
        UpdateDuplicateSelectionState();
        string summary = I18n.Tr("maintenance_duplicates_found", new Dictionary<string, string>
        {
            ["files"] = result.FilesScanned.ToString("N0"),
            ["groups"] = result.Groups.Count.ToString("N0"),
            ["copies"] = totalCandidates.ToString("N0"),
            ["errors"] = result.AccessErrors.ToString("N0")
        });
        if (candidates.Count < totalCandidates)
        {
            summary += " " + I18n.Tr("maintenance_duplicates_limited", new Dictionary<string, string>
            {
                ["shown"] = candidates.Count.ToString("N0"),
                ["total"] = totalCandidates.ToString("N0")
            });
        }
        DuplicateStatus.Text = summary;
    }

    private void OnDuplicateSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateDuplicateSelectionState();
    }

    private void UpdateDuplicateSelectionState()
    {
        DeleteDuplicateButton.IsEnabled = !_operationRunning && _duplicateCandidates.Any(item => item.CheckBox.IsChecked == true);
    }

    private void OnSelectDuplicateCopiesClicked(object sender, RoutedEventArgs e)
    {
        foreach ((CheckBox checkBox, _) in _duplicateCandidates)
        {
            checkBox.IsChecked = true;
        }
    }

    private async void OnDeleteDuplicatesClicked(object sender, RoutedEventArgs e)
    {
        List<DuplicateCandidate> selected = _duplicateCandidates
            .Where(item => item.CheckBox.IsChecked == true)
            .Select(item => item.Candidate)
            .ToList();
        if (selected.Count == 0 || _operationRunning)
        {
            return;
        }

        List<PreviewItem> previewItems = selected
            .Select(item => new PreviewItem(
                item.File.Path,
                item.File.SizeBytes,
                risk: "yellow",
                isRecent: IsRecentlyModified(item.File.LastWriteTimeUtc),
                importanceKey: "preview_importance_user",
                allowRecentDeletion: true))
            .ToList();
        long size = selected.Sum(item => item.File.SizeBytes);
        var preview = new PreviewDialog(previewItems, size, selected.Count, I18n.Tr("maintenance_confirm_duplicates"))
        {
            Owner = this
        };
        if (preview.ShowDialog() != true)
        {
            return;
        }

        _operationRunning = true;
        SetScanningControls(true);
        DuplicateStatus.Text = I18n.Tr("scanning");
        try
        {
            var verification = await Task.Run(() =>
            {
                var unchanged = new List<string>();
                int changed = 0;
                foreach (DuplicateCandidate candidate in selected)
                {
                    try
                    {
                        if (DuplicateFileScanner.MatchesContentHash(
                            candidate.File.Path,
                            candidate.File.SizeBytes,
                            candidate.Hash))
                        {
                            unchanged.Add(candidate.File.Path);
                        }
                        else
                        {
                            changed++;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        changed++;
                    }
                }
                return (unchanged, changed);
            });
            CleanResult result = await Task.Run(() =>
                CategoryCleaner.CleanFiles(verification.unchanged, allowRecentFiles: true));

            DuplicateStatus.Text = I18n.Tr("maintenance_delete_done", new Dictionary<string, string>
            {
                ["count"] = result.RemovedItems.ToString("N0"),
                ["size"] = FileSystemUtils.HumanSize(result.FreedBytes)
            });
            if (verification.changed > 0)
            {
                DuplicateStatus.Text += " " + I18n.Tr("maintenance_changed_files", new Dictionary<string, string>
                {
                    ["count"] = verification.changed.ToString("N0")
                });
            }
            if (result.Errors.Count > 0 || result.SkippedItems > 0)
            {
                ShowMessage(I18n.Tr("done_title"),
                    DuplicateStatus.Text + Environment.NewLine + I18n.Tr("skipped_intro")
                    + string.Join(Environment.NewLine, result.Errors.Select(error => error.Detail)),
                    AppMessageKind.Warning);
            }
            _duplicateCandidates.Clear();
            DuplicateResultsPanel.Children.Clear();
            AddEmptyResult(DuplicateResultsPanel);
            DeleteDuplicateButton.IsEnabled = false;
            SelectDuplicateCopiesButton.IsEnabled = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            ShowMessage(I18n.Tr("maintenance_title"), ex.Message, AppMessageKind.Error);
        }
        finally
        {
            _operationRunning = false;
            SetScanningControls(false);
            UpdateDuplicateSelectionState();
        }
    }

    private async void OnScanEmptyClicked(object sender, RoutedEventArgs e)
    {
        if (_operationRunning)
        {
            return;
        }

        string root = EmptyFolderRootBox.Text.Trim();
        _emptyFolderCandidates.Clear();
        _emptyFolderResult = null;
        _emptyFolderScanRoot = null;
        EmptyFolderResultsPanel.Children.Clear();
        DeleteEmptyButton.IsEnabled = false;
        _operationRunning = true;
        SetScanningControls(true);
        EmptyFolderStatus.Text = I18n.Tr("maintenance_empty_scanning");

        try
        {
            EmptyFolderScanResult result = await Task.Run(() => EmptyFolderCleaner.FindEmptyDirectories(root));
            _emptyFolderResult = result;
            _emptyFolderScanRoot = Path.GetFullPath(root);
            PopulateEmptyFolderResults(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            EmptyFolderStatus.Text = string.Empty;
            ShowMessage(I18n.Tr("maintenance_title"), I18n.Tr("maintenance_scan_error", new Dictionary<string, string>
            {
                ["error"] = ex.Message
            }), AppMessageKind.Error);
        }
        finally
        {
            _operationRunning = false;
            SetScanningControls(false);
        }
    }

    private void PopulateEmptyFolderResults(EmptyFolderScanResult result)
    {
        List<string> visiblePaths = result.EmptyDirectories.Take(MaximumVisibleResults).ToList();
        foreach (string path in visiblePaths)
        {
            var checkBox = CreateResultCheckBox(path, I18n.Tr("preview_importance_empty_folder"));
            checkBox.Checked += OnEmptyFolderSelectionChanged;
            checkBox.Unchecked += OnEmptyFolderSelectionChanged;
            _emptyFolderCandidates.Add((checkBox, path));
            EmptyFolderResultsPanel.Children.Add(checkBox);
        }

        if (visiblePaths.Count == 0)
        {
            AddEmptyResult(EmptyFolderResultsPanel);
            EmptyFolderStatus.Text = I18n.Tr("maintenance_empty_none", new Dictionary<string, string>
            {
                ["directories"] = result.DirectoriesScanned.ToString("N0"),
                ["errors"] = result.AccessErrors.ToString("N0")
            });
            return;
        }

        UpdateEmptyFolderSelectionState();
        EmptyFolderStatus.Text = I18n.Tr("maintenance_empty_found", new Dictionary<string, string>
        {
            ["directories"] = result.DirectoriesScanned.ToString("N0"),
            ["count"] = result.EmptyDirectories.Count.ToString("N0"),
            ["errors"] = result.AccessErrors.ToString("N0")
        });
        if (visiblePaths.Count < result.EmptyDirectories.Count)
        {
            EmptyFolderStatus.Text += " " + I18n.Tr("maintenance_duplicates_limited", new Dictionary<string, string>
            {
                ["shown"] = visiblePaths.Count.ToString("N0"),
                ["total"] = result.EmptyDirectories.Count.ToString("N0")
            });
        }
    }

    private void OnEmptyFolderSelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateEmptyFolderSelectionState();
    }

    private void UpdateEmptyFolderSelectionState()
    {
        DeleteEmptyButton.IsEnabled = !_operationRunning
            && _emptyFolderResult is not null
            && _emptyFolderCandidates.Any(item => item.CheckBox.IsChecked == true);
    }

    private async void OnDeleteEmptyClicked(object sender, RoutedEventArgs e)
    {
        List<string> selected = _emptyFolderCandidates
            .Where(item => item.CheckBox.IsChecked == true)
            .Select(item => item.Path)
            .ToList();
        if (selected.Count == 0 || _operationRunning || _emptyFolderScanRoot is null)
        {
            return;
        }

        var previewItems = selected
            .Select(path => new PreviewItem(
                path,
                risk: "green",
                importanceKey: "preview_importance_empty_folder",
                isDirectory: true))
            .ToList();
        var preview = new PreviewDialog(previewItems, 0, selected.Count, I18n.Tr("maintenance_confirm_empty"))
        {
            Owner = this
        };
        if (preview.ShowDialog() != true)
        {
            return;
        }

        _operationRunning = true;
        SetScanningControls(true);
        EmptyFolderStatus.Text = I18n.Tr("cleaning");
        try
        {
            CleanResult result = await Task.Run(() =>
                EmptyFolderCleaner.DeleteEmptyDirectories(_emptyFolderScanRoot, selected));
            EmptyFolderStatus.Text = I18n.Tr("maintenance_empty_done", new Dictionary<string, string>
            {
                ["count"] = result.RemovedDirectories.ToString("N0")
            });
            if (result.SkippedItems > 0)
            {
                EmptyFolderStatus.Text += " " + I18n.Tr("maintenance_changed_files", new Dictionary<string, string>
                {
                    ["count"] = result.SkippedItems.ToString("N0")
                });
            }
            _emptyFolderCandidates.Clear();
            EmptyFolderResultsPanel.Children.Clear();
            AddEmptyResult(EmptyFolderResultsPanel);
            DeleteEmptyButton.IsEnabled = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            ShowMessage(I18n.Tr("maintenance_title"), ex.Message, AppMessageKind.Error);
        }
        finally
        {
            _operationRunning = false;
            SetScanningControls(false);
            UpdateEmptyFolderSelectionState();
        }
    }

    private static CheckBox CreateResultCheckBox(string path, string detail)
    {
        var label = new StackPanel { Margin = new Thickness(0, 2, 4, 2) };
        label.Children.Add(new TextBlock
        {
            Text = path,
            Foreground = (Brush)Application.Current.FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });
        label.Children.Add(new TextBlock
        {
            Text = detail,
            Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0)
        });
        return new CheckBox
        {
            Content = label,
            Style = (Style)Application.Current.FindResource("CustomCheckBoxStyle"),
            Margin = new Thickness(6, 4, 6, 4),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }

    private static bool IsRecentlyModified(DateTime lastWriteTimeUtc)
    {
        TimeSpan age = DateTime.UtcNow - lastWriteTimeUtc;
        return age.TotalSeconds >= 0 && age.TotalSeconds < 60;
    }

    private void AddEmptyResult(Panel panel)
    {
        panel.Children.Add(new TextBlock
        {
            Text = I18n.Tr("maintenance_no_results"),
            Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 28, 8, 28)
        });
    }

    private void SetScanningControls(bool busy)
    {
        DuplicateRootBox.IsEnabled = !busy;
        EmptyFolderRootBox.IsEnabled = !busy;
        BrowseDuplicatesButton.IsEnabled = !busy;
        BrowseEmptyButton.IsEnabled = !busy;
        ScanDuplicatesButton.IsEnabled = !busy;
        ScanEmptyButton.IsEnabled = !busy;
        SelectDuplicateCopiesButton.IsEnabled = !busy && _duplicateCandidates.Count > 0;
        DeleteDuplicateButton.IsEnabled = !busy && _duplicateCandidates.Any(item => item.CheckBox.IsChecked == true);
        DeleteEmptyButton.IsEnabled = !busy
            && _emptyFolderResult is not null
            && _emptyFolderCandidates.Any(item => item.CheckBox.IsChecked == true);
    }

    private void ShowMessage(string title, string message, AppMessageKind kind)
    {
        new AppMessageDialog(title, message, kind) { Owner = this }.ShowDialog();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
