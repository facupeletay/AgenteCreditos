# Riesgo Web Empresarial

Agente que analiza **riesgo reputacional y legal** de una empresa a partir de un
**PDF (scorecard enriquecido)**, usando la **Responses API de OpenAI con web search**,
con **historial de instructivos versionables** (prompts que se pueden bifurcar).

Todo vive en **un solo proyecto .NET 8 (Blazor Server)**. No hay backend separado:
la lógica de negocio está en `Services/` dentro del mismo proyecto.

---

## Arquitectura

```
RiesgoWebEmpresarial.csproj      Blazor Server, net8.0
Program.cs                       DI + AddRazorPages + AddServerSideBlazor
appsettings.json                 OpenAI:ApiKey (vacía) + OpenAI:Model

Models/
  Instructivo.cs                 prompt versionable (árbol padre -> hijo)
  Analisis.cs                    una corrida (estado, resultado, auditoría)
  Hallazgo.cs                    hallazgo puntual con severidad
  RiesgoRespuestaDto.cs          forma del JSON que devuelve el modelo

Services/
  IInstructivoService / InstructivoService    CRUD + versionado/bifurcación (en memoria, thread-safe)
  IAnalisisService  / AnalisisService         orquesta PDF -> extracción -> OpenAI -> resultado (background con Task.Run)
  PdfExtractorService                         PdfPig: texto + regex de CUIT + heurística de razón social
  OpenAiRiesgoService                         arma el prompt, llama Responses API con web search, deserializa JSON

Pages/
  Index.razor                    seleccionar instructivo + subir PDF + "Analizar" + polling cada 3 s + veredicto
  Historial.razor                tabla de todos los análisis
  Instructivos.razor             tabla de instructivos + crear / bifurcar

Shared/MainLayout.razor          layout simple, acento naranja #e96525
tools/generar-pdf-ejemplo.ps1    genera un PDF de prueba
docs/scorecard-ejemplo.pdf       PDF de prueba ya generado
```

### Paquetes NuGet

| Paquete | Para qué |
|---|---|
| `OpenAI` 2.1.0 | SDK oficial de OpenAI para .NET — Responses API (`OpenAI.Responses`) con web search |
| `UglyToad.PdfPig` 0.1.9 | extracción de texto del PDF |
| `Microsoft.Extensions.Configuration` (+ `.UserSecrets`) 8.0.0 | leer `OpenAI:ApiKey` de appsettings / User Secrets / variables de entorno |

---

## Requisitos

- **.NET SDK 8.0** (esta máquina tiene solo *runtimes*, falta el SDK).
  Instalalo con:

  ```bash
  winget install Microsoft.DotNet.SDK.8
  ```

  Cerrá y reabrí la terminal, y verificá:

  ```bash
  dotnet --version
  ```

---

## Configurar la API key (nunca se hardcodea)

`appsettings.json` deja `OpenAI:ApiKey` vacía a propósito. Cargá la key real por
**una** de estas vías (precedencia: variable de entorno > User Secrets > appsettings):

### Opción A — User Secrets (recomendado en dev)

```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-TU_KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

### Opción B — variable de entorno

PowerShell:

```powershell
$env:OpenAI__ApiKey = "sk-TU_KEY"
$env:OpenAI__Model  = "gpt-4o-mini"
```

bash:

```bash
export OpenAI__ApiKey="sk-TU_KEY"
export OpenAI__Model="gpt-4o-mini"
```

> El doble guion bajo `__` es el separador de secciones para variables de entorno.

---

## Correr local

```bash
dotnet restore
dotnet run
```

Abrí el navegador en la URL que imprime la consola (por defecto
`https://localhost:7080` o `http://localhost:5080`).

---

## Probar el flujo end-to-end

1. **Generá el PDF de prueba** (ya viene uno en `docs/`, pero podés regenerarlo):

   ```powershell
   pwsh ./tools/generar-pdf-ejemplo.ps1
   ```

   Produce `docs/scorecard-ejemplo.pdf` con una empresa ficticia
   (`ACME CONSTRUCCIONES S.A.`, CUIT `30-71234567-9`) y datos de scorecard.

2. **Instructivos** (`/instructivos`): ya hay uno original sembrado
   ("Riesgo reputacional y legal - base"). Opcional: tocá **Bifurcar** para
   crear una v2 editando el prompt del padre; se guarda mostrando de qué ID deriva.

3. **Analizar** (`/`):
   - Elegí el instructivo.
   - Subí `docs/scorecard-ejemplo.pdf`.
   - Escribí tu usuario y tocá **Analizar**.
   - La página hace polling cada 3 s. Vas a ver `Procesando…` y luego el
     **veredicto**: empresa analizada, empresas vinculadas, hallazgos coloreados
     por severidad (alto / medio / bajo) y conclusión ejecutiva.

4. **Historial** (`/historial`): queda la corrida registrada con estado,
   severidad general y cantidad de hallazgos.

> Con una empresa ficticia el modelo normalmente devolverá **sin hallazgos**;
> es el resultado correcto. Para ver hallazgos reales, usá un PDF con una razón
> social y CUIT de una empresa real que tenga antecedentes públicos.

### Si algo falla

- **`Falta la API key`** → no configuraste `OpenAI:ApiKey` (ver arriba).
- **Estado `Error` con mensaje de deserialización** → el modelo no devolvió JSON
  limpio. `OpenAiRiesgoService` ya intenta recortar ```` ```json ```` y texto
  alrededor; si persiste, probá otro modelo (`gpt-4o`) o reforzá el instructivo.
- **`No se pudo extraer texto del PDF`** → el PDF es escaneado (imagen). Usá uno
  con texto real (el de `docs/` lo tiene).

---

## Notas de la Responses API (importante)

La Responses API del SDK oficial (`OpenAI.Responses`) está marcada como
**experimental** (diagnóstico `OPENAI001`, ya suprimido en el `.csproj`).
Toda la integración está aislada en **`Services/OpenAiRiesgoService.cs`**.
Si cambiás la versión del paquete `OpenAI` y cambia la firma de
`CreateResponseAsync` o de `ResponseTool.CreateWebSearchTool()`, ajustá solo ese
archivo. El resto del proyecto no depende del SDK.

---

## Qué falta para producción

Este proyecto es un prototipo funcional. Para llevarlo a producción:

- **Persistencia real**: hoy `InstructivoService` y `AnalisisService` guardan en
  memoria (`ConcurrentDictionary`) y se pierde todo al reiniciar. Migrar a
  **EF Core + SQL Server**: `DbContext`, migraciones, repos; los servicios pasan
  de `Singleton` a `Scoped`.
- **Cola de trabajo**: reemplazar `Task.Run` por un `IHostedService` con cola
  (Channel) o un broker (por ejemplo con reintentos, back-pressure, y que el
  trabajo sobreviva a reinicios).
- **Autenticación / autorización**: hoy el "usuario" es un texto libre. Sumar
  ASP.NET Core Identity o SSO corporativo, y auditar quién corre cada análisis.
- **Almacenamiento de PDFs**: guardar el archivo original (blob storage) y no
  solo su nombre.
- **Manejo de secretos**: Key Vault / Secret Manager en vez de variables de entorno.
- **Extracción de CUIT / razón social**: la heurística actual es mínima; validar
  dígito verificador del CUIT y mejorar el parseo por layout.
- **Observabilidad**: logging estructurado, métricas, trazas de las llamadas a OpenAI
  (tokens, costo, latencia), y guardado de la respuesta cruda para auditoría.
- **Rate limiting / costos**: límites por usuario y por día, y control de gasto.
- **Tests**: unitarios de `PdfExtractorService` y del mapeo de `OpenAiRiesgoService`,
  e integración del flujo con un mock del cliente de OpenAI.
