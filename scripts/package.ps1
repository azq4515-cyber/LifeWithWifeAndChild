# 打包一个可直接丢进 Mods\ 的发布包。
#
# 用法（Windows PowerShell 5.1 或 PowerShell 7 都行）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1
# 产物：
#   dist\LifeWithWifeAndChild-<版本>.zip
#     └─ LifeWithWifeAndChild\
#          manifest.json
#          LifeWithWifeAndChild.dll
#          i18n\default.json
#          i18n\zh-CN.json
#          README.md
#          LICENSE
#
# 不打包：pdb（调试符号）、config.json（每个玩家自己生成）、源码、bin/obj。
#
# 注意：这里手写 zip 条目，用正斜杠作分隔符。
#   Compress-Archive 在 Windows PowerShell 5.1 上会写出反斜杠路径，
#   虽然 Windows 能打开，但不符合 zip 规范，Linux/macOS 的某些解压工具会解成怪名字。

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$stageRoot = Join-Path $dist "stage"
$stage = Join-Path $stageRoot "LifeWithWifeAndChild"

Write-Host "== 1. 读版本号 =="
$manifest = Get-Content (Join-Path $root "manifest.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $manifest.Version
Write-Host ("   {0} {1}" -f $manifest.Name, $version)

Write-Host "== 2. 编译（$Configuration）=="
Push-Location $root
try {
    dotnet build -c $Configuration -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "构建失败（exit $LASTEXITCODE）" }
} finally {
    Pop-Location
}

Write-Host "== 3. 准备发布目录 =="
if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "i18n") -Force | Out-Null

$dll = Join-Path $root "bin\$Configuration\LifeWithWifeAndChild.dll"
if (-not (Test-Path $dll)) { throw "找不到构建产物：$dll" }

Copy-Item $dll $stage
Copy-Item (Join-Path $root "manifest.json") $stage
Copy-Item (Join-Path $root "README.md") $stage
Copy-Item (Join-Path $root "i18n\default.json") (Join-Path $stage "i18n")
Copy-Item (Join-Path $root "i18n\zh-CN.json") (Join-Path $stage "i18n")
Copy-Item (Join-Path $root "LICENSE") $stage

Write-Host "== 4. 压缩 =="
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zipPath = Join-Path $dist ("LifeWithWifeAndChild-" + $version + ".zip")
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$zipStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)
try {
    $archive = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = Get-ChildItem $stage -Recurse -File
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
            $entryName = "LifeWithWifeAndChild/" + $relative

            $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $input = [System.IO.File]::OpenRead($file.FullName)
                try { $input.CopyTo($entryStream) } finally { $input.Dispose() }
            } finally {
                $entryStream.Dispose()
            }
        }
    } finally {
        $archive.Dispose()
    }
} finally {
    $zipStream.Dispose()
}

Write-Host "== 5. 校验 =="
$readArchive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    foreach ($entry in ($readArchive.Entries | Where-Object { $_.Name -ne "" })) {
        Write-Host ("   {0}  ({1:N0} bytes)" -f $entry.FullName, $entry.Length)
    }

    $names = @($readArchive.Entries | ForEach-Object { $_.FullName })
    foreach ($required in @(
        "LifeWithWifeAndChild/manifest.json",
        "LifeWithWifeAndChild/LifeWithWifeAndChild.dll",
        "LifeWithWifeAndChild/i18n/zh-CN.json"
    )) {
        if ($names -notcontains $required) { throw "zip 结构不对：缺少 $required" }
    }

    if ($names | Where-Object { $_ -match '\\' }) { throw "zip 里有反斜杠路径" }
} finally {
    $readArchive.Dispose()
}

$sizeKb = [Math]::Round((Get-Item $zipPath).Length / 1KB, 1)
Write-Host ""
Write-Host ("完成：{0}  ({1} KB)" -f $zipPath, $sizeKb)
Write-Host "玩家用法：解压后把 LifeWithWifeAndChild 文件夹整个放进 Mods\"
