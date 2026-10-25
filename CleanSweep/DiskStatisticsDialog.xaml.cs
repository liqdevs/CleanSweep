using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Models;
using CleanSweep.Core.Cleaning;
using CleanSweep.Core.Scanning;
using CleanSweep.Core.Utils;

namespace CleanSweep;

public partial class DiskStatisticsDialog : Window
{
    private readonly string _drive;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _scanRunning = true;
    private bool _directoryLoading;
    private bool _deleting;
    private bool _closeWhenScanEnds;
    private bool _allowClose;
    private List<DiskFileSelection> _fileSelections = new();
    private List<DiskFileSelection> _visibleFileSelections = new();
    private List<DiskFileSelection> _topFileSelections = new();
    private readonly HashSet<string> _selectedFolderPaths = new(StringComparer.OrdinalIgnoreCase);
    private string _volumeRoot = string.Empty;
    private string _currentDirectory = string.Empty;
    private Dictionary<string, DiskDirectoryUsage> _directoryUsage =
        new(StringComparer.OrdinalIgnoreCase);
    private string _fileSortColumn = "size";
    private bool _sortDescending = true;

    public DiskStatisticsDialog(string drive)
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);
        _drive = drive;
        Retranslate();
        DriveText.Text = drive;
    }

    private void Retranslate()
    {
        Title = I18n.Tr("disk_stats_title");
        TitleText.Text = I18n.Tr("disk_stats_title");
        CapacityLabel.Text = I18n.Tr("disk_stats_capacity");
        UsedLabel.Text = I18n.Tr("disk_stats_used");
        FreeLabel.Text = I18n.Tr("disk_stats_free");
        FoldersTitle.Text = I18n.Tr("disk_stats_folders");
        FoldersList.ToolTip = I18n.Tr("disk_stats_folder_list_hint");
        LargestFilesTitle.Text = I18n.Tr("disk_stats_largest_files");
        FileSearchBox.ToolTip = I18n.Tr("disk_stats_search_top_hint");
        FolderNameColumn.Header = I18n.Tr("disk_stats_name");
        FolderSizeColumn.Header = I18n.Tr("disk_stats_size");
        FolderCountColumn.Header = I18n.Tr("disk_stats_files");
        FilePathColumn.Header = I18n.Tr("disk_stats_file");
        FileSizeColumn.Header = I18n.Tr("disk_stats_size");
        UpButton.Content = I18n.Tr("disk_stats_up");
        FileSearchBox.ToolTip = I18n.Tr("disk_stats_search_top_hint");
        DeleteSelectedButton.Content = I18n.Tr("disk_stats_delete_selected");
        DeleteSelectedButton.ToolTip = I18n.Tr("disk_stats_delete_hint");
        CloseButton.Content = I18n.Tr(_scanRunning ? "disk_stats_cancel" : "settings_close");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScanProgress.Visibility = Visibility.Visible;
        StatusText.Text = I18n.Tr("scanning");
        var progress = CreateThrottledProgress<DiskScanProgress>(update =>
        {
            StatusText.Text = I18n.Tr("disk_stats_scanning", new Dictionary<string, string>
            {
                ["count"] = update.ItemsScanned.ToString("N0"),
                ["path"] = update.CurrentPath
            });
            FileSearchBox.ToolTip = I18n.Tr("disk_stats_search_hint");
        });

        try
        {
            var result = await Task.Run(
                () => DiskStatisticsScanner.Scan(_drive, progress, _cancellation.Token),
                _cancellation.Token);
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = I18n.Tr("disk_stats_cancelled");
        }
        catch (Exception ex)
        {
            StatusText.Text = I18n.Tr("disk_stats_error", new Dictionary<string, string> { ["error"] = ex.Message });
        }
        finally
        {
            _scanRunning = false;
            ScanProgress.Visibility = Visibility.Collapsed;
            CloseButton.Content = I18n.Tr("settings_close");
            if (_closeWhenScanEnds)
            {
                _allowClose = true;
                Close();
            }
        }
    }

    private void ShowResult(DiskStatisticsResult result)
    {
        _directoryUsage = result.DirectoryUsage as Dictionary<string, DiskDirectoryUsage>
            ?? new Dictionary<string, DiskDirectoryUsage>(result.DirectoryUsage, StringComparer.OrdinalIgnoreCase);
        CapacityValue.Text = FileSystemUtils.HumanSize(result.TotalCapacityBytes);
        UsedValue.Text = FileSystemUtils.HumanSize(result.DriveUsedBytes);
        FreeValue.Text = FileSystemUtils.HumanSize(result.FreeBytes);

        double usedFraction = result.TotalCapacityBytes == 0
            ? 0
            : Math.Clamp(result.DriveUsedBytes / (double)result.TotalCapacityBytes, 0, 1);
        UsedBar.Width = CapacityBar.ActualWidth * usedFraction;
        CapacityBar.SizeChanged += (_, _) => UsedBar.Width = CapacityBar.ActualWidth * usedFraction;

        ScanSummaryText.Text = I18n.Tr("disk_stats_summary", new Dictionary<string, string>
        {
            ["size"] = FileSystemUtils.HumanSize(result.ScannedBytes),
            ["count"] = result.FileCount.ToString("N0")
        });
        if (result.UnavailablePathCount > 0)
        {
            ScanSummaryText.Text += " " + I18n.Tr("disk_stats_partial", new Dictionary<string, string>
            {
                ["count"] = result.UnavailablePathCount.ToString("N0")
            });
        }

        _volumeRoot = Path.GetPathRoot(result.RootPath) ?? result.RootPath;
        _currentDirectory = _volumeRoot;
        var rootFilesLabel = I18n.Tr("disk_stats_root_files");
        var rootFolders = result.Folders.Where(folder => folder.Name.Length > 0).Select(folder => new FolderBrowseItem
        {
            Name = folder.Name,
            FullPath = Path.Combine(_volumeRoot, folder.Name),
            SizeText = FileSystemUtils.HumanSize(
                _directoryUsage.GetValueOrDefault(Path.Combine(_volumeRoot, folder.Name))?.SizeBytes ?? folder.SizeBytes),
            FileCountText = (_directoryUsage.GetValueOrDefault(Path.Combine(_volumeRoot, folder.Name))?.FileCount ?? folder.FileCount).ToString("N0"),
            IsSelected = _selectedFolderPaths.Contains(Path.Combine(_volumeRoot, folder.Name)),
            CanDelete = true,
            DeleteToolTip = I18n.Tr("disk_stats_select_folder_hint")
        }).ToList();
        rootFolders.Insert(0, new FolderBrowseItem
        {
            Name = I18n.Tr("disk_stats_largest_files"),
            IsLargestFiles = true,
            CanDelete = false
        });
        rootFolders.Insert(0, new FolderBrowseItem
        {
            Name = rootFilesLabel,
            FullPath = _volumeRoot,
            IsRootFiles = true,
            CanDelete = false
        });
        SetFolderItems(rootFolders);
        _topFileSelections = result.LargestFiles.Select(file => new DiskFileSelection
        {
            Path = file.Path,
            DisplayPath = file.Path,
            Name = Path.GetFileName(file.Path),
            SizeBytes = file.SizeBytes,
            SizeText = FileSystemUtils.HumanSize(file.SizeBytes)
        }).ToList();
        _fileSelections = _topFileSelections.ToList();
        _visibleFileSelections = _topFileSelections.ToList();
        foreach (var file in result.RootFiles)
        {
            AddOrUpdateFile(file, displayFullPath: false);
        }
        RenderFileItems();
        CurrentPathText.Text = _volumeRoot;
        UpButton.IsEnabled = false;
        LargestFilesTitle.Text = I18n.Tr("disk_stats_largest_files");

        StatusText.Text = I18n.Tr("disk_stats_complete");
        if (result.RootFileCount > result.RootFiles.Count)
        {
            StatusText.Text += " " + I18n.Tr("disk_stats_browse_limited", new Dictionary<string, string>
            {
                ["shown"] = result.RootFiles.Count.ToString("N0"),
                ["count"] = result.RootFileCount.ToString("N0")
            });
        }
        ScanProgress.Visibility = Visibility.Collapsed;
        DeleteSelectedButton.IsEnabled = false;
    }

    private void OnFileCheckChanged(object sender, RoutedEventArgs e)
    {
        UpdateDeleteButton();
    }

    private void UpdateDeleteButton()
    {
        DeleteSelectedButton.IsEnabled = !_scanRunning && !_directoryLoading && !_deleting &&
            (_fileSelections.Any(file => file.IsSelected) || _selectedFolderPaths.Count > 0);
    }

    private void SetFolderItems(IEnumerable<FolderBrowseItem> folders)
    {
        FoldersList.ItemsSource = folders.ToList();
        FoldersList.SelectedItem = null;
    }

    private void OnFolderCheckChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: FolderBrowseItem folder } checkBox || !folder.CanDelete)
        {
            return;
        }

        folder.IsSelected = checkBox.IsChecked == true;
        if (folder.IsSelected)
        {
            _selectedFolderPaths.Add(folder.FullPath);
        }
        else
        {
            _selectedFolderPaths.Remove(folder.FullPath);
        }
        UpdateDeleteButton();
    }

    private void OnFolderCheckboxDoubleClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private async void OnFolderDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_scanRunning || _deleting || _directoryLoading ||
            FoldersList.SelectedItem is not FolderBrowseItem folder)
        {
            return;
        }

        if (folder.IsLargestFiles)
        {
            _currentDirectory = _volumeRoot;
            CurrentPathText.Text = _volumeRoot;
            UpButton.IsEnabled = false;
            LargestFilesTitle.Text = I18n.Tr("disk_stats_largest_files");
            _visibleFileSelections = _topFileSelections;
            RenderFileItems();
            StatusText.Text = I18n.Tr("disk_stats_top_files_hint");
            return;
        }

        await LoadDirectoryAsync(folder.IsRootFiles ? _volumeRoot : folder.FullPath);
    }

    private async Task LoadDirectoryAsync(string directoryPath)
    {
        _directoryLoading = true;
        FoldersList.IsEnabled = false;
        FileSearchBox.IsEnabled = false;
        UpButton.IsEnabled = false;
        LargestFilesList.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        StatusText.Text = I18n.Tr("disk_stats_browsing", new Dictionary<string, string> { ["path"] = directoryPath });

        var progress = CreateThrottledProgress<DiskScanProgress>(update =>
        {
            StatusText.Text = I18n.Tr("disk_stats_browsing_progress", new Dictionary<string, string>
            {
                ["count"] = update.ItemsScanned.ToString("N0"),
                ["path"] = update.CurrentPath
            });
        });

        try
        {
            var listing = await Task.Run(() => DiskStatisticsScanner.ListDirectoryContents(
                directoryPath,
                _volumeRoot,
                progress,
                _cancellation.Token,
                _directoryUsage));

            _currentDirectory = Path.GetFullPath(directoryPath);
            CurrentPathText.Text = _currentDirectory;
            FileSearchBox.Clear();
            UpButton.IsEnabled = !string.Equals(_currentDirectory, _volumeRoot, StringComparison.OrdinalIgnoreCase);
            LargestFilesTitle.Text = I18n.Tr("disk_stats_files_in_folder", new Dictionary<string, string>
            {
                ["folder"] = Path.GetFileName(_currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    is { Length: > 0 } name ? name : _currentDirectory
            });

            var folders = listing.Folders.Select(folder => new FolderBrowseItem
            {
                Name = folder.Name,
                FullPath = Path.Combine(_currentDirectory, folder.Name),
                SizeText = FileSystemUtils.HumanSize(folder.SizeBytes),
                FileCountText = folder.FileCount.ToString("N0"),
                IsSelected = _selectedFolderPaths.Contains(Path.Combine(_currentDirectory, folder.Name)),
                CanDelete = true,
                DeleteToolTip = I18n.Tr("disk_stats_select_folder_hint")
            }).ToList();
            if (string.Equals(_currentDirectory, _volumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                folders.Insert(0, new FolderBrowseItem
                {
                    Name = I18n.Tr("disk_stats_root_files"),
                    FullPath = _currentDirectory,
                    IsRootFiles = true,
                    CanDelete = false
                });
            }
            SetFolderItems(folders);

            var currentFiles = new List<DiskFileSelection>();
            foreach (var file in listing.Files)
            {
                currentFiles.Add(AddOrUpdateFile(file, displayFullPath: false));
            }

            _visibleFileSelections = currentFiles;
            RenderFileItems();
            string status = I18n.Tr("disk_stats_browse_complete", new Dictionary<string, string>
            {
                ["count"] = listing.TotalFileCount.ToString("N0"),
                ["folderCount"] = listing.Folders.Count.ToString("N0")
            });
            if (listing.TotalFileCount > listing.Files.Count)
            {
                status += " " + I18n.Tr("disk_stats_browse_limited", new Dictionary<string, string>
                {
                    ["shown"] = listing.Files.Count.ToString("N0"),
                    ["count"] = listing.TotalFileCount.ToString("N0")
                });
            }
            if (listing.UnavailablePathCount > 0)
            {
                status += " " + I18n.Tr("disk_stats_partial", new Dictionary<string, string>
                {
                    ["count"] = listing.UnavailablePathCount.ToString("N0")
                });
            }
            StatusText.Text = status;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = I18n.Tr("disk_stats_cancelled");
        }
        catch (Exception ex)
        {
            StatusText.Text = I18n.Tr("disk_stats_error", new Dictionary<string, string> { ["error"] = ex.Message });
        }
        finally
        {
            _directoryLoading = false;
            FoldersList.IsEnabled = true;
            FileSearchBox.IsEnabled = true;
            LargestFilesList.IsEnabled = true;
            ScanProgress.Visibility = Visibility.Collapsed;
            UpButton.IsEnabled = !_scanRunning &&
                !string.Equals(_currentDirectory, _volumeRoot, StringComparison.OrdinalIgnoreCase);
            UpdateDeleteButton();
            if (_closeWhenScanEnds)
            {
                _allowClose = true;
                Close();
            }
        }
    }

    private async void OnUpClicked(object sender, RoutedEventArgs e)
    {
        if (string.Equals(_currentDirectory, _volumeRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? parent = Directory.GetParent(_currentDirectory)?.FullName;
        if (parent != null && DiskStatisticsScanner.IsWithinSelectedVolume(parent, _volumeRoot))
        {
            await LoadDirectoryAsync(parent);
        }
    }

    private void OnFileSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_fileSelections != null)
        {
            RenderFileItems();
        }
    }

    private DiskFileSelection AddOrUpdateFile(DiskLargestFile file, bool displayFullPath)
    {
        var selection = _fileSelections.FirstOrDefault(
            existing => string.Equals(existing.Path, file.Path, StringComparison.OrdinalIgnoreCase));
        if (selection == null)
        {
            selection = new DiskFileSelection
            {
                Path = file.Path,
                Name = Path.GetFileName(file.Path)
            };
            _fileSelections.Add(selection);
        }

        selection.SizeBytes = file.SizeBytes;
        selection.SizeText = FileSystemUtils.HumanSize(file.SizeBytes);
        selection.DisplayPath = displayFullPath ? file.Path : selection.Name;
        return selection;
    }

    private void OnFilesHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Role == GridViewColumnHeaderRole.Padding)
        {
            return;
        }

        string column = header.Content?.ToString() == I18n.Tr("disk_stats_size") ? "size" : "name";
        if (_fileSortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _fileSortColumn = column;
            _sortDescending = column == "size";
        }

        RenderFileItems();
    }

    private void RenderFileItems()
    {
        string query = FileSearchBox?.Text?.Trim() ?? string.Empty;
        IEnumerable<DiskFileSelection> visible = _visibleFileSelections;
        if (!string.IsNullOrEmpty(query))
        {
            visible = visible.Where(file => file.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        visible = _fileSortColumn == "name"
            ? (_sortDescending ? visible.OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase) : visible.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
            : (_sortDescending ? visible.OrderByDescending(file => file.SizeBytes) : visible.OrderBy(file => file.SizeBytes));
        LargestFilesList.ItemsSource = visible.ToList();
    }

    private sealed class FolderBrowseItem
    {
        public string Name { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public string SizeText { get; init; } = string.Empty;
        public string FileCountText { get; init; } = string.Empty;
        public bool IsRootFiles { get; init; }
        public bool IsLargestFiles { get; init; }
        public bool CanDelete { get; init; }
        public string DeleteToolTip { get; init; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    private async void OnDeleteSelectedClicked(object sender, RoutedEventArgs e)
    {
        var selectedFiles = _fileSelections.Where(file => file.IsSelected).ToList();
        var selectedFolders = GetTopLevelSelectedFolders(_selectedFolderPaths, _volumeRoot);
        var selectedFolderSet = new HashSet<string>(selectedFolders, StringComparer.OrdinalIgnoreCase);
        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
        {
            return;
        }

        _directoryLoading = true;
        FoldersList.IsEnabled = false;
        LargestFilesList.IsEnabled = false;
        FileSearchBox.IsEnabled = false;
        UpButton.IsEnabled = false;
        DeleteSelectedButton.IsEnabled = false;
        ScanProgress.Visibility = Visibility.Visible;
        StatusText.Text = I18n.Tr("disk_stats_preparing_folder_delete");

        try
        {
            IProgress<DiskScanProgress> preparationProgress = CreateThrottledProgress<DiskScanProgress>(
                update =>
                {
                    if (_directoryLoading)
                    {
                        StatusText.Text = I18n.Tr("disk_stats_preparing_delete_progress", new Dictionary<string, string>
                        {
                            ["count"] = update.ItemsScanned.ToString("N0"),
                            ["path"] = update.CurrentPath
                        });
                    }
                });

            var previewData = await Task.Run(() =>
            {
                var previewItems = new List<PreviewItem>();
                var largestFiles = new PriorityQueue<DiskLargestFile, long>();
                long totalSize = 0;
                int totalFileCount = 0;
                int remainingFileSlots = Math.Max(0, PreviewDialog.MaximumVisibleItems - selectedFolders.Count);

                foreach (string selectedFolder in selectedFolders)
                {
                    _cancellation.Token.ThrowIfCancellationRequested();
                    long folderSize;
                    int folderFileCount;
                    if (remainingFileSlots > 0)
                    {
                        var folderContents = DiskStatisticsScanner.GetFolderPreviewContents(
                            selectedFolder,
                            _volumeRoot,
                            remainingFileSlots,
                            _cancellation.Token,
                            preparationProgress);
                        foreach (var file in folderContents.LargestFiles)
                        {
                            AddLargestFile(largestFiles, file, remainingFileSlots);
                        }
                        folderSize = folderContents.TotalSizeBytes;
                        folderFileCount = folderContents.FileCount;
                    }
                    else
                    {
                        var usage = _directoryUsage.GetValueOrDefault(selectedFolder);
                        folderSize = usage?.SizeBytes ?? 0;
                        folderFileCount = usage?.FileCount ?? 0;
                    }

                    if (previewItems.Count < PreviewDialog.MaximumVisibleItems)
                    {
                        var assessment = FileImportanceAnalyzer.Analyze(selectedFolder);
                        previewItems.Add(new PreviewItem(
                            selectedFolder,
                            folderSize,
                            folderFileCount > 0 ? assessment.Risk : "green",
                            false,
                            folderFileCount > 0 ? assessment.DescriptionKey : "preview_importance_empty_folder",
                            isDirectory: true));
                    }
                    totalSize += folderSize;
                    totalFileCount += folderFileCount;
                }

                foreach (var selectedFile in selectedFiles)
                {
                    _cancellation.Token.ThrowIfCancellationRequested();
                    if (IsWithinAnySelectedFolder(selectedFile.Path, selectedFolderSet, _volumeRoot))
                    {
                        continue;
                    }
                    try
                    {
                        var file = new FileInfo(selectedFile.Path);
                        if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            var candidate = new DiskLargestFile { Path = file.FullName, SizeBytes = file.Length };
                            AddLargestFile(largestFiles, candidate, remainingFileSlots);
                            totalSize += candidate.SizeBytes;
                            totalFileCount++;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
                    {
                    }
                }

                foreach (var file in largestFiles.UnorderedItems
                             .Select(item => item.Element)
                             .OrderByDescending(file => file.SizeBytes))
                {
                    var assessment = FileImportanceAnalyzer.Analyze(file.Path);
                    previewItems.Add(new PreviewItem(
                        file.Path,
                        file.SizeBytes,
                        assessment.Risk,
                        CategoryCleaner.ShouldSkipForCleanup(file.Path),
                        assessment.DescriptionKey,
                        allowRecentDeletion: true));
                }

                return (
                    Items: previewItems,
                    TotalSize: totalSize,
                    TotalItemCount: totalFileCount + selectedFolders.Count);
            }, _cancellation.Token);

            if (previewData.Items.Count == 0)
            {
                StatusText.Text = I18n.Tr("disk_stats_no_selected_files");
                return;
            }
            var previewDialog = new PreviewDialog(
                previewData.Items,
                previewData.TotalSize,
                previewData.TotalItemCount)
            {
                Owner = this
            };
            if (previewDialog.ShowDialog() != true)
            {
                return;
            }

            _directoryLoading = false;
            _deleting = true;
            CloseButton.IsEnabled = false;
            StatusText.Text = I18n.Tr("disk_stats_deleting");
            bool deletionFinished = false;
            IProgress<CleanProgress> deletionProgress = CreateThrottledProgress<CleanProgress>(
                update =>
                {
                    if (_deleting && !deletionFinished)
                    {
                        StatusText.Text = I18n.Tr("disk_stats_deleting_progress", new Dictionary<string, string>
                        {
                            ["count"] = update.ItemsProcessed.ToString("N0"),
                            ["path"] = update.CurrentPath
                        });
                    }
                });
            var deleteOutcome = await Task.Run(() =>
            {
                var cleanup = new CleanResult();
                var folderResults = new List<(string Path, CleanResult Result)>();
                foreach (string folder in selectedFolders)
                {
                    _cancellation.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (Directory.Exists(folder))
                        {
                            var folderResult = CategoryCleaner.CleanDirectoryWithinRoot(
                                folder,
                                _volumeRoot,
                                _cancellation.Token,
                                deletionProgress);
                            folderResults.Add((folder, folderResult));
                            MergeCleanResult(cleanup, folderResult);
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException or ArgumentException or NotSupportedException)
                    {
                        AddCleanupError(cleanup, new CleanError("folder_cleanup_failed", Path.GetFileName(folder)));
                    }
                }

                var standaloneFiles = selectedFiles
                    .Where(file => !IsWithinAnySelectedFolder(file.Path, selectedFolderSet, _volumeRoot))
                    .ToList();
                var fileResult = CategoryCleaner.CleanFilesWithinRoot(
                    standaloneFiles.Select(file => file.Path),
                    _volumeRoot,
                    _cancellation.Token,
                    allowRecentFiles: true);
                MergeCleanResult(cleanup, fileResult);
                var deletedFiles = standaloneFiles
                    .Where(file => !File.Exists(file.Path))
                    .ToList();
                return (Cleanup: cleanup, FolderResults: folderResults, DeletedFiles: deletedFiles);
            }, _cancellation.Token);
            deletionFinished = true;
            CleanResult result = deleteOutcome.Cleanup;

            foreach (var file in deleteOutcome.DeletedFiles)
            {
                SubtractFileFromDirectoryUsage(file.Path, file.SizeBytes);
            }
            foreach (var folderResult in deleteOutcome.FolderResults)
            {
                ApplyFolderUsageResult(folderResult.Path, folderResult.Result);
            }
            foreach (string path in _selectedFolderPaths
                         .Where(selectedPath => IsWithinAnySelectedFolder(selectedPath, selectedFolderSet, _volumeRoot) ||
                             selectedFolderSet.Contains(selectedPath))
                         .ToList())
            {
                _selectedFolderPaths.Remove(path);
            }

            _fileSelections.RemoveAll(selection => !File.Exists(selection.Path));
            _topFileSelections.RemoveAll(selection => !File.Exists(selection.Path));
            _visibleFileSelections.RemoveAll(selection => !File.Exists(selection.Path));
            RenderFileItems();
            UpdateDeleteButton();

            var (total, _, free) = FileSystemUtils.DiskUsage(_drive);
            CapacityValue.Text = FileSystemUtils.HumanSize(total);
            UsedValue.Text = FileSystemUtils.HumanSize(Math.Max(0, total - free));
            FreeValue.Text = FileSystemUtils.HumanSize(free);
            double usedFraction = total == 0 ? 0 : Math.Clamp((total - free) / (double)total, 0, 1);
            UsedBar.Width = CapacityBar.ActualWidth * usedFraction;

            string message = I18n.Tr("disk_stats_delete_done", new Dictionary<string, string>
            {
                ["size"] = FileSystemUtils.HumanSize(result.FreedBytes),
                ["count"] = result.RemovedItems.ToString("N0")
            });
            if (result.RemovedDirectories > 0)
            {
                message += "\n\n" + I18n.Tr("disk_stats_folders_deleted", new Dictionary<string, string>
                {
                    ["count"] = result.RemovedDirectories.ToString("N0")
                });
            }
            if (result.SkippedItems > 0)
            {
                message += "\n\n" + I18n.Tr("disk_stats_delete_skipped", new Dictionary<string, string>
                {
                    ["count"] = result.SkippedItems.ToString("N0")
                });
            }
            if (result.Errors.Count > 0 || result.OmittedErrorsCount > 0)
            {
                var shownErrors = result.Errors.Take(5).ToList();
                message += "\n\n" + string.Join("\n", shownErrors.Select(error =>
                    I18n.Tr(error.Key, new Dictionary<string, string> { ["detail"] = error.Detail })));
                int errorsNotShown = result.OmittedErrorsCount + result.Errors.Count - shownErrors.Count;
                if (errorsNotShown > 0)
                {
                    message += "\n" + I18n.Tr("cleanup_more_errors_omitted", new Dictionary<string, string>
                    {
                        ["count"] = errorsNotShown.ToString("N0")
                    });
                }
            }

            int errorCount = result.Errors.Count + result.OmittedErrorsCount;
            StatusText.Text = errorCount == 0
                ? I18n.Tr("disk_stats_complete")
                : I18n.Tr("disk_stats_delete_errors", new Dictionary<string, string> { ["count"] = errorCount.ToString("N0") });
            new AppMessageDialog(
                I18n.Tr("done_title"),
                message,
                errorCount == 0 ? AppMessageKind.Success : AppMessageKind.Warning)
            {
                Owner = this
            }.ShowDialog();
            while (!Directory.Exists(_currentDirectory))
            {
                string? parent = Directory.GetParent(_currentDirectory)?.FullName;
                if (parent is null || !DiskStatisticsScanner.IsWithinSelectedVolume(parent, _volumeRoot))
                {
                    break;
                }
                _currentDirectory = parent;
            }
            if (!_closeWhenScanEnds && Directory.Exists(_currentDirectory))
            {
                await LoadDirectoryAsync(_currentDirectory);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = I18n.Tr("disk_stats_error", new Dictionary<string, string> { ["error"] = ex.Message });
            new AppMessageDialog(
                I18n.Tr("done_title"),
                I18n.Tr("disk_stats_delete_failure", new Dictionary<string, string> { ["error"] = ex.Message }),
                AppMessageKind.Error)
            {
                Owner = this
            }.ShowDialog();
        }
        finally
        {
            _deleting = false;
            _directoryLoading = false;
            FoldersList.IsEnabled = true;
            LargestFilesList.IsEnabled = true;
            FileSearchBox.IsEnabled = true;
            UpButton.IsEnabled = !_scanRunning &&
                !string.Equals(_currentDirectory, _volumeRoot, StringComparison.OrdinalIgnoreCase);
            ScanProgress.Visibility = Visibility.Collapsed;
            CloseButton.IsEnabled = true;
            UpdateDeleteButton();
            if (_closeWhenScanEnds)
            {
                _allowClose = true;
                Close();
            }
        }
    }

    private void SubtractFileFromDirectoryUsage(string filePath, long sizeBytes)
    {
        string? directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory) && DiskStatisticsScanner.IsWithinSelectedVolume(directory, _volumeRoot))
        {
            if (_directoryUsage.TryGetValue(directory, out var usage))
            {
                _directoryUsage[directory] = new DiskDirectoryUsage
                {
                    SizeBytes = Math.Max(0, usage.SizeBytes - sizeBytes),
                    FileCount = Math.Max(0, usage.FileCount - 1)
                };
            }
            if (string.Equals(directory, _volumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static List<string> GetTopLevelSelectedFolders(IEnumerable<string> selectedFolders, string volumeRoot)
    {
        var selectedPaths = new HashSet<string>(selectedFolders, StringComparer.OrdinalIgnoreCase);
        return selectedPaths
            .Where(path => !IsWithinAnySelectedFolder(path, selectedPaths, volumeRoot))
            .ToList();
    }

    private static bool IsWithinAnySelectedFolder(
        string path,
        HashSet<string> selectedFolders,
        string volumeRoot)
    {
        string? parent = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(parent) &&
            DiskStatisticsScanner.IsWithinSelectedVolume(parent, volumeRoot))
        {
            if (selectedFolders.Contains(parent))
            {
                return true;
            }
            if (string.Equals(parent, volumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            parent = Path.GetDirectoryName(parent);
        }
        return false;
    }

    private static void AddLargestFile(
        PriorityQueue<DiskLargestFile, long> largestFiles,
        DiskLargestFile candidate,
        int limit)
    {
        if (limit <= 0)
        {
            return;
        }
        if (largestFiles.Count < limit)
        {
            largestFiles.Enqueue(candidate, candidate.SizeBytes);
        }
        else if (largestFiles.TryPeek(out _, out long smallestSize) &&
            candidate.SizeBytes > smallestSize)
        {
            largestFiles.Dequeue();
            largestFiles.Enqueue(candidate, candidate.SizeBytes);
        }
    }

    private void ApplyFolderUsageResult(string folderPath, CleanResult result)
    {
        foreach (string nestedPath in _directoryUsage.Keys
                     .Where(path => !string.Equals(path, folderPath, StringComparison.OrdinalIgnoreCase) &&
                         DiskStatisticsScanner.IsWithinSelectedVolume(path, folderPath))
                     .ToList())
        {
            _directoryUsage.Remove(nestedPath);
        }

        if (Directory.Exists(folderPath) && _directoryUsage.TryGetValue(folderPath, out var folderUsage))
        {
            _directoryUsage[folderPath] = new DiskDirectoryUsage
            {
                SizeBytes = Math.Max(0, folderUsage.SizeBytes - result.FreedBytes),
                FileCount = Math.Max(0, folderUsage.FileCount - result.RemovedItems)
            };
        }
        else
        {
            RemoveDirectoryFromUsage(folderPath);
        }

        string? parent = Path.GetDirectoryName(folderPath);
        while (!string.IsNullOrEmpty(parent) &&
            DiskStatisticsScanner.IsWithinSelectedVolume(parent, _volumeRoot))
        {
            if (_directoryUsage.TryGetValue(parent, out var usage))
            {
                _directoryUsage[parent] = new DiskDirectoryUsage
                {
                    SizeBytes = Math.Max(0, usage.SizeBytes - result.FreedBytes),
                    FileCount = Math.Max(0, usage.FileCount - result.RemovedItems)
                };
            }
            if (string.Equals(parent, _volumeRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            parent = Path.GetDirectoryName(parent);
        }
    }

    private static void MergeCleanResult(CleanResult destination, CleanResult source)
    {
        destination.FreedBytes += source.FreedBytes;
        destination.RemovedItems += source.RemovedItems;
        destination.RemovedDirectories += source.RemovedDirectories;
        destination.SkippedItems += source.SkippedItems;
        destination.OmittedErrorsCount += source.OmittedErrorsCount;
        foreach (var error in source.Errors)
        {
            if (destination.Errors.Count < 100)
            {
                destination.Errors.Add(error);
            }
            else
            {
                destination.OmittedErrorsCount++;
            }
        }
    }

    private static void AddCleanupError(CleanResult result, CleanError error)
    {
        if (result.Errors.Count < 100)
        {
            result.Errors.Add(error);
        }
        else
        {
            result.OmittedErrorsCount++;
        }
    }

    private static IProgress<T> CreateThrottledProgress<T>(Action<T> update)
    {
        IProgress<T> dispatcherProgress = new Progress<T>(update);
        return new ThrottledProgress<T>(dispatcherProgress, Stopwatch.Frequency / 4);
    }

    private sealed class ThrottledProgress<T>(
        IProgress<T> inner,
        long minimumIntervalTicks) : IProgress<T>
    {
        private long _lastReportTimestamp;

        public void Report(T value)
        {
            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Read(ref _lastReportTimestamp);
            if (now - previous < minimumIntervalTicks ||
                Interlocked.CompareExchange(ref _lastReportTimestamp, now, previous) != previous)
            {
                return;
            }

            inner.Report(value);
        }
    }

    private void RemoveDirectoryFromUsage(string directoryPath)
    {
        foreach (string indexedPath in _directoryUsage.Keys
                     .Where(path => DiskStatisticsScanner.IsWithinSelectedVolume(path, directoryPath))
                     .ToList())
        {
            _directoryUsage.Remove(indexedPath);
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        if (_scanRunning || _directoryLoading)
        {
            _closeWhenScanEnds = true;
            CloseButton.IsEnabled = false;
            CloseButton.Content = I18n.Tr("disk_stats_cancelling");
            _cancellation.Cancel();
            return;
        }

        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_deleting)
        {
            e.Cancel = true;
            return;
        }

        if ((_scanRunning || _directoryLoading) && !_allowClose)
        {
            e.Cancel = true;
            _closeWhenScanEnds = true;
            _cancellation.Cancel();
        }
        else
        {
            _cancellation.Dispose();
        }
    }

    private sealed class DiskFileSelection
    {
        public string Path { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeText { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
