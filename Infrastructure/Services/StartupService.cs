using System.Text;
using System.Text.Json;
using Winsane.Core.Models;

namespace Winsane.Infrastructure.Services;

public class StartupService
{
    private readonly CoreService _coreService;

    public StartupService(CoreService coreService) => _coreService = coreService;

    public async Task<List<Item>> GetStartupEntriesAsync()
    {
        var items = new List<Item>();

        string script = @"
$results = @()
$regSources = @(
    @{ Run='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; Approved='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Location='Registry (User)' },
    @{ Run='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'; Approved='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'; Location='Registry (Machine)' }
)
foreach ($src in $regSources) {
    $props = $null
    try { $props = (Get-ItemProperty -Path $src.Run -ErrorAction SilentlyContinue).PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } } catch {}
    if ($props) {
        foreach ($prop in $props) {
            $isEnabled = $true
            try {
                $approved = (Get-ItemProperty -Path $src.Approved -Name $prop.Name -ErrorAction SilentlyContinue).($prop.Name)
                if ($approved -and $approved[0] -ne 2) { $isEnabled = $false }
            } catch {}
            $results += @{ Name = $prop.Name; Command = $prop.Value; Location = $src.Location; ApprovedPath = $src.Approved; IsEnabled = $isEnabled }
        }
    }
}
$startupFolders = @(
    @{ Path=[Environment]::GetFolderPath('Startup'); Approved='HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Location='Startup Folder (User)' },
    @{ Path=[Environment]::GetFolderPath('CommonStartup'); Approved='HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder'; Location='Startup Folder (Machine)' }
)
foreach ($sf in $startupFolders) {
    if (Test-Path $sf.Path) {
        foreach ($f in (Get-ChildItem -Path $sf.Path -File -ErrorAction SilentlyContinue)) {
            $isEnabled = $true
            try {
                $approved = (Get-ItemProperty -Path $sf.Approved -Name $f.Name -ErrorAction SilentlyContinue).($f.Name)
                if ($approved -and $approved[0] -ne 2) { $isEnabled = $false }
            } catch {}
            $results += @{ Name = $f.BaseName; Command = $f.FullName; Location = $sf.Location; ApprovedPath = $sf.Approved; IsEnabled = $isEnabled }
        }
    }
}
$results | ConvertTo-Json -Compress
";

        var (success, output, _) = await _coreService.ExecutePowerShellAsync(script);
        if (!success || string.IsNullOrWhiteSpace(output)) return items;

        try
        {
            var entries = JsonSerializer.Deserialize<List<StartupEntryDto>>(output);
            if (entries == null) return items;

            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.Name)) continue;
                string n = e.Name.Replace("'", "''"), p = e.ApprovedPath.Replace("'", "''");

                items.Add(new Item
                {
                    Name = e.Name,
                    Purpose = $"{e.Location} — {e.Command}",
                    CheckCommand = $"$v = (Get-ItemProperty -Path '{p}' -Name '{n}' -ErrorAction SilentlyContinue).'{n}'; if ($null -eq $v) {{ $true }} else {{ $v[0] -eq 2 }}",
                    TrueCommand = $"$p = '{p}'; $n = '{n}'; $v = (Get-ItemProperty -Path $p -Name $n -ErrorAction SilentlyContinue).$n; if ($v) {{ $v[0] = 2; Set-ItemProperty -Path $p -Name $n -Value $v -Force }}",
                    FalseCommand = $"$p = '{p}'; $n = '{n}'; $v = (Get-ItemProperty -Path $p -Name $n -ErrorAction SilentlyContinue).$n; if ($v) {{ $v[0] = 3; Set-ItemProperty -Path $p -Name $n -Value $v -Force }} else {{ $blob = [byte[]]::new(12); $blob[0] = 3; New-ItemProperty -Path $p -Name $n -PropertyType Binary -Value $blob -Force }}",
                });
            }
        }
        catch (JsonException) { }

        return items;
    }

    private class StartupEntryDto
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public string Location { get; set; } = "";
        public string ApprovedPath { get; set; } = "";
        public bool IsEnabled { get; set; }
    }
}