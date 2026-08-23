param(
	[Parameter(Mandatory=$true)]
	[ValidateSet('replace','update')]
	[string]$Action,

	[Parameter(Mandatory=$true)]
	[string]$LocalDbPath,

	[Parameter(Mandatory=$true)]
	[string]$AndroidDbPath
)

function Exec-Adb {
	param(
		[string[]]$Args,
		[string]$RedirectStdOutPath
	)

	# Use PowerShell native invocation to capture output and reliable exit code
	$adb = 'adb'
	if ($RedirectStdOutPath) {
		try {
			# Run adb and redirect both stdout and stderr to the file for diagnostics
			& $adb @Args 2>&1 | Out-File -FilePath $RedirectStdOutPath -Encoding UTF8
			$exit = $LASTEXITCODE
		}
		catch {
			$_ | Out-File -FilePath $RedirectStdOutPath -Append -Encoding UTF8
			$exit = 1
		}
		return [PSCustomObject]@{ ExitCode = $exit; StdOutPath = $RedirectStdOutPath }
	}
	else {
		$output = & $adb @Args 2>&1
		$exit = $LASTEXITCODE
		return [PSCustomObject]@{ ExitCode = $exit; Output = $output }
	}
}

function Ensure-FileExists {
	param([string]$Path)
	if (-not (Test-Path $Path)) {
		Write-Error "File not found: $Path"
		exit 2
	}
}

if (-not (Get-Command adb -ErrorAction SilentlyContinue)) {
	Write-Error "adb not found in PATH. Install Android Platform Tools and ensure 'adb' is available."
	exit 3
}

Ensure-FileExists -Path $LocalDbPath

$package = $AndroidDbPath

Write-Host "Action: $Action"
Write-Host "Local DB: $LocalDbPath"
Write-Host "Android package: $package"

# Discover databases in the installed app
$lsArgs = @('shell', "run-as $package ls /data/data/$package/databases")
$tmpOut = [System.IO.Path]::GetTempFileName()
$res = Exec-Adb -Args $lsArgs -RedirectStdOutPath $tmpOut
if ($res.ExitCode -ne 0) {
	$dbg = Get-Content $tmpOut -ErrorAction SilentlyContinue
	Write-Error "Failed to list databases for package $package. Ensure the device/emulator is connected and the app is debuggable (so run-as works).`nADB output:`n$($dbg -join "`n")"
	Remove-Item $tmpOut -ErrorAction SilentlyContinue
	exit 4
}

$dbList = Get-Content $tmpOut | Where-Object { $_ -and ($_ -ne '.') -and ($_ -ne '..') } | ForEach-Object { $_.Trim() }
Remove-Item $tmpOut -ErrorAction SilentlyContinue

if (-not $dbList -or $dbList.Count -eq 0) {
	Write-Error "No databases found in /data/data/$package/databases."
	exit 5
}

function Choose-TargetDb {
	param([string[]]$DbList, [string]$SourcePath)
	if ($DbList.Count -eq 1) { return $DbList[0] }
	$sourceBase = [System.IO.Path]::GetFileName($SourcePath)
	foreach ($d in $DbList) { if ($d -eq $sourceBase) { return $d } }
	Write-Host "Multiple databases found: $($DbList -join ', ')"
	Write-Host "No exact match for source basename '$sourceBase'. Will pick the first database: $($DbList[0])"
	return $DbList[0]
}

$targetDb = Choose-TargetDb -DbList $dbList -SourcePath $LocalDbPath

if ($Action -eq 'replace') {
	# Replace entire DB file. Use basename for target file name.
	$basename = [System.IO.Path]::GetFileName($LocalDbPath)

	Write-Host "Pushing $LocalDbPath to device temporary location..."
	$res = Exec-Adb -Args @('push', $LocalDbPath, "/data/local/tmp/$basename")
	if ($res.ExitCode -ne 0) { Write-Error "adb push failed. Output: $($res.Output -join "`n")"; exit 6 }

	Write-Host "Copying into app databases via run-as (will overwrite)..."
	$shellCmd = "run-as $package cp /data/local/tmp/$basename /data/data/$package/databases/$basename && rm /data/local/tmp/$basename"
	$res = Exec-Adb -Args @('shell', $shellCmd)
	if ($res.ExitCode -ne 0) { Write-Error "Failed to copy replacement DB into app databases. Output: $($res.Output -join "`n")"; exit 7 }

	Write-Host "Replace completed: /data/data/$package/databases/$basename"
	exit 0
}

if ($Action -eq 'update') {
	# Merge Accounts table from LocalDbPath into the installed DB (targetDb)
	# Requires sqlite3 CLI available locally
	if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
		Write-Error "sqlite3 CLI not found in PATH. Install sqlite3 (https://www.sqlite.org/download.html) or ensure it's on PATH."
		exit 8
	}

	$tempDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ([System.IO.Path]::GetRandomFileName())
	New-Item -ItemType Directory -Path $tempDir | Out-Null
	$localTarget = Join-Path $tempDir $targetDb
	$localSource = Join-Path $tempDir ([System.IO.Path]::GetFileName($LocalDbPath))

	try {
		Write-Host "Pulling target DB from device..."
		$args = @('exec-out', "run-as $package cat /data/data/$package/databases/$targetDb")
		$res = Exec-Adb -Args $args -RedirectStdOutPath $localTarget
		if ($res.ExitCode -ne 0) {
			$dbg = Get-Content $localTarget -ErrorAction SilentlyContinue
			Write-Error "Failed to pull target DB. adb output:`n$($dbg -join "`n")"; exit 9 }

		Write-Host "Copying local source DB to temp workspace..."
		Copy-Item -Path $LocalDbPath -Destination $localSource -Force

		Write-Host "Merging Accounts table from source into target (local)..."
		$sql = "ATTACH '$localSource' AS src; BEGIN TRANSACTION; PRAGMA foreign_keys=OFF; DELETE FROM Accounts; INSERT INTO Accounts SELECT * FROM src.Accounts; COMMIT; DETACH src;"

		$proc = Start-Process -FilePath "sqlite3" -ArgumentList @($localTarget, $sql) -NoNewWindow -Wait -PassThru
		if ($proc.ExitCode -ne 0) { Write-Error "sqlite3 reported an error (exit $($proc.ExitCode))."; exit 10 }

		Write-Host "Pushing modified DB back to device temporary location..."
		$basename = $targetDb
		$res = Exec-Adb -Args @('push', $localTarget, "/data/local/tmp/$basename")
		if ($res.ExitCode -ne 0) { Write-Error "adb push failed. Output: $($res.Output -join "`n")"; exit 11 }

		Write-Host "Replacing device DB with merged DB..."
		$shellCmd = "run-as $package cp /data/local/tmp/$basename /data/data/$package/databases/$basename && rm /data/local/tmp/$basename"
		$res = Exec-Adb -Args @('shell', $shellCmd)
		if ($res.ExitCode -ne 0) { Write-Error "Failed to replace DB on device. Output: $($res.Output -join "`n")"; exit 12 }

		Write-Host "Update completed for Accounts table in $targetDb"
		exit 0
	}
	finally {
		Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
	}
}
