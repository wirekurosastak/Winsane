using System.Text;
using System.Text.Json; // Requires System.Text.Json (standard in .NET Core/5+)
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public class StartupService
{
    private readonly CoreService _coreService;

    public StartupService(CoreService coreService)
    {
        _coreService = coreService;
    }

    public async Task<List<Item>> GetStartupEntriesAsync()
    {
        var items = new List<Item>();

        // Refactored PS Script:
        // 1. Output is now JSON for safe parsing.
        // 2. Logic remains similar but structured for object export.
        string script = @"
$results = @()

# 1. Registry Run keys
$regSources = @(
    @{ Run='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; Approved='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Scope='User'; Location='Registry (User)' },
    @{ Run='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; Approved='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Scope='Machine'; Location='Registry (Machine)' }
)

foreach ($src in $regSources) {
    $props = $null
    try { $props = (Get-ItemProperty -Path $src.Run -ErrorAction SilentlyContinue).PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } } catch {}
    
    if ($props) {
        foreach ($prop in $props) {
            $isEnabled = $true
            try {
                $approved = (Get-ItemProperty -Path $src.Approved -Name $prop.Name -ErrorAction SilentlyContinue).($prop.Name)
                # If binary data exists and first byte is not 0x02 (usually 0x03 for disabled), it's disabled.
                # Note: Missing value means ENABLED by default.
                if ($approved -and $approved[0] -ne 2) { $isEnabled = $false }
            } catch {}

            $results += @{
                Name = $prop.Name
                Command = $prop.Value
                Location = $src.Location
                ApprovedPath = $src.Approved
                IsEnabled = $isEnabled
            }
        }
    }
}

# 2. Shell:startup folder
$startupFolders = @(
    @{ Path=[Environment]::GetFolderPath('Startup'); Approved='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Location='Startup Folder (User)' },
    @{ Path=[Environment]::GetFolderPath('CommonStartup'); Approved='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Location='Startup Folder (Machine)' }
)

foreach ($sf in $startupFolders) {
    if (Test-Path $sf.Path) {
        $files = Get-ChildItem -Path $sf.Path -File -ErrorAction SilentlyContinue
        foreach ($f in $files) {
            $isEnabled = $true
            try {
                $approved = (Get-ItemProperty -Path $sf.Approved -Name $f.Name -ErrorAction SilentlyContinue).($f.Name)
                if ($approved -and $approved[0] -ne 2) { $isEnabled = $false }
            } catch {}

            $results += @{
                Name = $f.BaseName
                Command = $f.FullName
                Location = $sf.Location
                ApprovedPath = $sf.Approved
                IsEnabled = $isEnabled
            }
        }
    }
}

# Output as compressed JSON
$results | ConvertTo-Json -Compress
";

        var (success, output, _) = await _coreService.ExecutePowerShellAsync(script);

        if (!success || string.IsNullOrWhiteSpace(output))
            return items;

        try 
        {
            var entries = JsonSerializer.Deserialize<List<StartupEntryDto>>(output);
            
            if (entries == null) return items;

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // Escape for single-quoted PowerShell strings
                string escapedName = entry.Name.Replace("'", "''");
                string escapedPath = entry.ApprovedPath.Replace("'", "''");

                // --- LOGIC FIXES BELOW ---

                // Check: Returns True if Enabled (Value missing OR byte[0] == 2)
                string checkCmd = $@"
$v = (Get-ItemProperty -Path '{escapedPath}' -Name '{escapedName}' -ErrorAction SilentlyContinue).'{escapedName}';
if ($null -eq $v) {{ $true }} else {{ $v[0] -eq 2 }}";

                // Enable: 
                // If value exists, set byte[0] = 0x02. 
                // If value is missing, do nothing (it's already enabled by default).
                string enableCmd = $@"
$p = '{escapedPath}'; $n = '{escapedName}';
$v = (Get-ItemProperty -Path $p -Name $n -ErrorAction SilentlyContinue).$n;
if ($v) {{ 
    $v[0] = 2; 
    Set-ItemProperty -Path $p -Name $n -Value $v -Force 
}}";

                // Disable: 
                // If value exists, set byte[0] = 0x03.
                // If value matches MISSING, we must create a new binary blob (12 bytes, first byte 0x03).
                string disableCmd = $@"
$p = '{escapedPath}'; $n = '{escapedName}';
$v = (Get-ItemProperty -Path $p -Name $n -ErrorAction SilentlyContinue).$n;
if ($v) {{ 
    $v[0] = 3; 
    Set-ItemProperty -Path $p -Name $n -Value $v -Force 
}} else {{ 
    $blob = [byte[]]::new(12); 
    $blob[0] = 3; 
    New-ItemProperty -Path $p -Name $n -PropertyType Binary -Value $blob -Force 
}}";

                items.Add(new Item
                {
                    Name = entry.Name,
                    Purpose = $"{entry.Location} — {entry.Command}",
                    CheckCommand = checkCmd.Replace(Environment.NewLine, " ").Trim(), // Flatten for safety
                    TrueCommand = enableCmd.Replace(Environment.NewLine, " ").Trim(),
                    FalseCommand = disableCmd.Replace(Environment.NewLine, " ").Trim(),
                });
            }
        }
        catch (JsonException)
        {
            // Handle parsing error or empty array scenarios
        }

        return items;
    }

    // Helper class for JSON deserialization
    private class StartupEntryDto
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
        public string ApprovedPath { get; set; } = "";
        public bool IsEnabled { get; set; }
    }
}