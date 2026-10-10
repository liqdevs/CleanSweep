namespace CleanSweep.Core.Models;

public class StartupItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "hkcu_run" | "hklm_run" | "user_startup" | "common_startup"
    public bool Enabled { get; set; }
    public bool NeedsAdmin { get; set; }
}

