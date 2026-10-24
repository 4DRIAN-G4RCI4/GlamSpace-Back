# Calidad de código con SonarQube Cloud — GlamSpace

**Actividad: Parte 1 (40 pts) · Sprint 4 · Responsable: Adrian Adonai García Jiménez**

Repositorios: `GlamSpace-Back` (ASP.NET Core .NET 9, Clean Architecture) y `GlamSpace-Front` (React Native / Expo, JavaScript).

Este documento reúne en un solo lugar toda la Parte 1 de la actividad: la comparativa de herramientas, la justificación de la elección, la integración con GitHub Actions, el análisis inicial, las correcciones, el segundo análisis y el estado del Quality Gate. Las fuentes son el historial de git del repositorio, el workflow `.github/workflows/sonar.yml` y `docs/CALIDAD.md`, que se conserva como bitácora de trabajo.

---

## 1. Resumen ejecutivo

- Se eligió **SonarQube Cloud** (plan Free, repositorio público), frente a Codacy, DeepSource y GitHub CodeQL.
- Se integró con **GitHub Actions** (`.github/workflows/sonar.yml`), con el token en GitHub Secrets y sin exclusiones del código fuente.
- El análisis inicial del backend reportó **1 vulnerabilidad y 1 code smell** sobre 1,197 líneas (seguridad **C**, deuda técnica 5 min).
- **Se corrigieron los 2 hallazgos** (el 100 % de lo reportado): S5122 (CORS abierto) y S6966 (`app.Run()` bloqueante). El segundo análisis quedó en **0 vulnerabilidades, 0 code smells, seguridad A y deuda 0 min**.
- El **Quality Gate está en rojo únicamente por cobertura de pruebas (0 % frente al 80 % exigido en código nuevo)**: el proyecto aún no tiene proyecto de pruebas. No es un hallazgo de código y no se ocultó nada.

---

## 2. Alternativas y tabla comparativa

*Cubre los puntos 1 y 2 de la actividad: investigar al menos 3 alternativas y elegir una gratuita.*

Se investigaron cuatro herramientas de análisis estático con opción gratuita para repositorios públicos y soporte de C# (el lenguaje del backend). Datos consultados el 22/10/2026 en la documentación oficial de cada herramienta (fuentes al final de la sección).

| | **SonarQube Cloud** | **Codacy** | **DeepSource** | **GitHub CodeQL** |
|---|---|---|---|---|
| **Qué es** | Plataforma de calidad y seguridad de código de SonarSource (antes SonarCloud). | Plataforma de revisión automática que agrupa varias herramientas de análisis de terceros. | Plataforma de análisis estático con correcciones automáticas (*Autofix*). | Motor de análisis de seguridad de GitHub (*code scanning*). |
| **Características** | Bugs, vulnerabilidades, code smells, *security hotspots*, duplicación, deuda técnica, cobertura (importada) y **Quality Gate**. | Análisis estático, detección de secretos, vulnerabilidades en dependencias, duplicación, complejidad, cobertura y *quality gates*. | Análisis estático, *Autofix*, detección de secretos, cobertura y reportes de calidad. | Análisis semántico de **seguridad**: el código se convierte en una base de datos consultable para encontrar vulnerabilidades. |
| **Lenguajes** | 30+, incluidos **C#**, JavaScript/TypeScript, T-SQL, IaC y GitHub Actions. | 40+, incluido **C#** (con SonarC#, Roslyn, PMD CPD y Lizard). | Go, Rust, Java, Scala, **C#**, JavaScript, PHP, Python, Ruby, C/C++, Swift, Kotlin, SQL, Docker, Terraform y otros. | C/C++, **C#**, Go, Java/Kotlin, JavaScript/TypeScript, Python, Ruby y Swift (Rust en vista previa). |
| **Métricas** | Bugs, vulnerabilidades, code smells, hotspots, % duplicación, deuda técnica (min), calificaciones A–E de confiabilidad/seguridad/mantenibilidad, cobertura, LOC. | Hallazgos por categoría, complejidad, % duplicación, cobertura y calificación general del repositorio. | Hallazgos por categoría (riesgo de bug, seguridad, rendimiento, anti-patrones, estilo) y cobertura. | Solo alertas de seguridad con su severidad. **No** mide code smells, duplicación, deuda técnica ni cobertura. |
| **Integración con GitHub** | GitHub App + GitHub Actions (scanner de .NET). Comenta en los PR y publica el Quality Gate como *check*. | GitHub App con análisis en su nube (sin pipeline) o CLI en Actions. Comenta en los PR. | GitHub App + archivo `.deepsource.toml`. Comenta en los PR y puede abrir PR con correcciones. | **Nativa**: se activa en *Settings → Code security* o con un workflow. Resultados en la pestaña *Security*. |
| **Límites gratuitos** | Plan **Free**: privados hasta 50k LOC, públicos sin límite; solo analiza la rama principal y los PR hacia ella. Plan **OSS**: repos públicos ilimitados con análisis de ramas y PR. | Plan *open source* gratuito en repositorios públicos. Ignora archivos de más de 150 KB y suspende el análisis de cuentas gratuitas inactivas. | Plan *Open Source* gratuito solo para repositorios públicos (planes reestructurados en 2026). | Gratis en repositorios públicos. En privados requiere GitHub Code Security (de pago). |
| **Ventajas** | Analizador de C# propio que corre durante la compilación (más preciso). Cubre calidad **y** seguridad en una sola herramienta. Quality Gate configurable. Estándar en la industria. | Se activa sin modificar el pipeline. Combina muchas herramientas y lenguajes. Fácil de empezar. | *Autofix* propone correcciones. Configuración simple con un archivo. Pocos falsos positivos. | Sin cuentas externas. Análisis de seguridad muy profundo. Sin costo en repos públicos. |
| **Desventajas** | Requiere Java 21 en el CI. En el plan Free el análisis de ramas se limita a la rama principal. Hay que desactivar *Automatic Analysis* para usar Actions. | Para C# depende de herramientas de terceros con configuración por separado. Métricas de deuda técnica menos detalladas. | Plan gratuito reciente (menos historial de estabilidad). Las funciones de IA y algunas integraciones son de pago. | **Solo seguridad**: no mide mantenibilidad, duplicación ni deuda técnica, que sí pide la actividad. |

### Resumen contra las necesidades del proyecto

| Necesidad | SonarQube Cloud | Codacy | DeepSource | GitHub CodeQL |
|---|---|---|---|---|
| Análisis profundo de C# / .NET | ✅ Analizador propio durante el build | ⚠️ Herramientas de terceros | ✅ | ✅ (solo seguridad) |
| Bugs + vulnerabilidades + code smells | ✅ | ✅ | ✅ | ❌ Solo vulnerabilidades |
| Duplicación y deuda técnica | ✅ Ambas | ⚠️ Duplicación; deuda menos detallada | ⚠️ Parcial | ❌ |
| Quality Gate (aprobado / no aprobado) | ✅ | ✅ | ⚠️ Limitado | ❌ |
| Gratis para un repositorio público | ✅ | ✅ | ✅ | ✅ |
| Análisis automático con GitHub Actions y comentarios en PR | ✅ | ✅ | ✅ | ✅ |

**Conclusión:** CodeQL se descarta porque solo analiza seguridad. Entre las tres restantes, SonarQube Cloud es la única que cubre con detalle todas las métricas que pide la actividad (incluida la deuda técnica), analiza C# durante la compilación y tiene un Quality Gate completo.

### Fuentes de la comparativa

- SonarQube Cloud: [Subscription plans](https://docs.sonarsource.com/sonarqube-cloud/administering-sonarcloud/managing-subscription/subscription-plans), [Branch analysis](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/branch-analysis/branch-analysis), [Requisitos del scanner (Java 21)](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/scanners/scanner-environment/general-requirements)
- Codacy: [Supported languages and tools](https://docs.codacy.com/getting-started/supported-languages-and-tools/), [Organization overview](https://docs.codacy.com/organizations/reporting/organization-overview)
- DeepSource: [Core analyzers](https://docs.deepsource.com/docs/platform/reference/core-analyzers); planes: [reseña de precios 2026](https://aicodereview.cc/blog/deepsource-pricing) (fuente de terceros)
- GitHub CodeQL: [About code scanning with CodeQL](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-code-scanning)

---

## 3. Justificación técnica de la elección

*Cubre el punto 2 de la actividad.*

**SonarQube Cloud** (antes SonarCloud) es el servicio en la nube de SonarSource para análisis estático de código. Se eligió por lo siguiente:

| Criterio | Qué aporta al proyecto |
|---|---|
| **Bugs** | Detecta errores de lógica que pueden fallar en ejecución (referencias nulas, condiciones siempre verdaderas, recursos sin liberar). |
| **Vulnerabilidades** | Encuentra problemas de seguridad (inyección, datos sensibles, criptografía débil) y los *security hotspots* que requieren revisión manual. |
| **Code smells** | Señala código difícil de mantener: métodos largos, complejidad alta, nombres confusos, código muerto. |
| **Duplicación** | Mide el porcentaje de líneas duplicadas para detectar copiar y pegar. |
| **Deuda técnica** | Estima el tiempo que costaría corregir todos los code smells y lo expresa como calificación A–E. |
| **Quality Gate** | Define una condición de aprobado/reprobado por análisis y por PR (sin bugs nuevos, cobertura mínima, duplicación < 3 %). |
| **Integración con GitHub Actions** | Corre con cada push y cada PR, sin servidores propios. |
| **Comentarios en los PR** | Publica en cada Pull Request el resultado del Quality Gate y los hallazgos del código nuevo. |
| **Costo** | Gratis para repositorios públicos. |
| **Soporte de C# / .NET** | Analizador nativo de C# (reglas `csharpsquid:S...`) integrado con `dotnet build`, lo que da más precisión que un analizador de texto. |

Razón de fondo: cubre en una sola herramienta bugs, seguridad, mantenibilidad y duplicación, en lugar de combinar varios analizadores, y encaja con el flujo de ramas y PRs (`Sprint4_Adrian → develop → release → main`) que ya usa el equipo.

---

## 4. Integración con GitHub Actions

*Cubre los puntos 3 y 7 de la actividad: integración con GitHub Actions y documentación de instalación, configuración y ejecución.*

### 4.1 El workflow, comentado

Archivo: `.github/workflows/sonar.yml` (agregado en el commit `28a42a5`, 22/10/2026).

```yaml
name: SonarQube Cloud
```
Nombre que aparece en la pestaña *Actions* de GitHub.

```yaml
on:
  push:
    branches:
      - develop
      - main
      - Sprint4_Adrian
  pull_request:
    types: [opened, synchronize, reopened]
```
**Disparadores.** Se analiza en cada push a `develop`, `main` y `Sprint4_Adrian`, y en cada Pull Request cuando se abre, se actualiza o se reabre. `Sprint4_Adrian` se agregó en el commit `b118468` porque es la rama principal del proyecto en SonarQube Cloud: cada push ahí genera un análisis completo de todo el código, no solo el del PR (ver sección 5).

```yaml
permissions:
  contents: read
  pull-requests: read
```
**Permisos mínimos** para el token del workflow: solo lectura del código y de los PR. Los comentarios en los PR los publica la GitHub App de SonarQube Cloud, no este workflow.

```yaml
jobs:
  sonar:
    name: Análisis SonarQube Cloud
    runs-on: ubuntu-latest
```
Un solo job en un runner Ubuntu alojado por GitHub.

```yaml
      - name: Checkout
        uses: actions/checkout@v6
        with:
          fetch-depth: 0
```
**Historial completo.** Sin `fetch-depth: 0` el clon es superficial y Sonar no puede calcular bien el *blame* ni distinguir qué es código nuevo, que es justamente lo que evalúa el Quality Gate.

```yaml
      - name: Instalar .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.x'
```
SDK de .NET 9, la versión del backend.

```yaml
      - name: Instalar Java 21
        uses: actions/setup-java@v4
        with:
          distribution: temurin
          java-version: '21'
```
**El scanner de Sonar corre sobre la JVM.** SonarQube Cloud retiró el soporte de Java 17 para los scanners el 20/07/2026 y ahora exige Java 21 o mayor (ver sección 5).

```yaml
      - name: Instalar dotnet-sonarscanner
        run: |
          dotnet tool install --global dotnet-sonarscanner
          echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"
```
Instala el scanner como herramienta global de .NET y agrega su carpeta al `PATH` para los pasos siguientes.

```yaml
      - name: Iniciar análisis
        run: >
          dotnet sonarscanner begin
          /k:"4DRIAN-G4RCI4_GlamSpace-Back"
          /o:"4drian-g4rci4"
          /d:sonar.token="${{ secrets.SONAR_TOKEN }}"
          /d:sonar.host.url="https://sonarcloud.io"
          /d:sonar.exclusions="**/bin/**,**/obj/**"
```
- `/k`: clave del proyecto en SonarQube Cloud (corregida en el commit `0feb322`).
- `/o`: organización `4drian-g4rci4`. El frontend tiene su propio proyecto dentro de la misma organización.
- `/d:sonar.token`: se lee del **secret de GitHub**; nunca está escrito en el repositorio.
- `/d:sonar.exclusions`: **solo `bin/` y `obj/`**, que son artefactos de compilación. No se excluye ningún archivo de código fuente, no se usa `//NOSONAR` y no se marcó nada como falso positivo.

```yaml
      - name: Compilar
        run: dotnet build GlamSpaces.sln
```
El scanner de C# **observa la compilación**: por eso `begin` va antes del build y `end` después. Sin este build no habría análisis de C#.

```yaml
      - name: Terminar análisis y subir resultados
        run: dotnet sonarscanner end /d:sonar.token="${{ secrets.SONAR_TOKEN }}"
```
Cierra el análisis, sube los resultados a sonarcloud.io y evalúa el Quality Gate.

### 4.2 Manejo del secreto

- El token vive solo en **GitHub → Settings → Secrets and variables → Actions**, con el nombre `SONAR_TOKEN`.
- En los logs de Actions aparece enmascarado.
- No hay ningún token en el repositorio ni en `appsettings.json`.

### 4.3 Cómo reproducirlo desde cero (instalación, configuración y ejecución)

1. **Crear el proyecto en SonarQube Cloud.** Entrar a https://sonarcloud.io con la cuenta de GitHub → *+* → *Analyze new project* → organización `4drian-g4rci4` → repositorio `GlamSpace-Back`. Verificar que la clave sea `4DRIAN-G4RCI4_GlamSpace-Back`.
2. **Generar el token.** *My Account* → *Security* → *Generate Tokens*. Se muestra una sola vez.
3. **Guardar el token en GitHub Secrets** como `SONAR_TOKEN` (nunca en el repositorio).
4. **Desactivar *Automatic Analysis*.** En el proyecto: *Administration* → *Analysis Method* → desactivar *Automatic Analysis*. Es obligatorio: el análisis automático y el de GitHub Actions no pueden convivir, y para .NET el correcto es el de Actions porque analiza mientras compila.
5. **Agregar el workflow** `.github/workflows/sonar.yml` y hacer push.

**Requisitos del entorno de análisis:** .NET SDK 9, **Java 21 o mayor**, y `dotnet-sonarscanner`.

**Ejecución automática.** Cada push a `develop`, `main` o `Sprint4_Adrian` y cada PR dispara el workflow. El resultado aparece en sonarcloud.io y como *check* en el PR.

**Ejecución local**, con el token en una variable de entorno (nunca escrito en un archivo del repo):

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

> El análisis local también requiere tener *Automatic Analysis* desactivado.

---

## 5. Obstáculos y cómo se resolvieron

| Obstáculo | Qué pasaba | Solución |
|---|---|---|
| **Plan Free y repositorio privado** | En el plan Free, SonarQube Cloud solo analiza la rama principal del proyecto y los PR hacia ella. Con el repositorio privado no se podía analizar la rama de trabajo del sprint. | Se hizo **público** el repositorio, lo que además levanta el límite de líneas del plan Free. |
| **La rama de trabajo no se analizaba completa** | En un PR, Sonar solo analiza el código nuevo del PR, no todo el proyecto. La rama de trabajo era `Sprint4_Adrian`, no `main`. | Se cambió la **rama principal del proyecto en SonarQube Cloud a `Sprint4_Adrian`** y se agregó esa rama al disparador de push del workflow (commit `b118468`). Así cada push a la rama del sprint genera un análisis completo. **Pendiente:** al cerrar la actividad hay que devolver la rama principal a `main` y quitar `Sprint4_Adrian` de los triggers (ver sección 12). |
| **Java 17 ya no sirve** | SonarQube Cloud retiró el soporte de Java 17 para los scanners el 20/07/2026; el scanner fallaba al arrancar. | Se fijó **Java 21 (Temurin)** en el workflow con `actions/setup-java@v4`. |
| **Clave de proyecto incorrecta** | El `begin` apuntaba a una clave que no coincidía con el proyecto de SonarQube Cloud y el análisis no se publicaba donde debía. | Se corrigió `/k:` a `4DRIAN-G4RCI4_GlamSpace-Back` (commit `0feb322`). |
| **Automatic Analysis en conflicto con Actions** | SonarQube Cloud no permite que el análisis automático y el del pipeline convivan en el mismo proyecto. | Se desactivó *Automatic Analysis* en *Administration → Analysis Method* del proyecto del backend. |
| **Token expuesto durante la configuración** | El token de SonarQube se mostró en texto plano en una conversación de chat mientras se configuraba el workflow. | **Pendiente obligatorio:** revocar ese token en *My Account → Security* de sonarcloud.io, generar uno nuevo y actualizar el secret `SONAR_TOKEN` en GitHub. Mientras no se haga, el token debe considerarse comprometido (ver sección 12). |

---

## 6. Análisis inicial

*Cubre el punto 4 de la actividad.*

Primer análisis de todo el código del backend, antes de corregir cualquier hallazgo.

- **Fecha:** 22/10/2026, 19:47 UTC
- **Rama:** `Sprint4_Adrian` (rama principal del proyecto en SonarQube Cloud)
- **Commit analizado:** `0feb322`
- **Proyecto:** [4DRIAN-G4RCI4_GlamSpace-Back](https://sonarcloud.io/dashboard?id=4DRIAN-G4RCI4_GlamSpace-Back&branch=Sprint4_Adrian)
- **Quality Gate:** ✅ **Aprobado** (perfil *Sonar way*; las condiciones se evalúan sobre el código nuevo)
- **Fuente de los datos:** API pública de SonarQube Cloud (`/api/measures/component`, `/api/qualitygates/project_status`, `/api/issues/search`, `/api/hotspots/search`), registrada en `docs/CALIDAD.md` el 22/10/2026.

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
| Confiabilidad | `reliability_rating` | A |
| Seguridad | `security_rating` | **C** |
| Mantenibilidad | `sqale_rating` | A |

### Hallazgos

| # | Severidad | Tipo | Regla | Archivo | Línea | Mensaje | Esfuerzo |
|---|---|---|---|---|---|---|---|
| 1 | Major | Vulnerabilidad | `csharpsquid:S5122` | `GlamSpaces.Api/Program.cs` | 51 | *Restrict this CORS policy to trusted origins.* | 4 h |
| 2 | Major | Code smell | `csharpsquid:S6966` | `GlamSpaces.Api/Program.cs` | 75 | *Await RunAsync instead.* | 5 min |

La calificación de seguridad **C** se debe al hallazgo 1. La deuda técnica (5 min) solo cuenta code smells, por eso no incluye las 4 h estimadas de la vulnerabilidad.

---

## 7. Hallazgos corregidos

*Cubre el punto 5 de la actividad.*

Ambas correcciones se hicieron en la rama `Sprint4_Adrian` y entraron a `develop` con el **Pull Request #23** ("RE_Sprint4: Calidad de código con SonarQube Cloud", merge `30f5ce2`, 22/10/2026 17:56). De ahí pasaron a `release` con el **PR #27** (merge `d939f0c`) y a `main` con el **PR #28** (merge `e8e1594`), el mismo día.

### Hallazgo 1 — `csharpsquid:S5122`: política de CORS abierta a cualquier origen

- **Tipo y severidad:** Vulnerabilidad, Major (seguridad)
- **Archivo y línea:** `GlamSpaces.Api/Program.cs:51`
- **Commit de la corrección:** `f55ddc1` — *"RE_Sprint4: fix S5122 en GlamSpaces.Api/Program.cs"*, 22/10/2026 17:37:10
- **Archivos tocados:** `GlamSpaces.Api/Program.cs`, `GlamSpaces.Api/appsettings.json`

**Antes** (de `git show f55ddc1`):

```csharp
// CORS abierto para el frontend (Expo web corre en otro puerto).
// Para producción habría que restringir esto a los dominios reales.
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("FrontendDev", politica =>
    {
        politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
...
app.UseCors("FrontendDev");
```

**Después:**

```csharp
// CORS: solo los orígenes del frontend pueden llamar a la API desde un navegador.
// La lista vive en appsettings.json (Cors:OrigenesPermitidos); en Azure se puede cambiar
// sin recompilar con las variables Cors__OrigenesPermitidos__0, __1, ...
// (La app nativa no usa CORS; esto solo aplica a navegadores.)
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("Frontend", politica =>
    {
        politica.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod();
    });
});
...
app.UseCors("Frontend");
```

Y en `appsettings.json`:

```json
"Cors": {
  "OrigenesPermitidos": [
    "https://glamspacesfrontsa.z41.web.core.windows.net",
    "http://localhost:8081",
    "http://localhost:19006"
  ]
}
```

**Por qué era un problema.** Con `AllowAnyOrigin()`, **cualquier sitio web** podía llamar a la API desde el navegador de un usuario y leer sus respuestas (datos de salones, usuarios, resultados de login). Eso anula la protección de "mismo origen" del navegador y facilita ataques desde páginas de terceros.

**Cómo quedó.** Se pasó a `WithOrigins(...)` con una lista explícita de orígenes permitidos: el sitio del frontend en Azure Storage y los dos puertos locales de Expo web (8081 y 19006). La lista vive en configuración, así que en Azure se ajusta sin recompilar mediante `Cors__OrigenesPermitidos__0`, `__1`, etc. Se validó con un *preflight* `OPTIONS`: los orígenes de la lista reciben `Access-Control-Allow-Origin` y un origen desconocido no, por lo que el navegador bloquea la llamada.

### Hallazgo 2 — `csharpsquid:S6966`: `app.Run()` bloqueante

- **Tipo y severidad:** Code smell, Major (confiabilidad)
- **Archivo y línea:** `GlamSpaces.Api/Program.cs:75`
- **Commit de la corrección:** `1c5faae` — *"RE_Sprint4: fix S6966 en GlamSpaces.Api/Program.cs"*, 22/10/2026 17:37:29

**Antes / después** (de `git show 1c5faae`):

```diff
 app.MapControllers();
 app.MapHealthChecks("/health");
 
-app.Run();
+// Versión asíncrona: no bloquea el hilo principal mientras la API está en ejecución.
+await app.RunAsync();
```

**Por qué era un problema.** `app.Run()` es la versión **síncrona**: bloquea el hilo principal mientras la API está en ejecución, aunque el archivo ya es asíncrono (`Program.cs` usa *top-level statements* con `await`). Mezclar llamadas bloqueantes con código asíncrono puede producir bloqueos y desperdicia hilos del *thread pool*.

**Cómo quedó.** Se reemplazó por `await app.RunAsync();`. Se verificó que la API arranca y que Swagger responde igual.

### ¿Por qué se corrigieron 2 hallazgos y no 3?

La actividad pide corregir **al menos 3 hallazgos reales o justificar si hay menos**. Esta es la justificación:

El análisis inicial de SonarQube Cloud con el perfil por defecto (*Sonar way*) sobre las 1,197 líneas del backend reportó **solo 2 hallazgos**: 0 bugs, 1 vulnerabilidad, 1 code smell, 0 *security hotspots* y 0 % de duplicación. **Se corrigieron los 2, es decir el 100 % de lo reportado**, y no quedó ningún hallazgo abierto en el backend.

No se agregó un tercero porque no existe otro hallazgo real que corregir, y la actividad prohíbe expresamente ocultar o forzar hallazgos: no se usaron exclusiones del código fuente, ni comentarios `//NOSONAR`, ni se marcó nada como falso positivo o como "won't fix".

El número bajo se explica por decisiones de diseño que ya venían de los sprints anteriores:

- **Arquitectura por capas** (Domain, Infrastructure, Api) con controllers muy cortos: casi no hay lógica que duplicar ni métodos largos.
- **Reglas de negocio en stored procedures**: el C# solo orquesta (llama al SP y arma la respuesta).
- **Contraseñas con PBKDF2** (100,000 iteraciones, salt por usuario) y comparación en tiempo constante (`FixedTimeEquals`): evita los hallazgos típicos de criptografía débil.
- **Consultas parametrizadas** (Dapper con parámetros): no hay SQL concatenado, que es la fuente habitual de vulnerabilidades de inyección.
- **Cadena de conexión fuera del repositorio** (user-secrets en local, configuración de la app en Azure): no hay credenciales en el código.

Para ser transparentes: en el **frontend** sí quedan **2 hallazgos abiertos** (sección 10). Sumando los dos repositorios, el análisis produjo 4 hallazgos reales en total: 2 corregidos en el backend, 1 corregido en el frontend (commit `1763823`) y 2 todavía abiertos en el frontend.

---

## 8. Segundo análisis y comparación

*Cubre el punto 6 de la actividad.*

- **Fecha:** 22/10/2026, 23:38 UTC
- **Rama:** `Sprint4_Adrian`
- **Commit analizado:** `2d2aefe`
- **Quality Gate:** ❌ **No aprobado**, exclusivamente por la condición de cobertura de pruebas (sección 9). Todas las demás condiciones en OK.
- **Hallazgos:** los 2 del análisis inicial aparecen como **FIXED / CLOSED** en SonarQube Cloud (`/api/issues/search?resolved=true`). Quedan **0 hallazgos abiertos** en el backend.

| Métrica | Clave Sonar | Inicial (`0feb322`) | Final (`2d2aefe`) | Diferencia |
|---|---|---|---|---|
| Bugs | `bugs` | 0 | 0 | = |
| Vulnerabilidades | `vulnerabilities` | 1 | **0** | **−1** |
| Code smells | `code_smells` | 1 | **0** | **−1** |
| Security hotspots | `security_hotspots` | 0 | 0 | = |
| Duplicación | `duplicated_lines_density` | 0.0 % | 0.0 % | = |
| Deuda técnica | `sqale_index` | 5 min | **0 min** | **−5 min** |
| Confiabilidad | `reliability_rating` | A | A | = |
| Seguridad | `security_rating` | **C** | **A** | **C → A** |
| Mantenibilidad | `sqale_rating` | A | A | = |
| Líneas de código | `ncloc` | 1,197 | 1,273 | +76 |
| Quality Gate | — | ✅ Aprobado | ❌ No aprobado (cobertura) | — |

El aumento de 76 líneas no viene de las correcciones: son 75 líneas del script de pruebas de la HU-11 (`Database/05_PruebasHU11.sql`, que Sonar cuenta como T-SQL) más 1 línea neta en `Program.cs` por el cambio de CORS.

---

## 9. Estado del Quality Gate y plan para ponerlo en verde

### Condiciones (perfil *Sonar way*, sobre el código nuevo)

| Condición | Umbral | Resultado | Estado |
|---|---|---|---|
| Calificación de confiabilidad | A | A | ✅ |
| Calificación de seguridad | A | A | ✅ |
| Calificación de mantenibilidad | A | A | ✅ |
| Duplicación | ≤ 3 % | 0.0 % | ✅ |
| Security hotspots revisados | 100 % | 100 % | ✅ |
| **Cobertura de pruebas** | **≥ 80 %** | **0.0 %** | ❌ |

### ¿Por qué pasó de aprobado a no aprobado si se corrigieron todos los hallazgos?

El Quality Gate evalúa solo el **código nuevo** desde la línea base del 22/10/2026 16:54. En el análisis inicial, el código nuevo era únicamente el workflow y la documentación, que no cuentan para cobertura, así que la condición no aplicaba. Al corregir los hallazgos se modificaron líneas de **C#** en `Program.cs`, y esas líneas nuevas sí exigen 80 % de cobertura. El proyecto todavía no tiene pruebas automatizadas ni genera reporte de cobertura, de modo que el valor es 0 %.

**No es un hallazgo de código** y no se ocultó: no se usaron exclusiones de cobertura ni se modificó el Quality Gate para bajar el umbral.

### Plan para ponerlo en verde

1. Crear un proyecto de pruebas `GlamSpaces.Tests` con **xUnit** y agregarlo a `GlamSpaces.sln`.
2. Probar la capa de dominio/aplicación con pruebas unitarias y los endpoints con **`WebApplicationFactory`** (pruebas de integración en memoria).
3. Medir cobertura con **coverlet** en formato OpenCover:
   `dotnet test --collect:"XPlat Code Coverage" /p:CoverletOutputFormat=opencover`
4. Pasarle el reporte al scanner en el paso `begin`:
   `/d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"`
5. Agregar el `dotnet test` al workflow **entre** `begin` y `end`, para que la cobertura se suba dentro del mismo análisis.
6. Priorizar las pruebas sobre el código tocado recientemente, porque el umbral aplica al código nuevo.

El análisis del Pull Request #23 muestra el mismo patrón: todas las condiciones en OK excepto la cobertura.

---

## 10. Frontend (`GlamSpace-Front`)

El frontend se analizó en su **propio proyecto** de SonarQube Cloud, dentro de la misma organización `4drian-g4rci4`. Lenguaje analizado: **JavaScript** (React Native / Expo).

**Diferencia de método respecto al backend:** en el repositorio del frontend **no hay** carpeta `.github/workflows/` ni archivo `sonar-project.properties`, así que su análisis no se hace por pipeline sino con el *Automatic Analysis* de SonarQube Cloud. Esto se verificó revisando el repositorio local.

### Hallazgo corregido

| Hallazgo | Autor | Commit | Fecha | Qué se hizo |
|---|---|---|---|---|
| Promesas no esperadas dentro de `useEffect` | Erick Trejo Reséndiz | `1763823` — *"RE_Sprint4: Marca con void las promesas que no se esperan (SonarQube)"* | 22/10/2026 16:43 | Se marcaron con el operador `void` las funciones asíncronas autoejecutadas, para declarar explícitamente que no se espera su resultado. |

Archivos tocados (5): `screens/BusquedaSalones/useBusquedaSalones.js`, `screens/DetalleSalon/useDetalleSalon.js`, `screens/DetalleSalon/useDisponibilidad.js`, `screens/Login/useLogin.js`, `screens/PublicarSalon/usePublicarSalon.js`.

Ejemplo (`screens/Login/useLogin.js`):

```diff
   useEffect(() => {
     // Si ya había una sesión activa, al reabrir la app se mantiene.
-    (async () => {
+    void (async () => {
       const sesionGuardada = await obtenerSesionActiva();
```

### Hallazgos abiertos (2), ambos en `utils/validadores.js`

| # | Regla | Línea | Qué señala | Estado |
|---|---|---|---|---|
| 1 | `S8786` | 6 | La expresión regular del correo (`REGEX_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/`) no es una validación robusta de direcciones de correo. | **Abierto, sin responsable asignado** |
| 2 | `S7773` | 22 | Se usa el global `NaN` en lugar de `Number.NaN`. | **Abierto, sin responsable asignado** |

Las dos líneas señaladas existen tal cual en el archivo actual, lo que confirma que los hallazgos siguen vigentes. No se ocultaron ni se marcaron como falso positivo: quedan registrados aquí como pendientes del equipo del frontend.

---

## 11. Cumplimiento de la rúbrica (Parte 1, 40 pts)

| # | Punto de la actividad | Estado | Evidencia |
|---|---|---|---|
| 1 | Investigar al menos 3 alternativas y elegir una gratuita | ✅ Cumplido | Sección 2: se compararon **4** herramientas (SonarQube Cloud, Codacy, DeepSource, CodeQL) con nombre, características, lenguajes, métricas, integración con GitHub, límites gratuitos, ventajas y desventajas. Fuente en el repo: `docs/CALIDAD.md` §1, commit `85a152f`. |
| 2 | Justificación técnica de la elección | ✅ Cumplido | Sección 3. Fuente: `docs/CALIDAD.md` §2, commit `188a96a`. |
| 3 | Integración con GitHub Actions | ✅ Cumplido | Sección 4 y el archivo `.github/workflows/sonar.yml` (commits `28a42a5`, `b118468`, `0feb322`). Token en el secret `SONAR_TOKEN`. |
| 4 | Análisis inicial con hallazgos y métricas | ✅ Cumplido | Sección 6: 1,197 LOC, 1 vulnerabilidad, 1 code smell, seguridad C, deuda 5 min; commit analizado `0feb322`. Registro en `docs/CALIDAD.md` §5, commit `c6f8e41`. |
| 5 | Corregir al menos 3 hallazgos reales o justificar si hay menos | ⚠️ **Parcial — justificado** | Sonar reportó **solo 2 hallazgos** en el backend y **se corrigieron los 2** (commits `f55ddc1` y `1c5faae`, PR #23). En el frontend se corrigió además 1 hallazgo (commit `1763823`) y quedan 2 abiertos en `utils/validadores.js`. Justificación completa en la sección 7. |
| 6 | Segundo análisis y comparación | ✅ Cumplido | Sección 8: 0 vulnerabilidades, 0 code smells, seguridad A, deuda 0 min; commit analizado `2d2aefe`. Registro en `docs/CALIDAD.md` §7, commit `e27c855`. |
| 7 | Documentar instalación, configuración y ejecución | ✅ Cumplido | Secciones 4.1 a 4.3: workflow comentado línea por línea, manejo del secreto, pasos de alta en sonarcloud.io, requisitos del entorno, y ejecución automática y local. |
| — | No ocultar hallazgos con exclusiones injustificadas | ✅ Cumplido | `sonar.exclusions` solo cubre `**/bin/**` y `**/obj/**` (artefactos de compilación). Sin `//NOSONAR`, sin falsos positivos marcados, sin exclusiones de cobertura, sin cambios al Quality Gate. Verificable en `.github/workflows/sonar.yml`. |

Commits de referencia (repositorio `4DRIAN-G4RCI4/GlamSpace-Back`): `28a42a5`, `b118468`, `0feb322`, `c6f8e41`, `f55ddc1`, `1c5faae`, `2d2aefe`, `e27c855`, `85a152f`, y los merges de los PR #23 (`30f5ce2`), #27 (`d939f0c`) y #28 (`e8e1594`).

---

## 12. Pendientes y riesgos

| # | Pendiente | Por qué importa | Prioridad |
|---|---|---|---|
| 1 | **Revocar el token de SonarQube** que se expuso en texto plano durante la configuración, generar uno nuevo y actualizar el secret `SONAR_TOKEN` en GitHub. | Un token filtrado permite a un tercero publicar análisis falsos o leer la configuración del proyecto. Debe tratarse como comprometido hasta que se revoque. | **Alta** |
| 2 | **Devolver la rama principal del proyecto en SonarQube Cloud a `main`** y quitar `Sprint4_Adrian` del disparador de push en `.github/workflows/sonar.yml`. | `Sprint4_Adrian` fue una medida temporal para que el plan Free analizara la rama del sprint. Dejarla así desalinea el análisis del flujo real de ramas. | Alta (al cerrar la actividad) |
| 3 | **Crear `GlamSpaces.Tests` con xUnit y publicar la cobertura** para poner el Quality Gate en verde (plan en la sección 9). | Es la única condición del Quality Gate que falla. | Alta |
| 4 | **Asignar responsable a los 2 hallazgos abiertos del frontend** (`S8786` y `S7773` en `utils/validadores.js`). | Son los únicos hallazgos reales sin corregir del proyecto. | Media |
| 5 | **Agregar las capturas de pantalla** de la sección 13. | La evidencia visual se pide para la entrega y la presentación. | Media |
| 6 | Considerar si el repositorio debe seguir **público** después de la actividad. | Se hizo público para levantar el límite del plan Free; conviene que sea una decisión consciente y no un efecto secundario. | Media |
| 7 | Decidir si el frontend también se analiza por **GitHub Actions** en lugar de *Automatic Analysis*. | Daría análisis consistente y comentarios en PR en los dos repositorios. | Baja |

---

## 13. Capturas de evidencia

> Insertar aquí las imágenes antes de entregar o presentar.

**CAPTURA AQUÍ — Panel inicial.** Vista *Overview* del proyecto `4DRIAN-G4RCI4_GlamSpace-Back` en sonarcloud.io para el análisis del commit `0feb322`, mostrando 1,197 LOC, 1 vulnerabilidad, 1 code smell, seguridad C y el Quality Gate aprobado.

**CAPTURA AQUÍ — Panel final.** Vista *Overview* del análisis del commit `2d2aefe`, mostrando 0 vulnerabilidades, 0 code smells, seguridad A, deuda 0 min y el Quality Gate no aprobado por cobertura.

**CAPTURA AQUÍ — Lista de issues.** Pestaña *Issues* del análisis inicial con los dos hallazgos (`S5122` en `Program.cs:51` y `S6966` en `Program.cs:75`); opcionalmente, la misma vista con el filtro *resolved* mostrándolos como FIXED.

**CAPTURA AQUÍ — Comentario en un Pull Request.** Comentario de la GitHub App de SonarQube Cloud en el **PR #23** con el resultado del Quality Gate y los hallazgos del código nuevo.

**CAPTURA AQUÍ — Workflow en verde.** Pestaña *Actions* de GitHub con una ejecución exitosa del workflow "SonarQube Cloud": Checkout → .NET 9 → Java 21 → instalar scanner → begin → build → end, todos en verde.

**CAPTURA AQUÍ (opcional) — Frontend.** Panel del proyecto del frontend en sonarcloud.io con los 2 hallazgos abiertos de `utils/validadores.js`.

---

## 14. Anexo: línea de tiempo

Fechas y autores tomados de `git log` (hora local, UTC−06:00).

| Fecha y hora | Repo | Commit | Autor | Qué pasó |
|---|---|---|---|---|
| 22/10/2026 12:07 | Back | `28a42a5` | Adrian García | Se agrega el workflow de SonarQube Cloud. |
| 22/10/2026 12:07 | Back | `188a96a` | Adrian García | Se agrega `docs/CALIDAD.md` con la configuración y la justificación de la herramienta. |
| 22/10/2026 13:45 | Back | `b118468` | Adrian García | Se agrega `Sprint4_Adrian` al disparador de push del workflow (análisis completo de la rama del sprint). |
| 22/10/2026 13:47 | Back | `0feb322` | Adrian García | Se corrige la clave del proyecto de SonarQube Cloud. **Commit del análisis inicial.** |
| 22/10/2026 13:49 | Back | `c6f8e41` | Adrian García | Se registra el análisis inicial en `CALIDAD.md`. |
| 22/10/2026 16:43 | Front | `1763823` | Erick Trejo Reséndiz | Se marcan con `void` las promesas no esperadas en 5 hooks (hallazgo del frontend). |
| 22/10/2026 17:37:10 | Back | `f55ddc1` | Adrian García | **Fix S5122:** CORS con lista de orígenes permitidos desde configuración. |
| 22/10/2026 17:37:29 | Back | `1c5faae` | Adrian García | **Fix S6966:** `await app.RunAsync()` en lugar de `app.Run()`. |
| 22/10/2026 17:37:56 | Back | `2d2aefe` | Adrian García | Se registran los hallazgos corregidos en `CALIDAD.md`. **Commit del análisis final.** |
| 22/10/2026 17:41 | Back | `e27c855` | Adrian García | Se registra el análisis final en `CALIDAD.md`. |
| 22/10/2026 17:56:38 | Back | `85a152f` | Adrian García | Se agrega la comparativa de herramientas a `CALIDAD.md`. |
| 22/10/2026 17:56:57 | Back | `30f5ce2` | Adrian A. García Jiménez | Merge del **PR #23** (`Sprint4_Adrian` → `develop`): "Calidad de código con SonarQube Cloud". |
| 22/10/2026 17:57:13 | Back | `d939f0c` | Adrian A. García Jiménez | Merge del **PR #27** (`develop` → `release`). |
| 22/10/2026 17:57:29 | Back | `e8e1594` | Adrian A. García Jiménez | Merge del **PR #28** (`release` → `main`). |

Como referencia cruzada: el análisis inicial en SonarQube Cloud quedó fechado 22/10/2026 19:47 UTC y el final 23:38 UTC, consistentes con los commits `0feb322` (13:47 local = 19:47 UTC) y `2d2aefe` (17:37 local = 23:37 UTC).

---

## Datos NO VERIFICADOS

Todo lo anterior se reconstruyó del repositorio salvo lo siguiente, que no se pudo comprobar desde la máquina local porque vive en sonarcloud.io o en GitHub y requiere acceso a esas plataformas:

1. **Las métricas de los dos análisis** (1,197 y 1,273 LOC, conteos de hallazgos, calificaciones, deuda técnica, fechas UTC y condiciones del Quality Gate). Están tomadas de `docs/CALIDAD.md`, que las registró desde la API pública de SonarQube Cloud, pero no se consultó la API en esta revisión. Los commits analizados (`0feb322`, `2d2aefe`) sí existen y sus fechas son consistentes.
2. **Que el Quality Gate siga hoy en rojo** y que la única condición en falla siga siendo la cobertura.
3. **El estado del repositorio como público** y la **rama principal del proyecto en SonarQube Cloud** configurada como `Sprint4_Adrian`.
4. **La existencia del secret `SONAR_TOKEN`** en GitHub y que *Automatic Analysis* esté desactivado en el proyecto del backend.
5. **Las ejecuciones del workflow en verde** en la pestaña Actions y los **comentarios de SonarQube Cloud en los PR**.
6. **El nombre y la configuración del proyecto del frontend** en SonarQube Cloud, y sus números de PR. Que el frontend use *Automatic Analysis* es una deducción: su repositorio no tiene workflow ni `sonar-project.properties`.
7. **Las reglas `S8786` y `S7773` del frontend**: las líneas de código que señalan sí existen en `utils/validadores.js`, pero el estado "abierto" y el identificador exacto de cada regla provienen del dictado, no de la plataforma.
8. **La exposición del token en el chat** y, por tanto, la necesidad de revocarlo: proviene del dictado.
9. **El retiro del soporte de Java 17 el 20/07/2026** por parte de SonarQube Cloud: está documentado como comentario en `sonar.yml` y en `docs/CALIDAD.md`, pero no se verificó contra la documentación oficial en esta revisión.
10. **Las métricas y hallazgos del frontend** (líneas de código, calificaciones, Quality Gate): el dictado no las incluyó y no hay registro de ellas en ninguno de los dos repositorios.
