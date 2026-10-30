<#
.SYNOPSIS
    Genera memoria/bd/GlamSpaces_BD_Completa.sql: el esquema completo y vigente de la BD.

.DESCRIPTION
    Une, en orden, los scripts de Database/ que definen el esquema (tablas, stored procedures,
    índices y restricciones). No incluye datos de prueba (04), pruebas (05) ni semillas.
    El archivo generado es idempotente: se puede correr en una BD vacía o en una que ya tiene
    el esquema.

    Regla (memoria/REGLAS.md): cada script nuevo de Database/ que cambie el esquema se agrega a
    la lista $Scripts y se vuelve a correr este generador. El workflow "Migraciones BD" falla si
    el archivo generado no coincide con el del repo.

.EXAMPLE
    .\scripts\generar-bd-completa.ps1
#>
$ErrorActionPreference = "Stop"

$Raiz = Split-Path -Parent $PSScriptRoot
$Salida = Join-Path $Raiz "memoria/bd/GlamSpaces_BD_Completa.sql"

# Orden de ejecución. "Hasta" corta el script antes de esa línea (para dejar fuera una semilla).
$Scripts = @(
    @{ Archivo = "01_Tablas.sql" }
    @{ Archivo = "02_StoredProcedures.sql" }
    @{ Archivo = "03_Indices.sql" }
    @{ Archivo = "06_SolicitudesHU14.sql"; Hasta = "-- 4) Semilla" }
    @{ Archivo = "07_TelefonoSolicitudesHU16.sql" }
    @{ Archivo = "08_SolicitudesHU15.sql" }
)

$partes = New-Object System.Collections.Generic.List[string]
$partes.Add(@"
/* =========================================================================
   GlamSpaces — Script completo de la base de datos (esquema vigente)
   ARCHIVO GENERADO por scripts/generar-bd-completa.ps1. NO editar a mano:
   se cambia el script de Database/ y se vuelve a generar.

   Contiene, en orden: $(($Scripts | ForEach-Object { $_.Archivo }) -join ', ').
   Es idempotente (IF NOT EXISTS / CREATE OR ALTER). Sin datos de prueba ni semillas:
   04_DatosPrueba.sql (solo local) y la semilla de 06_SolicitudesHU14.sql se corren aparte.

   Las migraciones 002 a 004 (BusquedasFavoritas, Salones.Telefono y su índice único) son de
   práctica del Sprint 4: solo existen en GlamSpaces.Migrations, no en producción.
   ========================================================================= */
"@)

foreach ($script in $Scripts) {
    $ruta = Join-Path $Raiz ("Database/" + $script.Archivo)
    if (-not (Test-Path $ruta)) { throw "No existe Database/$($script.Archivo)." }
    $lineas = [System.IO.File]::ReadAllText($ruta, [System.Text.Encoding]::UTF8).TrimStart([char]0xFEFF) -split "\r?\n"

    if ($script.Hasta) {
        $corte = [Array]::FindIndex($lineas, [Predicate[string]] { param($l) $l.StartsWith($script.Hasta) })
        if ($corte -lt 0) { throw "No se encontró '$($script.Hasta)' en $($script.Archivo)." }
        # Quita también la línea de guiones que abre esa sección.
        if ($corte -gt 0 -and $lineas[$corte - 1] -match '^-- -+$') { $corte-- }
        $lineas = $lineas[0..($corte - 1)]
    }

    $partes.Add("`n-- #########################################################################`n-- Database/$($script.Archivo)`n-- #########################################################################`n")
    $partes.Add((($lineas -join "`n").TrimEnd()) + "`nGO")
}

New-Item -ItemType Directory -Force -Path (Split-Path $Salida) | Out-Null
# Saltos LF y UTF-8 sin BOM: el resultado es el mismo en Windows y en el runner Linux de GitHub.
[System.IO.File]::WriteAllText($Salida, (($partes -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))
Write-Host "Generado memoria/bd/GlamSpaces_BD_Completa.sql ($($Scripts.Count) scripts)."
