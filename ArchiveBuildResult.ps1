$configulationName = "Release"
$platformName = "Any CPU"
if ($args.Length -gt 0) {
    $configulationName = $args[0]
    if ($configulationName -ine "Publish") {
        return
    }
    if ($args.Length -gt 1) {
        $platformName = $args[1]
    }
}

$solutionDirPath = Split-Path $MyInvocation.MyCommand.Path -Parent
$workDirName = "lpubsppop01.EBookBuilder_" + $configulationName + "_" + ($platformName -replace " ", "_")
$workDirPath = Join-Path $solutionDirPath $workDirName

if (!(Test-Path -LiteralPath $workDirPath)) {
    New-Item -ItemType Directory $workDirPath
}

$srcBinDirPath = Join-Path $solutionDirPath "EBookBuilder\bin\Publish"
Copy-Item (Join-Path $solutionDirPath "README.md") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "lpubsppop01.EBookBuilder.exe") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "lpubsppop01.EBookBuilder.exe.config") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "Microsoft.WindowsAPICodePack.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "Microsoft.WindowsAPICodePack.Shell.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "Microsoft.WindowsAPICodePack.ShellExtensions.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "System.Buffers.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "System.Diagnostics.DiagnosticSource.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "System.Memory.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "System.Numerics.Vectors.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "System.Runtime.CompilerServices.Unsafe.dll") $workDirPath
Copy-Item (Join-Path $srcBinDirPath "ExifLibrary.dll") $workDirPath

$archiveFilename = $workDirName + ".zip"
$archiveFilePath = Join-Path $solutionDirPath $archiveFilename
if (Test-Path -LiteralPath $archiveFilePath) {
    Remove-Item $archiveFilePath
}
7z a $archiveFilePath $workDirPath\*

Remove-Item -Recurse $workDirPath
