using System;
using System.Windows;
using System.Windows.Controls;
using CleanSweep.Core.Localization;
using CleanSweep.Core.Utils;

namespace CleanSweep;

public partial class SettingsDialog : Window
{
    private string _currentLang;
    private string _currentDrive;
    private bool _initializing = true;

    public event Action<string>? LanguageChanged;
    public event Action<string>? DriveChanged;

    public SettingsDialog(string currentLang, string currentDrive)
    {
        InitializeComponent();
        WindowChromeTheme.Apply(this);
        _currentLang = currentLang;
        _currentDrive = currentDrive;

        // Populate Languages
        foreach (var code in I18n.Languages)
        {
            var item = new ComboBoxItem
            {
                Content = I18n.LanguageNames.GetValueOrDefault(code, code),
                Tag = code
            };
            LanguageComboBox.Items.Add(item);
            if (string.Equals(code, currentLang, StringComparison.OrdinalIgnoreCase))
            {
                LanguageComboBox.SelectedItem = item;
            }
        }

        // Populate Drives
        var drives = FileSystemUtils.ListAvailableDrives();
        if (!drives.Contains(currentDrive, StringComparer.OrdinalIgnoreCase))
        {
            drives.Insert(0, currentDrive);
        }

        foreach (var drive in drives)
        {
            var item = new ComboBoxItem
            {
                Content = drive,
                Tag = drive
            };
            DiskComboBox.Items.Add(item);
            if (string.Equals(drive, currentDrive, StringComparison.OrdinalIgnoreCase))
            {
                DiskComboBox.SelectedItem = item;
            }
        }

        _initializing = false;
        Retranslate();
    }

    private void Retranslate()
    {
        Title = I18n.Tr("settings_title");
        LanguageLabel.Text = I18n.Tr("settings_language");
        DiskLabel.Text = I18n.Tr("settings_disk");
        DiskHintText.Text = I18n.Tr("settings_disk_hint");
        CloseButton.Content = I18n.Tr("settings_close");
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string code)
        {
            if (code != _currentLang)
            {
                _currentLang = code;
                I18n.SetLanguage(code);
                Retranslate();
                LanguageChanged?.Invoke(code);
            }
        }
    }

    private void OnDiskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (DiskComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string drive)
        {
            if (drive != _currentDrive)
            {
                _currentDrive = drive;
                I18n.SaveDrive(drive);
                DriveChanged?.Invoke(drive);
            }
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
