using GlamSpaces.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Base de datos: Azure SQL (SQL Server en la nube).
// La cadena de conexión NO va en appsettings.json (tiene contraseña):
//  - En tu compu: dotnet user-secrets
//  - En Azure App Service: Configuración > Cadenas de conexión > GlamSpacesDb
builder.Services.AddDbContext<GlamSpacesContext>(opciones =>
    opciones.UseSqlServer(
        builder.Configuration.GetConnectionString("GlamSpacesDb"),
        sql => sql.EnableRetryOnFailure() // reintenta si Azure tarda en responder
    )
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS abierto para el frontend (Expo web corre en otro puerto).
// Para producción habría que restringir esto a los dominios reales.
builder.Services.AddCors(opciones =>
{
    opciones.AddPolicy("FrontendDev", politica =>
    {
        politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Crea la base de datos y las tablas automáticamente si no existen
// (equivalente a una migración inicial, útil mientras se arranca el proyecto).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GlamSpacesContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendDev");
app.UseAuthorization();
app.MapControllers();

app.Run();
