# Calidad de código — GlamSpace-Back

Sprint 4 · Responsable: Adrian Adonai García Jiménez

## 1. Herramienta elegida: SonarQube Cloud

**SonarQube Cloud** (antes SonarCloud) es el servicio en la nube de SonarSource para el análisis estático de código.

### Justificación técnica

| Criterio | Qué aporta al proyecto |
|---|---|
| **Bugs** | Detecta errores de lógica que pueden fallar en tiempo de ejecución (referencias nulas, condiciones siempre verdaderas, recursos sin liberar). |
| **Vulnerabilidades** | Encuentra problemas de seguridad (inyección, datos sensibles, criptografía débil) y los *security hotspots* que hay que revisar a mano. |
| **Code smells** | Señala código difícil de mantener: métodos muy largos, complejidad alta, nombres confusos, código muerto. |
| **Duplicación** | Mide el porcentaje de líneas duplicadas para detectar copiar y pegar. |
| **Deuda técnica** | Estima el tiempo que costaría corregir todos los *code smells* y la expresa como calificación (A–E). |
| **Quality Gate** | Define una condición de aprobado/reprobado (ej. sin bugs nuevos, cobertura mínima, duplicación < 3%) para cada análisis y cada PR. |
| **Integración con GitHub Actions** | Corre automáticamente con cada push y PR, sin servidores propios. |
| **Comentarios en los PR** | Publica en cada Pull Request el resultado del Quality Gate y los hallazgos del código nuevo. |
| **Costo** | Gratis para repositorios públicos. |
| **Soporte de C# / .NET** | Tiene un analizador nativo para C# (reglas `csharpsquid:S...`) integrado con `dotnet build`. |

Se eligió sobre otras opciones porque cubre en una sola herramienta todos los aspectos de calidad (bugs, seguridad, mantenibilidad y duplicación), en lugar de combinar varios analizadores, y porque se integra con el flujo de ramas y PRs que ya usa el equipo.

## 2. Instalación y configuración

1. **Crear el proyecto en sonarcloud.io**
   - Entrar a https://sonarcloud.io con la cuenta de GitHub.
   - *+* → *Analyze new project* → elegir la organización `4drian-g4rci4` → seleccionar el repo `GlamSpace-Back`.
   - Verificar que la clave del proyecto sea `4DRIAN-G4RCI4_GlamSpace-Back`.
2. **Generar el token**
   - En sonarcloud.io: *My Account* → *Security* → *Generate Tokens*. Copiarlo (solo se muestra una vez).
3. **Guardar el token en GitHub Secrets** (nunca en el repositorio)
   - En GitHub: *Settings* → *Secrets and variables* → *Actions* → *New repository secret*.
   - Nombre: `SONAR_TOKEN`, valor: el token generado.
4. **Desactivar Automatic Analysis**
   - En sonarcloud.io, dentro del proyecto: *Administration* → *Analysis Method* → desactivar *Automatic Analysis*.
   - Es obligatorio: el análisis automático y el de GitHub Actions no pueden convivir. El de Actions es el correcto para .NET porque analiza el código mientras compila.

### Requisitos del entorno de análisis

- .NET SDK 9.
- **Java 21 o mayor.** SonarQube Cloud retiró el soporte de Java 17 para los scanners el 20 de julio de 2026.
- `dotnet-sonarscanner` (herramienta global de .NET).

## 3. Cómo ejecutar el análisis

### Automático (GitHub Actions)

El workflow `.github/workflows/sonar.yml` corre:
- en cada **push a `develop`, a `main` y a `Sprint4_Adrian`**;
- en cada **Pull Request** (cuando se abre, se actualiza o se reabre).

> En un Pull Request, Sonar solo analiza el **código nuevo** del PR. Para el análisis completo de
> todo el código, la rama principal del proyecto en SonarQube Cloud se configuró como `Sprint4_Adrian`,
> y cada push a esa rama genera un análisis de todo el proyecto.

Pasos del workflow: checkout con historial completo (`fetch-depth: 0`) → .NET 9 → Java 21 → instalar `dotnet-sonarscanner` → `begin` → `dotnet build GlamSpaces.sln` → `end`. El resultado aparece en sonarcloud.io y en el PR.

Solo se excluyen `bin/` y `obj/` (archivos generados al compilar). **No se excluye ni se oculta ningún hallazgo del código fuente.**

### Local

Con el token en una variable de entorno (nunca escrito en un archivo del repo):

```powershell
# PowerShell
dotnet tool install --global dotnet-sonarscanner
$env:SONAR_TOKEN = "<tu token>"

dotnet sonarscanner begin /k:"4DRIAN-G4RCI4_GlamSpace-Back" /o:"4drian-g4rci4" /d:sonar.token="$env:SONAR_TOKEN" /d:sonar.host.url="https://sonarcloud.io"
dotnet build GlamSpaces.sln
dotnet sonarscanner end /d:sonar.token="$env:SONAR_TOKEN"
```

```bash
# Bash
export SONAR_TOKEN="<tu token>"
dotnet sonarscanner begin /k:"4DRIAN-G4RCI4_GlamSpace-Back" /o:"4drian-g4rci4" /d:sonar.token="$SONAR_TOKEN" /d:sonar.host.url="https://sonarcloud.io"
dotnet build GlamSpaces.sln
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

> Para el análisis local también hay que tener *Automatic Analysis* desactivado.

## 4. Análisis inicial

Primer análisis de todo el código, antes de corregir cualquier hallazgo.

- **Fecha:** 22/10/2026, 19:47 UTC
- **Rama:** `Sprint4_Adrian` (rama principal del proyecto en SonarQube Cloud)
- **Commit analizado:** `0feb322`
- **Proyecto:** [4DRIAN-G4RCI4_GlamSpace-Back](https://sonarcloud.io/dashboard?id=4DRIAN-G4RCI4_GlamSpace-Back&branch=Sprint4_Adrian)
- **Quality Gate:** ✅ **Aprobado** (Sonar way: las condiciones se evalúan sobre el código nuevo)
- **Fuente:** API pública de SonarQube Cloud (`/api/measures/component`, `/api/qualitygates/project_status`, `/api/issues/search`, `/api/hotspots/search`)

### Métricas

| Métrica | Clave Sonar | Valor |
|---|---|---|
| Líneas de código | `ncloc` | 1,197 |
| Bugs | `bugs` | 0 |
| Vulnerabilidades | `vulnerabilities` | 1 |
| Code smells | `code_smells` | 1 |
| Security hotspots | `security_hotspots` | 0 |
| Duplicación | `duplicated_lines_density` | 0.0 % |
| Deuda técnica | `sqale_index` | 5 min |
| Calificación de confiabilidad | `reliability_rating` | A (1.0) |
| Calificación de seguridad | `security_rating` | **C (3.0)** |
| Calificación de mantenibilidad | `sqale_rating` | A (1.0) |

### Hallazgos (ordenados por severidad)

| # | Severidad | Tipo | Regla | Archivo | Línea | Mensaje | Impacto | Esfuerzo |
|---|---|---|---|---|---|---|---|---|
| 1 | Major | Vulnerabilidad | `csharpsquid:S5122` | `GlamSpaces.Api/Program.cs` | 51 | Restrict this CORS policy to trusted origins. | Seguridad / media | 4 h |
| 2 | Major | Code smell | `csharpsquid:S6966` | `GlamSpaces.Api/Program.cs` | 75 | Await RunAsync instead. | Confiabilidad / media | 5 min |

La calificación de seguridad **C** se debe al hallazgo 1. La deuda técnica (5 min) solo cuenta code smells, por eso no incluye las 4 h de la vulnerabilidad.

<!-- CAPTURA: resumen del análisis inicial en sonarcloud.io (Overview) -->

<!-- CAPTURA: lista de hallazgos del análisis inicial (Issues) -->

## 5. Hallazgos corregidos

| # | Hallazgo | Regla Sonar | Archivo | Por qué es un problema | Cómo se corrigió | Commit |
|---|---|---|---|---|---|---|
| 1 | | | | | | |
| 2 | | | | | | |
| 3 | | | | | | |

<!-- CAPTURA: hallazgo 1 antes/después -->

<!-- CAPTURA: hallazgo 2 antes/después -->

<!-- CAPTURA: hallazgo 3 antes/después -->

## 6. Análisis final

Análisis después de las correcciones.

- Fecha:
- Commit analizado:
- Quality Gate:

| Métrica | Inicial | Final |
|---|---|---|
| Bugs | | |
| Vulnerabilidades | | |
| Security hotspots | | |
| Code smells | | |
| Duplicación | | |
| Deuda técnica | | |

<!-- CAPTURA: resumen del análisis final en sonarcloud.io (Overview) -->

<!-- CAPTURA: comentario de SonarQube Cloud en el Pull Request -->
