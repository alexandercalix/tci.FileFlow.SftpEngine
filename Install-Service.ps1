<#
.SYNOPSIS
Installs the SFTP Engine as a native Windows Service.

.DESCRIPTION
This script configures a new instance of the SFTP Engine, binds it to a specific port,
and registers it as an automatic Windows Service without needing NSSM.
#>

# Requires Run as Administrator
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Este script necesita permisos de Administrador para crear el servicio."
    Write-Warning "Por favor, haz clic derecho sobre este archivo y selecciona 'Ejecutar como Administrador'."
    Pause
    exit
}

Clear-Host
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Instalador del Servicio SFTP Engine " -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Get current script path
$scriptPath = $PSScriptRoot
$exeName = "tci.FileFlow.SftpEngine.Blazor.exe"
$exePath = Join-Path -Path $scriptPath -ChildPath $exeName

if (!(Test-Path $exePath)) {
    Write-Host "Error: No se encontro el archivo $exeName en esta carpeta." -ForegroundColor Red
    Write-Host "Asegurate de extraer todos los archivos del .zip antes de ejecutar este script." -ForegroundColor Yellow
    Pause
    exit
}

# Prompt for Instance Name
$defaultName = "SFTPEngine_01"
$instanceName = Read-Host "Ingresa el nombre del Servicio [Por defecto: $defaultName]"
if ([string]::IsNullOrWhiteSpace($instanceName)) {
    $instanceName = $defaultName
}

# Prompt for Port
$defaultPort = "80"
$port = Read-Host "Ingresa el Puerto en el que correra (Ej: 80, 81) [Por defecto: $defaultPort]"
if ([string]::IsNullOrWhiteSpace($port)) {
    $port = $defaultPort
}

# Prompt for AutoStart
$autoStart = Read-Host "Deseas que el servicio inicie automaticamente con Windows? (S/N) [Por defecto: S]"
$startType = "auto"
if ($autoStart.ToUpper() -eq "N") {
    $startType = "demand"
}

Write-Host ""
Write-Host "------------------------------------------------" -ForegroundColor Cyan
Write-Host "Resumen de Instalacion:" -ForegroundColor Yellow
Write-Host "  Nombre del Servicio : $instanceName"
Write-Host "  Puerto              : $port"
Write-Host "  Ruta del Ejecutable : $exePath"
Write-Host "------------------------------------------------" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Presiona 'S' para proceder o cualquier otra tecla para cancelar"
if ($confirm.ToUpper() -ne "S") {
    Write-Host "Instalacion cancelada." -ForegroundColor Red
    Pause
    exit
}

# Stop and delete if exists
$existingService = Get-Service -Name $instanceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "El servicio ya existe. Deteniendo e intentando actualizar..." -ForegroundColor Yellow
    Stop-Service -Name $instanceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $instanceName | Out-Null
    Start-Sleep -Seconds 2
}

# Build the execution command with arguments
# The quotes are tricky. We need binPath to be strictly "C:\path\to\exe" --urls=http://*:<port>
$binPath = "`"$exePath`" --urls=http://*:$port"

# Create the service using sc.exe
Write-Host "Creando el servicio en Windows..." -ForegroundColor Green
$createResult = sc.exe create $instanceName binPath= $binPath start= $startType DisplayName= "TCI SFTP Engine ($port)"
Write-Host $createResult

# Set Description
sc.exe description $instanceName "Motor de transferencia SFTP. Corriendo en el puerto: $port" | Out-Null

# Try to start it
if ($startType -eq "auto") {
    Write-Host "Iniciando el servicio..." -ForegroundColor Green
    Start-Service -Name $instanceName -ErrorAction Continue
    
    $status = (Get-Service -Name $instanceName).Status
    Write-Host "Estado actual del servicio: $status" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "------------------------------------------------" -ForegroundColor Green
    Write-Host "¡Instalación Completada con Éxito!" -ForegroundColor Green
    Write-Host "Puedes acceder a la consola desde cualquier navegador en: http://localhost:$port" -ForegroundColor Green
    Write-Host "------------------------------------------------" -ForegroundColor Green
}
else {
    Write-Host "Servicio creado. Deberas iniciarlo manualmente en los Servicios de Windows." -ForegroundColor Yellow
}

Write-Host ""
Pause
