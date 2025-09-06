# Run-PipeWise.ps1
# מפעיל את שרת ה-API (uvicorn) + ממתין לבריאות + מפעיל את לקוח ה-WPF + סוגר את השרת בסיום

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# === הגדרות ===
# שורש הפרויקט (כדי שפייתון ימצא core/, utils/, security/)
$ProjectRoot = "C:\Users\shlom\PipeWise"

# התיקייה שבה נמצא server.py (לא את הקובץ עצמו!)
$ApiDir     = "C:\Users\shlom\PipeWise\api"
# המודול/אפליקציה של uvicorn (server.py מכיל app)
$UvicornApp = "server:app"
$ApiHost    = "127.0.0.1"
$ApiPort    = 8000

# אם יש לך venv – ציין כאן (לא חובה). אחרת נאתר python מה-Path
$VenvPath   = ""   # למשל: "C:\Users\shlom\PipeWise\.venv"

# נתיב לאפליקציית הלקוח (נעדיף Release אם קיים)
$ClientExeRelease = "C:\Users\shlom\PipeWise_client\bin\Release\net8.0-windows\PipeWise.Client.exe"
$ClientExeDebug   = "C:\Users\shlom\PipeWise_client\bin\Debug\net8.0-windows\PipeWise.Client.exe"

# משתני סביבה אופציונליים ל-API
$env:AUTH_REQUIRED = "0"
# $env:PIPEWISE_PIPELINES_DIR = "C:\Users\shlom\PipeWise\config\pipelines"
# $env:PIPEWISE_REPORTS_DIR   = "C:\Users\shlom\PipeWise\reports"

# === לוגים ===
$LogDir   = Join-Path $PSScriptRoot "logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$StdOutLog = Join-Path $LogDir "PipeWise-API.out.log"
$StdErrLog = Join-Path $LogDir "PipeWise-API.err.log"

function Test-PortOpen([string]$RemoteHost, [int]$Port) {
  try {
    $r = Test-NetConnection -ComputerName $RemoteHost -Port $Port `
         -WarningAction SilentlyContinue -InformationLevel Quiet
    return [bool]$r
  } catch { return $false }
}

function Wait-For-Health($url, [int]$timeoutSec = 30) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $timeoutSec) {
    try {
      $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
      if ($resp.StatusCode -eq 200) { return $true }
    } catch {}
    Start-Sleep -Milliseconds 500
  }
  return $false
}

# === איתור python ===
$PythonExe = $null
if ($VenvPath -and (Test-Path (Join-Path $VenvPath "Scripts\python.exe"))) {
  $PythonExe = Join-Path $VenvPath "Scripts\python.exe"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
  $PythonExe = (Get-Command python).Path
}
if (-not $PythonExe) {
  Write-Host "ERROR: Python was not found. Install Python or set `$VenvPath." -ForegroundColor Red
  Pause
  exit 1
}

# === בדיקה שהתיקיות קיימות ===
if (-not (Test-Path $ApiDir)) {
  Write-Host "ERROR: ApiDir '$ApiDir' not found." -ForegroundColor Red
  Pause
  exit 1
}
if (-not (Test-Path $ProjectRoot)) {
  Write-Host "ERROR: ProjectRoot '$ProjectRoot' not found." -ForegroundColor Red
  Pause
  exit 1
}

# === PYTHONPATH לשורש הפרויקט (כדי שייבואי core/... יעבדו) ===
$env:PYTHONPATH = if ($env:PYTHONPATH) { "$ProjectRoot;$env:PYTHONPATH" } else { "$ProjectRoot" }
$env:PYTHONUNBUFFERED = "1"   # לוגים בזמן אמת

# === הפעלת השרת אם אינו מאזין ===
$ServerAlreadyRunning = Test-PortOpen -RemoteHost $ApiHost -Port $ApiPort
$ServerProc = $null

if (-not $ServerAlreadyRunning) {
  Write-Host "Starting API ($UvicornApp) at $ApiHost`:$ApiPort ..." -ForegroundColor Cyan
  $args = @(
    "-m","uvicorn",
    $UvicornApp,
    "--host",$ApiHost,
    "--port",$ApiPort,
    "--log-level","info"
  )

  try {
    $ServerProc = Start-Process -FilePath $PythonExe `
                                -ArgumentList $args `
                                -WorkingDirectory $ApiDir `
                                -WindowStyle Minimized `
                                -RedirectStandardOutput $StdOutLog `
                                -RedirectStandardError  $StdErrLog `
                                -PassThru
  } catch {
    Write-Host "Failed to start API. See logs at: $StdOutLog / $StdErrLog" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Pause
    exit 1
  }
} else {
  Write-Host "API already running on $ApiHost`:$ApiPort" -ForegroundColor Yellow
}

# === המתנה ל-/health (לא חוסם לנצח) ===
$HealthUrl = "http://$ApiHost`:$ApiPort/health"
if (Wait-For-Health $HealthUrl 30) {
  Write-Host "API is healthy at $HealthUrl" -ForegroundColor Green
} else {
  Write-Host "Warning: API health check timed out ($HealthUrl). Continuing..." -ForegroundColor Yellow
  Write-Host "Check logs: $StdOutLog / $StdErrLog" -ForegroundColor Yellow
}

# === הפעלת הלקוח ===
$ClientExe = if (Test-Path $ClientExeRelease) { $ClientExeRelease }
             elseif (Test-Path $ClientExeDebug) { $ClientExeDebug }
             else {
               Write-Host "ERROR: PipeWise.Client.exe not found (Release/Debug)." -ForegroundColor Red
               if ($ServerProc) { try { Stop-Process -Id $ServerProc.Id -Force } catch {} }
               Pause
               exit 1
             }

Write-Host "Starting client: $ClientExe" -ForegroundColor Cyan
$client = Start-Process -FilePath $ClientExe -WorkingDirectory (Split-Path $ClientExe) -PassThru
Wait-Process -Id $client.Id

# === סגירת השרת (אם אנחנו פתחנו אותו) ===
if ($ServerProc -and -not $ServerAlreadyRunning) {
  try {
    if (-not $ServerProc.HasExited) {
      Write-Host "Stopping API server..." -ForegroundColor Cyan
      Stop-Process -Id $ServerProc.Id -Force
    }
  } catch {}
}

Write-Host "Done. Logs at: $StdOutLog / $StdErrLog" -ForegroundColor Green
