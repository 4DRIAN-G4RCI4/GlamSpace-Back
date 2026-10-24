<#
.SYNOPSIS
    Migraciones de la base de datos de GlamSpaces con FluentMigrator (solo desarrollo y pruebas).

.DESCRIPTION
    Acciones:
      listar     Lista las migraciones y si están aplicadas o pendientes (dotnet-fm list migrations).
      subir      Aplica todas las migraciones pendientes (dotnet-fm migrate up).
      rollback   Revierte la última migración aplicada (dotnet-fm rollback by 1).
      reaplicar  Vuelve a aplicar lo revertido (migrate up) y lista el resultado.
      respaldar  Hace BACKUP DATABASE de la BD a un .bak local en respaldos/ y lo verifica.
      reset      Borra y recrea la BD local en LocalDB y corre todas las migraciones desde cero.
      consultar  Ejecuta una consulta de verificación (-Sql "SELECT ...") y muestra el resultado.

    Conexión por defecto: SOLO LocalDB (BD GlamSpacesMigraciones).
    Para otra BD de pruebas: variable de entorno GLAMSPACES_CONN.
    El script se niega a correr contra Azure SQL (cualquier conexión con "database.windows.net").

.EXAMPLE
    .\scripts\migraciones.ps1 listar
    .\scripts\migraciones.ps1 consultar -Sql "SELECT * FROM VersionInfo"
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("listar", "subir", "rollback", "reaplicar", "respaldar", "reset", "consultar")]
    [string]$Accion,

    [string]$Sql
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$Raiz = Split-Path -Parent $PSScriptRoot
$ConexionLocal = 'Server=(localdb)\MSSQLLocalDB;Database=GlamSpacesMigraciones;Trusted_Connection=True;TrustServerCertificate=True'
$Conexion = if ($env:GLAMSPACES_CONN) { $env:GLAMSPACES_CONN } else { $ConexionLocal }

# Barrera de seguridad: estas pruebas (rollback, reset) nunca deben tocar producción.
if ($Conexion -match 'database\.windows\.net') {
    throw "Conexión rechazada: apunta a Azure SQL (database.windows.net). Este script es solo para LocalDB o un SQL Server de pruebas."
}

$Builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $Conexion
$BaseDatos = $Builder.InitialCatalog
$Proyecto = Join-Path $Raiz "GlamSpaces.Migrations\GlamSpaces.Migrations.csproj"
$Ensamblado = Join-Path $Raiz "GlamSpaces.Migrations\bin\Release\net9.0\GlamSpaces.Migrations.dll"
$Procesador = "SqlServer2016"

function Compilar {
    Push-Location $Raiz
    try {
        dotnet tool restore | Out-Null                     # dotnet-fm desde .config/dotnet-tools.json
        dotnet build $Proyecto -c Release --nologo -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "No compiló GlamSpaces.Migrations." }
    }
    finally { Pop-Location }
}

# dotnet-fm recibe las opciones (-p, -c, -a) entre el comando y su subcomando:
#   list migrations -p ... | migrate -p ... up | rollback -p ... by 1
function Invocar-Fm([string[]]$Comando, [string[]]$Subcomando = @()) {
    Push-Location $Raiz
    try {
        & dotnet tool run dotnet-fm -- @Comando -p $Procesador -c $Conexion -a $Ensamblado @Subcomando
        if ($LASTEXITCODE -ne 0) { throw "dotnet-fm terminó con código $LASTEXITCODE." }
    }
    finally { Pop-Location }
}

# Ejecuta SQL y muestra los mensajes del servidor (PRINT, avance del BACKUP) y las filas que regrese.
function Ejecutar-Sql([string]$Texto, [string]$Catalogo = $BaseDatos) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $Conexion
    $b["Initial Catalog"] = $Catalogo   # indexador: en PowerShell, .InitialCatalog = ... se toma como clave nueva
    $cn = New-Object System.Data.SqlClient.SqlConnection $b.ConnectionString
    $cn.add_InfoMessage({ param($s, $e) Write-Host $e.Message })
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = $Texto
        $cmd.CommandTimeout = 300
        $lector = $cmd.ExecuteReader()
        # DataTable.Load avanza solo al siguiente resultado; se repite hasta mostrar todos.
        while (-not $lector.IsClosed) {
            $tabla = New-Object System.Data.DataTable
            $tabla.Load($lector)
            if ($tabla.Columns.Count -gt 0) {
                Write-Host ($tabla | Format-Table -AutoSize | Out-String -Width 200).TrimEnd()
                Write-Host "($($tabla.Rows.Count) filas)"
            }
        }
    }
    finally { $cn.Close() }
}

function Listar   { Compilar; Invocar-Fm @("list", "migrations") }
function Subir    { Compilar; Invocar-Fm @("migrate") @("up") }
function Revertir { Compilar; Invocar-Fm @("rollback") @("by", "1") }

function Respaldar {
    $carpeta = Join-Path $Raiz "respaldos"
    New-Item -ItemType Directory -Force -Path $carpeta | Out-Null
    $archivo = Join-Path $carpeta ("{0}_{1:yyyyMMdd_HHmmss}.bak" -f $BaseDatos, (Get-Date))
    Write-Host "Respaldando [$BaseDatos] en $archivo"
    # COPY_ONLY: no altera la cadena de respaldos; CHECKSUM: detecta páginas dañadas.
    Ejecutar-Sql "BACKUP DATABASE [$BaseDatos] TO DISK = N'$archivo' WITH INIT, COPY_ONLY, CHECKSUM" "master"
    Ejecutar-Sql "RESTORE VERIFYONLY FROM DISK = N'$archivo' WITH CHECKSUM" "master"
    Write-Host ("Archivo: {0} ({1:N0} KB)" -f $archivo, ((Get-Item $archivo).Length / 1KB))
}

function Reset-Limpio {
    if ($Builder.DataSource -notmatch '^\(localdb\)') {
        throw "El reset limpio solo se permite en LocalDB (borra la base de datos completa)."
    }
    Write-Host "Borrando y recreando [$BaseDatos] en $($Builder.DataSource)"
    Ejecutar-Sql @"
IF DB_ID(N'$BaseDatos') IS NOT NULL
BEGIN
    ALTER DATABASE [$BaseDatos] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$BaseDatos];
END
CREATE DATABASE [$BaseDatos];
PRINT 'Base de datos [$BaseDatos] creada vacía.';
"@ "master"
    Subir
}

switch ($Accion) {
    "listar"    { Listar }
    "subir"     { Subir }
    "rollback"  { Revertir }
    "reaplicar" { Subir; Listar }
    "respaldar" { Respaldar }
    "reset"     { Reset-Limpio }
    "consultar" {
        if (-not $Sql) { throw "Indica la consulta con -Sql `"SELECT ...`"." }
        Ejecutar-Sql $Sql
    }
}
