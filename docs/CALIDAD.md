# Calidad de código — GlamSpace-Back

Sprint 4 · Responsable: Adrian Adonai García Jiménez

## 1. Comparativa de alternativas

Se investigaron cuatro herramientas de análisis de código con opciones gratuitas para repositorios
públicos y soporte de C# (el lenguaje del backend). Datos consultados el 22/10/2026 en la
documentación oficial de cada herramienta (ver [Fuentes](#fuentes-de-la-comparativa)).

| | **SonarQube Cloud** | **Codacy** | **DeepSource** | **GitHub CodeQL** |
|---|---|---|---|---|
| **Qué es** | Plataforma de calidad y seguridad de código de SonarSource (antes SonarCloud). | Plataforma de revisión automática que agrupa varias herramientas de análisis de terceros. | Plataforma de análisis estático con correcciones automáticas (*Autofix*). | Motor de análisis de seguridad de GitHub (*code scanning*). |
| **Características** | Bugs, vulnerabilidades, code smells, *security hotspots*, duplicación, deuda técnica, cobertura (importada) y **Quality Gate**. | Análisis estático, detección de secretos, vulnerabilidades en dependencias, duplicación, complejidad, cobertura y *quality gates*. | Análisis estático, *Autofix*, detección de secretos, cobertura y reportes de calidad. | Análisis semántico de **seguridad**: el código se convierte en una base de datos y se consulta para encontrar vulnerabilidades. |
| **Lenguajes** | 30+, incluidos **C#**, JavaScript/TypeScript, T-SQL, IaC y GitHub Actions. | 40+, incluido **C#** (con SonarC#, Roslyn, PMD CPD y Lizard). | Go, Rust, Java, Scala, **C#**, JavaScript, PHP, Python, Ruby, C/C++, Swift, Kotlin, SQL, Docker, Terraform y otros. | C/C++, **C#**, Go, Java/Kotlin, JavaScript/TypeScript, Python, Ruby y Swift (Rust en vista previa). |
| **Métricas** | Bugs, vulnerabilidades, code smells, hotspots, % duplicación, deuda técnica (min), calificaciones A–E de confiabilidad/seguridad/mantenibilidad, cobertura, LOC. | Hallazgos por categoría, complejidad, % duplicación, cobertura y calificación general del repositorio. | Hallazgos por categoría (riesgo de bug, seguridad, rendimiento, anti-patrones, estilo) y cobertura. | Solo alertas de seguridad con su severidad. **No** mide code smells, duplicación, deuda técnica ni cobertura. |
| **Integración con GitHub** | GitHub App + GitHub Actions (scanner de .NET). Comenta en los PR y publica el Quality Gate como *check*. | GitHub App con análisis en su nube (sin pipeline) o CLI en Actions. Comenta en los PR. | GitHub App + archivo `.deepsource.toml`. Comenta en los PR y puede abrir PR con correcciones. | **Nativa**: se activa en *Settings → Code security* (configuración por defecto) o con un workflow. Resultados en la pestaña *Security*. |
| **Límites gratuitos** | Plan **Free**: proyectos privados hasta 50k LOC, públicos sin límite; solo analiza la rama principal y los PR hacia ella. Plan **OSS**: repositorios públicos ilimitados con análisis de ramas y PR. | Plan para *open source* gratuito en repositorios públicos. Ignora archivos de más de 150 KB y suspende el análisis de cuentas gratuitas inactivas. | Plan *Open Source* gratuito solo para repositorios públicos (los planes se reestructuraron en 2026). | Gratis en repositorios públicos. En privados requiere GitHub Code Security (de pago). |
| **Ventajas** | Analizador de C# propio que corre durante la compilación (más preciso). Cubre calidad **y** seguridad en una sola herramienta. Quality Gate configurable. Estándar en la industria. | Se activa sin modificar el pipeline. Combina muchas herramientas y lenguajes. Fácil de empezar. | *Autofix* propone correcciones. Configuración simple con un archivo. Pocos falsos positivos. | Sin cuentas externas. Análisis de seguridad muy profundo. Sin costo en repos públicos. |
| **Desventajas** | Requiere Java 21 en el CI. En el plan Free el análisis de ramas está limitado a la rama principal. Hay que desactivar *Automatic Analysis* para usar Actions. | Para C# depende de herramientas de terceros con configuración por separado. Métricas de deuda técnica menos detalladas. | Plan gratuito reciente (menos historial de estabilidad). Las funciones de IA y algunas integraciones son de pago. | **Solo seguridad**: no sirve para medir mantenibilidad, duplicación ni deuda técnica, que pide la práctica. |

### Resumen contra las necesidades del proyecto

| Necesidad | SonarQube Cloud | Codacy | DeepSource | GitHub CodeQL |
|---|---|---|---|---|
| Análisis profundo de C# / .NET | ✅ Analizador propio durante el build | ⚠️ Herramientas de terceros | ✅ | ✅ (solo seguridad) |
| Bugs + vulnerabilidades + code smells | ✅ | ✅ | ✅ | ❌ Solo vulnerabilidades |
| Duplicación y deuda técnica | ✅ Ambas | ⚠️ Duplicación; deuda menos detallada | ⚠️ Parcial | ❌ |
| Quality Gate (aprobado / no aprobado) | ✅ | ✅ | ⚠️ Limitado | ❌ |
| Gratis para un repositorio público | ✅ | ✅ | ✅ | ✅ |
| Análisis automático con GitHub Actions y comentarios en PR | ✅ | ✅ | ✅ | ✅ |

**Conclusión:** CodeQL se descarta porque solo analiza seguridad. Entre las tres restantes,
**SonarQube Cloud** es la única que cubre con detalle todas las métricas que pide la práctica
(incluida la deuda técnica), analiza C# durante la compilación y tiene un Quality Gate completo.
Su justificación técnica está en la sección 2.

### Fuentes de la comparativa

- SonarQube Cloud: [Subscription plans](https://docs.sonarsource.com/sonarqube-cloud/administering-sonarcloud/managing-subscription/subscription-plans), [Branch analysis](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/branch-analysis/branch-analysis), [Requisitos del scanner (Java 21)](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/scanners/scanner-environment/general-requirements)
- Codacy: [Supported languages and tools](https://docs.codacy.com/getting-started/supported-languages-and-tools/), [Organization overview (planes open source)](https://docs.codacy.com/organizations/reporting/organization-overview)
- DeepSource: [Core analyzers](https://docs.deepsource.com/docs/platform/reference/core-analyzers); planes: [reseña de precios 2026](https://aicodereview.cc/blog/deepsource-pricing) (fuente de terceros)
- GitHub CodeQL: [About code scanning with CodeQL](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-code-scanning)

## 2. Herramienta elegida: SonarQube Cloud

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

## 3. Instalación y configuración

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

## 4. Cómo ejecutar el análisis

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

## 5. Análisis inicial

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

## 6. Hallazgos corregidos

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

## 7. Análisis final

Análisis de todo el código después de las correcciones.

- **Fecha:** 22/10/2026, 23:38 UTC
- **Rama:** `Sprint4_Adrian`
- **Commit analizado:** `2d2aefe`
- **Quality Gate:** ❌ **No aprobado**, por la condición de **cobertura de pruebas** (ver abajo). Todas las demás condiciones están en OK.
- **Hallazgos:** los 2 hallazgos del análisis inicial aparecen como **FIXED / CLOSED** en SonarQube Cloud (`/api/issues/search?resolved=true`). Quedan **0 hallazgos abiertos**.

### Comparativa antes / después

| Métrica | Clave Sonar | Inicial (`0feb322`) | Final (`2d2aefe`) | Diferencia |
|---|---|---|---|---|
| Bugs | `bugs` | 0 | 0 | = |
| Vulnerabilidades | `vulnerabilities` | 1 | **0** | **−1** |
| Code smells | `code_smells` | 1 | **0** | **−1** |
| Security hotspots | `security_hotspots` | 0 | 0 | = |
| Duplicación | `duplicated_lines_density` | 0.0 % | 0.0 % | = |
| Deuda técnica | `sqale_index` | 5 min | **0 min** | **−5 min** |
| Calificación de confiabilidad | `reliability_rating` | A | A | = |
| Calificación de seguridad | `security_rating` | **C** | **A** | **C → A** |
| Calificación de mantenibilidad | `sqale_rating` | A | A | = |
| Líneas de código | `ncloc` | 1,197 | 1,273 | +76 |
| Quality Gate | — | ✅ Aprobado | ❌ No aprobado (cobertura) | — |

El aumento de líneas (+76) viene de 75 líneas del script de pruebas de la HU-11 (`Database/05_PruebasHU11.sql`, que Sonar cuenta como T-SQL) y 1 línea neta de `Program.cs` por el cambio de CORS.

### Condiciones del Quality Gate (Sonar way, sobre el código nuevo)

| Condición | Umbral | Resultado | Estado |
|---|---|---|---|
| Calificación de confiabilidad | A | A | ✅ |
| Calificación de seguridad | A | A | ✅ |
| Calificación de mantenibilidad | A | A | ✅ |
| Duplicación | ≤ 3 % | 0.0 % | ✅ |
| Security hotspots revisados | 100 % | 100 % | ✅ |
| **Cobertura de pruebas** | **≥ 80 %** | **0.0 %** | ❌ |

**¿Por qué el Quality Gate pasó de aprobado a no aprobado si se corrigieron todos los hallazgos?**
El Quality Gate solo evalúa el **código nuevo** desde la línea base del 22/10/2026 16:54. En el
análisis inicial, el código nuevo eran solo el workflow y la documentación, que no cuentan para
cobertura. Al corregir los hallazgos se modificaron líneas de C# en `Program.cs`, y esas líneas
nuevas exigen **80 % de cobertura de pruebas**. El proyecto **todavía no tiene pruebas
automatizadas** ni genera un reporte de cobertura, así que el resultado es 0 %.

No es un hallazgo de código y no se ocultó: no se usaron exclusiones de cobertura ni se cambió el
Quality Gate. La forma correcta de aprobarlo es **agregar pruebas automatizadas** (por ejemplo, un
proyecto `GlamSpaces.Tests` con xUnit y `WebApplicationFactory`) y publicar su cobertura en el
análisis (`dotnet test` con *coverlet* + `sonar.cs.opencover.reportsPaths`). Queda como siguiente paso.

El análisis del Pull Request #23 muestra el mismo resultado: todas las condiciones en OK excepto la cobertura.

<!-- CAPTURA: resumen del análisis final en sonarcloud.io (Overview) -->

<!-- CAPTURA: comentario de SonarQube Cloud en el Pull Request -->
