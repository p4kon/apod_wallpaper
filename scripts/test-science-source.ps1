# Run with Windows PowerShell 5.1 after a Release build. This is an opt-in live
# metadata regression probe, not part of startup or the offline smoke suite.
[CmdletBinding()]
param(
    [string[]] $Dates = @(
        '1995-06-16', '1995-06-20', '2000-01-01', '2003-06-01',
        '2012-03-12', '2014-10-01', '2015-01-01', '2026-08-05',
        '2026-08-31', '2026-09-01', '2026-09-27', '2026-09-28'
    ),
    [switch] $CheckRange
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Use powershell.exe -NoProfile -File scripts/test-science-source.ps1 (Windows PowerShell 5.1).'
}
if ($Dates.Count -lt 1 -or $Dates.Count -gt 50) { throw 'Supply between 1 and 50 dates.' }
$parsedDates = @($Dates | ForEach-Object {
    [datetime]::ParseExact($_, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
})
$assemblyPath = Join-Path $PSScriptRoot '../apod_wallpaper.Core/bin/Release/net48/apod_wallpaper.Core.dll'
if (-not (Test-Path -LiteralPath $assemblyPath)) { throw 'Build the solution in Release first.' }
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $assemblyPath).Path)
$type = $assembly.GetType('apod_wallpaper.ApodClient', $true)
$client = [Activator]::CreateInstance($type, $true)
$getEntry = $type.GetMethod('GetEntry')
$results = [Collections.Generic.List[object]]::new()
$failed = $false

function Test-Entry($entry, [datetime] $date) {
    if ($entry.Date -ne $date.ToString('yyyy-MM-dd')) { throw 'Returned publication date does not match the request.' }
    $post = [uri] $entry.PostUrl
    if ($post.Scheme -ne 'https' -or $post.Host -ne 'science.nasa.gov' -or
        -not $post.AbsolutePath.StartsWith('/image-article/')) { throw 'Invalid canonical publication URL.' }
    if ($entry.HasImage -and ([string]::IsNullOrWhiteSpace($entry.PreviewImageUrl) -or
        [string]::IsNullOrWhiteSpace($entry.BestImageUrl))) { throw 'Image publication is missing preview/original URL.' }
    if ($entry.MediaType -ne 'image' -and $entry.HasImage) { throw 'Non-image publication was exposed as an image.' }
    [pscustomobject]@{
        Date = $entry.Date; Status = 'Parsed'; Media = $entry.MediaType
        Fallback = $entry.IsFallbackImage; Post = $entry.PostUrl
        Preview = $entry.PreviewImageUrl; Original = $entry.BestImageUrl
    }
}

foreach ($date in $parsedDates) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $entry = $getEntry.Invoke($client, [object[]] @($date))
        $row = Test-Entry $entry $date
        $row | Add-Member -NotePropertyName ElapsedMs -NotePropertyValue $timer.ElapsedMilliseconds
        $results.Add($row)
    }
    catch {
        $cause = $_.Exception
        while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
        $failed = $true
        $results.Add([pscustomobject]@{
            Date = $date.ToString('yyyy-MM-dd'); Status = 'Failed'
            Error = $cause.GetType().Name; Message = $cause.Message
            ElapsedMs = $timer.ElapsedMilliseconds
        })
    }
    # Sequential requests with a pause; no 31-request parallel calendar scan.
    Start-Sleep -Milliseconds 1000
}
if ($CheckRange) {
    try {
        $start = [datetime]::new(2026, 8, 1)
        $end = [datetime]::new(2026, 8, 31)
        $entries = @($type.GetMethod('GetEntries').Invoke($client, [object[]] @($start, $end)))
        if ($entries.Count -ne 31) { throw "Expected 31 August publications; got $($entries.Count)." }
        for ($i = 0; $i -lt $entries.Count; $i++) {
            $null = Test-Entry $entries[$i] ($start.AddDays($i))
        }
        $results.Add([pscustomobject]@{ Range = '2026-08'; Status = 'Parsed'; Count = $entries.Count })
    }
    catch {
        $failed = $true
        $cause = $_.Exception
        while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
        $results.Add([pscustomobject]@{ Range = '2026-08'; Status = 'Failed'; Message = $cause.Message })
    }
}
$results | ConvertTo-Json -Depth 4
if ($failed) { exit 1 }
