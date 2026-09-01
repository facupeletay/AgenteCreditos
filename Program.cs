using Microsoft.EntityFrameworkCore;
using RiesgoWebEmpresarial.Data;
using RiesgoWebEmpresarial.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuracion: appsettings.json + appsettings.{Environment}.json + User Secrets (en Development)
// + variables de entorno. Todo esto ya lo cablea WebApplication.CreateBuilder.
// La API key NUNCA se hardcodea: se lee de OpenAI:ApiKey (ver appsettings.json / README).

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Servicios de negocio.
// Singleton porque el storage es en memoria y AnalisisService dispara trabajo en background;
// no se puede inyectar Scoped dentro de un Singleton. En produccion (con EF Core) pasarian a Scoped.
builder.Services.AddSingleton<IInstructivoService, InstructivoService>();
builder.Services.AddSingleton<PdfExtractorService>();
builder.Services.AddSingleton<OpenAiRiesgoService>();
builder.Services.AddSingleton<IAnalisisService, AnalisisService>();

// Base de datos de la web SgrPlus (EF Core / SQL Server).
// Se registra solo si hay connection string configurada, asi la app sigue
// levantando sin base (dev sin SQL local, pipeline, etc.). La conexion es lazy:
// no se abre hasta la primera consulta.
var sgrPlusCs = builder.Configuration.GetConnectionString("SgrPlusWeb");
if (!string.IsNullOrWhiteSpace(sgrPlusCs))
{
    builder.Services.AddDbContext<SgrPlusDbContext>(opt => opt.UseSqlServer(sgrPlusCs));
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
