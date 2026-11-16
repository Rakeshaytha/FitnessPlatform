$root = "D:\Projects\FitnessPlatform"

$projects = Get-ChildItem -Path $root -Recurse -Filter "*.csproj" |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

foreach ($project in $projects) {

    [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw

    $nodes = @(
        $xml.SelectNodes("//*[local-name()='Compile' or local-name()='None']") |
        Where-Object {
            $_.GetAttribute("Include") -match '^(obj[\\/]|\.\\obj[\\/])'
        }
    )

    if ($nodes.Count -eq 0) {
        continue
    }

    $backup = "$($project.FullName).bak"

    Copy-Item -LiteralPath $project.FullName -Destination $backup -Force

    foreach ($node in $nodes) {
        [void]$node.ParentNode.RemoveChild($node)
    }

    $xml.Save($project.FullName)

    Write-Host "Cleaned $($project.Name): removed $($nodes.Count) generated-file references" -ForegroundColor Green
}

Write-Host "Project cleanup completed." -ForegroundColor Cyan