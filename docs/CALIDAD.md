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

| # | Hallazgo | Regla Sonar | Archivo y línea | Por qué es un problema | Cómo se corrigió | Commit |
|---|---|---|---|---|---|---|
| 1 | Vulnerabilidad (Major): *Restrict this CORS policy to trusted origins.* | `csharpsquid:S5122` | `GlamSpaces.Api/Program.cs:51` | La política usaba `AllowAnyOrigin()`: **cualquier sitio web** podía llamar a la API desde el navegador de un usuario y leer sus respuestas (datos de salones, usuarios, resultados de login). Rompe la protección de "mismo origen" del navegador y facilita ataques desde páginas de terceros. | Se cambió a `WithOrigins(...)` con una **lista de orígenes permitidos**: el sitio del frontend en Azure y los puertos locales de Expo web (8081 y 19006). La lista vive en `appsettings.json` (`Cors:OrigenesPermitidos`) y en Azure se puede ajustar sin recompilar (`Cors__OrigenesPermitidos__0`, ...). Se probó con un *preflight* `OPTIONS`: el front de Azure y Expo local reciben `Access-Control-Allow-Origin`; un origen desconocido no lo recibe y el navegador bloquea la llamada. | `f55ddc1` |
| 2 | Code smell (Major, confiabilidad): *Await RunAsync instead.* | `csharpsquid:S6966` | `GlamSpaces.Api/Program.cs:75` | `app.Run()` es la versión **síncrona**: bloquea el hilo principal mientras la API está en ejecución, cuando el código ya es asíncrono (`Program.cs` usa *top-level statements* con `await`). Mezclar llamadas bloqueantes con código asíncrono puede causar bloqueos y desperdicia hilos. | Se reemplazó por `await app.RunAsync();`. Se verificó que la API arranca y Swagger responde igual. | `1c5faae` |

### ¿Por qué se corrigieron 2 hallazgos y no 3?

El análisis inicial de SonarQube Cloud (perfil **Sonar way**, el perfil por defecto) sobre las 1,197 líneas
del backend reportó **solo 2 hallazgos**: 0 bugs, 1 vulnerabilidad, 1 code smell, 0 *security hotspots*
y 0 % de duplicación. **Se corrigieron los 2 (el 100 %).**

No se agregó un tercero porque la regla del ejercicio prohíbe "inventar" o forzar hallazgos: no hay
otro hallazgo real que corregir, y no se usaron exclusiones, `//NOSONAR` ni se marcó nada como falso
positivo. El número bajo se explica por decisiones que ya se habían tomado en los sprints anteriores:

- **Arquitectura por capas** con controllers de una sola línea: casi no hay lógica que duplicar ni métodos largos.
- **Reglas de negocio en stored procedures**: el C# solo orquesta (llama al SP y arma la respuesta).
- **Contraseñas con PBKDF2** (100,000 iteraciones, salt por usuario) y comparación en tiempo constante (`FixedTimeEquals`): evita los hallazgos típicos de criptografía débil.
- **Consultas parametrizadas** (Dapper con parámetros): no hay SQL armado con concatenación de texto, que es la fuente común de vulnerabilidades de inyección.
- **Cadena de conexión fuera del repositorio** (user-secrets / Azure): no hay credenciales en el código.

<!-- CAPTURA: hallazgo 1 (S5122) antes/después -->

<!-- CAPTURA: hallazgo 2 (S6966) antes/después -->

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
