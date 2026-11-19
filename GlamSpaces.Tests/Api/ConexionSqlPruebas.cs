namespace GlamSpaces.Tests.Api;

// La parte de Program.cs que necesita un SQL Server de verdad: la conexión por petición y el
// healthcheck. No usa la BD de producción ni crea datos: solo abre una conexión a "master".
// En CI (sonar.yml) hay un SQL Server en un contenedor y la cadena llega en GLAMSPACES_SQL_PRUEBAS.
// En una compu sin esa variable no hay servidor con qué probar y estas dos pruebas no hacen nada
// (para correrlas en local: GLAMSPACES_SQL_PRUEBAS con una cadena a LocalDB o a un SQL Server propio).
public class ConexionSqlPruebas
{
    private static string? CadenaDePrueba()
    {
        var cadena = Environment.GetEnvironmentVariable("GLAMSPACES_SQL_PRUEBAS");
        return string.IsNullOrWhiteSpace(cadena) ? null : cadena;
    }

    private sealed class FabricaConSql(string cadena) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Pruebas");
            builder.UseSetting("ConnectionStrings:GlamSpacesDb", cadena);
            builder.UseSetting("Almacenamiento:CadenaConexion", "UseDevelopmentStorage=true");
            builder.UseSetting("Almacenamiento:Contenedor", "fotos-pruebas");
        }
    }

    [Fact]
    public async Task Health_ConBaseDeDatos_RespondeHealthy()
    {
        // Arreglar
        var cadena = CadenaDePrueba();
        if (cadena is null) return;
        await using var fabrica = new FabricaConSql(cadena);

        // Actuar
        var respuesta = await fabrica.CreateClient().GetAsync("/health");

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConexionPorPeticion_LlegaAbiertaYEsLaMismaDentroDeLaPeticion()
    {
        // Arreglar
        var cadena = CadenaDePrueba();
        if (cadena is null) return;
        await using var fabrica = new FabricaConSql(cadena);

        // Actuar
        using var alcance = fabrica.Services.CreateScope();
        var primera = alcance.ServiceProvider.GetRequiredService<IDbConnection>();
        var segunda = alcance.ServiceProvider.GetRequiredService<IDbConnection>();

        // Afirmar
        Assert.Equal(ConnectionState.Open, primera.State);
        Assert.Same(primera, segunda);
    }
}

// Sin la sección Cors:OrigenesPermitidos la lista queda vacía: ningún navegador recibe permiso.
// La raíz de contenido es una carpeta vacía, así que no se carga appsettings.json.
public class CorsSinConfiguracionPruebas
{
    private sealed class FabricaSinCors : WebApplicationFactory<Program>
    {
        public string Carpeta { get; } = Directory.CreateTempSubdirectory("glamspaces-sin-config-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Pruebas");
            builder.UseContentRoot(Carpeta);
            builder.UseSetting("ConnectionStrings:GlamSpacesDb", "");
            builder.UseSetting("Almacenamiento:CadenaConexion", "UseDevelopmentStorage=true");
            builder.UseSetting("Almacenamiento:Contenedor", "fotos-pruebas");
        }
    }

    [Fact]
    public async Task Cors_SinOrigenesConfigurados_NadieRecibePermiso()
    {
        // Arreglar
        await using var fabrica = new FabricaSinCors();
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/salones/buscar");
        preflight.Headers.Add("Origin", FabricaApi.OrigenFrontend);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        // Actuar
        var respuesta = await fabrica.CreateClient().SendAsync(preflight);

        // Afirmar
        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
        Directory.Delete(fabrica.Carpeta, recursive: true);
    }
}
