$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scripts = @(Get-ChildItem -LiteralPath $root -Filter '*.ps1' -File -Recurse -ErrorAction Stop)
$failures = 0

# Windows PowerShell 5.1 accepts certain Unicode typographic quotes as string
# delimiters. Reject them in scripts: build such text with [char]0x2019 instead.
$typographicQuotes = @([char]0x2018, [char]0x2019, [char]0x201C, [char]0x201D)
foreach ($script in $scripts) {
    $sourceLines = [System.IO.File]::ReadAllLines($script.FullName)
    for ($lineIndex = 0; $lineIndex -lt $sourceLines.Length; $lineIndex++) {
        foreach ($quoteChar in $typographicQuotes) {
            if ($sourceLines[$lineIndex].IndexOf($quoteChar) -ge 0) {
                $relativePath = $script.FullName.Substring($root.Length).TrimStart('\')
                Write-Host ('[ERRORE] Apostrofo o virgoletta tipografica nello script: ' + $relativePath +
                    ' (riga ' + ($lineIndex + 1) + '). Utilizzare apici ASCII o [char]0x2019.') -ForegroundColor Red
                $failures++
                break
            }
        }
    }
    $tokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
    foreach ($parseError in $parseErrors) {
        $relativePath = $script.FullName.Substring($root.Length).TrimStart('\')
        $line = $parseError.Extent.StartLineNumber
        $column = $parseError.Extent.StartColumnNumber
        Write-Host ('[ERRORE] Sintassi PowerShell: ' + $relativePath) -ForegroundColor Red
        Write-Host ('  Riga ' + $line + ', colonna ' + $column + ': ' + $parseError.Message) -ForegroundColor Red
        $failures++
    }
}

if ($failures -gt 0) {
    throw ('Preflight PowerShell non superato: ' + $failures + ' errore/i di sintassi. Nessun test o build successivo e stato avviato.')
}
Write-Host ('PASS Sintassi PowerShell: ' + $scripts.Count + ' script verificati.') -ForegroundColor Green
