namespace GlamSpaces.Tests.Apoyo;

// Levanta la API completa en memoria (TestServer) con Program.cs real, pero con los
// repositorios reemplazados por dobles: ninguna prueba abre una conexión a base de datos.
public sealed class FabricaApi : WebApplicationFactory<Program>
{
    public const string OrigenFrontend = "https://glamspacesfrontsa.z41.web.core.windows.net";

    public IUsuarioRepositorio Usuarios { get; } = Substitute.For<IUsuarioRepositorio>();
    public ISalonRepositorio Salones { get; } = Substitute.For<ISalonRepositorio>();
    public IPaqueteRepositorio Paquetes { get; } = Substitute.For<IPaqueteRepositorio>();
    public ISolicitudRepositorio Solicitudes { get; } = Substitute.For<ISolicitudRepositorio>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas");
        // Sin cadena de conexión: si algo intentara usar la BD, fallaría en lugar de conectarse.
        builder.UseSetting("ConnectionStrings:GlamSpacesDb", "");
        builder.ConfigureTestServices(servicios =>
        {
            servicios.AddScoped(_ => Usuarios);
            servicios.AddScoped(_ => Salones);
            servicios.AddScoped(_ => Paquetes);
            servicios.AddScoped(_ => Solicitudes);
        });
    }
}
