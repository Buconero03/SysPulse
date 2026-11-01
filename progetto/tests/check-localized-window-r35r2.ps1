param([string] $ProjectRoot)
$ErrorActionPreference = 'Stop'
if (-not $ProjectRoot) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$app = Join-Path $ProjectRoot 'src/ProcessLens.App'
$controls = Get-Content -LiteralPath (Join-Path $app 'Localization/LocalizedControls.cs') -Raw
$count = 0
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $app 'Localized') -Filter '*.g.xaml' -File) {
    [xml] $markup = Get-Content -LiteralPath $file.FullName -Raw
    $element = $markup.DocumentElement
    if ($element.NamespaceURI -ne 'clr-namespace:ProcessLens.App') { continue }
    $baseName = $element.LocalName
    $className = $element.GetAttribute('Class','http://schemas.microsoft.com/winfx/2006/xaml').Split('.')[-1]
    $declaration = [regex]::Match($controls, '(?m)^public\s+(?<modifiers>(?:\w+\s+)*)class\s+' + [regex]::Escape($baseName) + '\s*:')
    if (-not $declaration.Success -or $declaration.Groups['modifiers'].Value -match '\b(sealed|static)\b') {
        throw ('R35 R2: XAML root ' + $baseName + ' must be a public inheritable class for ' + $className)
    }
    $codePath = Join-Path $file.DirectoryName ($file.Name.Replace('.g.xaml','.xaml.g.cs'))
    $code = Get-Content -LiteralPath $codePath -Raw
    $pattern = '\bpartial\s+class\s+' + [regex]::Escape($className) + '\s*:\s*(?:global::)?(?:ProcessLens\.App\.)?' + [regex]::Escape($baseName) + '\b'
    if (-not [regex]::IsMatch($code,$pattern)) {
        throw ('R35 R2: generated code-behind and XAML must use the same base ' + $baseName + ' for ' + $className)
    }
    $count++
}
if ($count -lt 1) { throw 'R35 R2: no localized x:Class root checked.' }
Write-Host ('PASS R35 R2: ' + $count + ' localized XAML root(s) are inheritable and match the generated code-behind. Actual BAML and Windows runtime checks remain separate.')
