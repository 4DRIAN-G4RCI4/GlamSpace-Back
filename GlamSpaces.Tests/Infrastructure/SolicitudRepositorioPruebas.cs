namespace GlamSpaces.Tests.Infrastructure;

// HU-18: el repositorio llama al stored procedure correcto con los parámetros correctos y traduce
// su @Codigo / result sets. Usa ConexionFalsa (Dapper real, sin base de datos).
public class SolicitudRepositorioPruebas
{
    private static readonly DateTime Fecha = new(2026, 12, 5);

    private static DataTable SolicitudAtendida(string estado, int rechazadas) => ConexionFalsa.Tabla(
        ("Id", 7), ("SalonId", 1), ("NombreSalon", "Jardín Encanto"), ("ClienteId", 2), ("FechaEvento", Fecha),
        ("TipoEvento", "Boda"), ("NumeroInvitados", 120), ("Estado", estado), ("SolicitudesRechazadas", rechazadas));

    [Fact]
    public async Task Aceptar_LlamaCambiarEstadoConConfirmadaYRegresaLasRechazadas()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Solicitud confirmada. Se rechazaron 2 solicitud(es) pendiente(s) de esa fecha.",
            SolicitudAtendida("confirmada", 2));

        // Actuar
        var respuesta = await new SolicitudRepositorio(conexion).Aceptar(new AtenderSolicitudRequest { Id = 7, AdminId = 3 });

        // Afirmar
        Assert.Equal("sp_Solicitud_CambiarEstado", conexion.ComandoEjecutado);
        Assert.Equal(CommandType.StoredProcedure, conexion.TipoDeComando);
        Assert.Equal(7, conexion.Parametros["Id"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal("confirmada", conexion.Parametros["Estado"]);
        Assert.True(respuesta.Exito);
        Assert.Equal("confirmada", respuesta.Datos!.Estado);
        Assert.Equal(2, respuesta.Datos.SolicitudesRechazadas);
        Assert.StartsWith("Solicitud confirmada.", respuesta.Mensaje);
    }

    [Fact]
    public async Task Rechazar_LlamaCambiarEstadoConRechazada()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Solicitud rechazada.", SolicitudAtendida("rechazada", 0));

        // Actuar
        var respuesta = await new SolicitudRepositorio(conexion).Rechazar(new AtenderSolicitudRequest { Id = 7, AdminId = 3 });

        // Afirmar
        Assert.Equal("rechazada", conexion.Parametros["Estado"]);
        Assert.Equal("rechazada", respuesta.Datos!.Estado);
        Assert.Equal(0, respuesta.Datos.SolicitudesRechazadas);
    }

    [Theory]
    [InlineData(CodigosError.NoEsDueno, "No eres el dueño del salón de esta solicitud.")]
    [InlineData(CodigosError.SolicitudYaAtendida, "La solicitud ya fue atendida.")]
    [InlineData(CodigosError.FechaNoDisponible, "La fecha ya no está disponible.")]
    public async Task Aceptar_ConErrorDelSp_RegresaElCodigoSinDatos(int codigo, string mensaje)
    {
        // Arreglar: con error el SP hace RETURN sin result set.
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var respuesta = await new SolicitudRepositorio(conexion).Aceptar(new AtenderSolicitudRequest { Id = 7, AdminId = 9 });

        // Afirmar
        Assert.False(respuesta.Exito);
        Assert.Equal(codigo, respuesta.Codigo);
        Assert.Equal(mensaje, respuesta.Mensaje);
        Assert.Null(respuesta.Datos);
    }

    [Fact]
    public async Task ListarPorSalon_MandaLosFiltrosYMapeaClienteYPrecio()
    {
        // Arreglar: primer result set = paginado, segundo = página.
        var paginado = ConexionFalsa.Tabla(("TotalRegistros", 2), ("TotalPaginas", 1), ("Pagina", 1), ("TamanoPagina", 10));
        var pagina = ConexionFalsa.Tabla([
            [("Id", 8), ("SalonId", 1), ("NombreSalon", "Jardín"), ("ClienteId", 2), ("NombreCliente", "María López"),
             ("PrecioPaquete", 15000m), ("FechaEvento", Fecha), ("Estado", "pendiente")],
            [("Id", 9), ("SalonId", 1), ("NombreSalon", "Jardín"), ("ClienteId", 4), ("NombreCliente", "Roberto Sánchez"),
             ("PrecioPaquete", 8500m), ("FechaEvento", Fecha.AddDays(7)), ("Estado", "pendiente")],
        ]);
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", paginado, pagina);
        var request = new ListarSolicitudesSalonRequest { SalonId = 1, AdminId = 3, Estado = "pendiente", Pagina = 1, TamanoPagina = 10 };

        // Actuar
        var respuesta = await new SolicitudRepositorio(conexion).ListarPorSalon(request);

        // Afirmar
        Assert.Equal("sp_Solicitud_ListarPorSalon", conexion.ComandoEjecutado);
        Assert.Equal(1, conexion.Parametros["SalonId"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal("pendiente", conexion.Parametros["Estado"]);
        Assert.Equal(0, respuesta.Codigo);
        Assert.Equal(2, respuesta.TotalRegistros);
        Assert.Equal(["María López", "Roberto Sánchez"], respuesta.Datos!.Select(s => s.NombreCliente));
        Assert.Equal(15000m, respuesta.Datos![0].PrecioPaquete);
    }

    [Fact]
    public async Task ListarPorSalon_DeOtroAdministrador_RegresaElErrorYListaVacia()
    {
        // Arreglar: con error el SP regresa el paginado en 0 y la página vacía.
        var paginado = ConexionFalsa.Tabla(("TotalRegistros", 0), ("TotalPaginas", 0), ("Pagina", 1), ("TamanoPagina", 10));
        var vacia = new DataTable();
        vacia.Columns.Add("Id", typeof(int));
        var conexion = new ConexionFalsa().Responder(CodigosError.NoEsDueno, "No eres el dueño de este salón.", paginado, vacia);

        // Actuar
        var respuesta = await new SolicitudRepositorio(conexion).ListarPorSalon(new ListarSolicitudesSalonRequest { SalonId = 1, AdminId = 9 });

        // Afirmar
        Assert.Equal(CodigosError.NoEsDueno, respuesta.Codigo);
        Assert.False(respuesta.Exito);
        Assert.Empty(respuesta.Datos!);
        Assert.Null(conexion.Parametros["Estado"]); // sin estado, el SP usa "pendiente"
    }

    private static DataTable SolicitudFila(int id) => ConexionFalsa.Tabla(
        ("Id", id), ("SalonId", 1), ("NombreSalon", "Jardín Encanto"), ("ClienteId", 2), ("PaqueteId", 9),
        ("NombrePaquete", "Básico"), ("FechaEvento", Fecha), ("TipoEvento", "Boda"), ("NumeroInvitados", 120),
        ("TelefonoContacto", "7711234567"), ("Comentarios", "Sin comentarios"), ("Estado", "pendiente"));

    [Fact]
    public async Task Crear_MandaSoloLaFechaDelEventoYRegresaLaSolicitud()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Solicitud enviada.", SolicitudFila(15));
        var request = new CrearSolicitudRequest
        {
            ClienteId = 2, SalonId = 1, PaqueteId = 9, FechaEvento = Fecha.AddHours(18).AddMinutes(30),
            TipoEvento = "Boda", NumeroInvitados = 120, TelefonoContacto = "7711234567", Comentarios = "Sin comentarios",
        };

        // Actuar
        var r = await new SolicitudRepositorio(conexion).Crear(request);

        // Afirmar
        Assert.Equal("sp_Solicitud_Crear", conexion.ComandoEjecutado);
        Assert.Equal(Fecha, conexion.Parametros["FechaEvento"]); // sin la hora
        Assert.Equal(2, conexion.Parametros["ClienteId"]);
        Assert.Equal(9, conexion.Parametros["PaqueteId"]);
        Assert.Equal("7711234567", conexion.Parametros["TelefonoContacto"]);
        Assert.True(r.Exito);
        Assert.Equal(15, r.Datos!.Id);
        Assert.Equal("Básico", r.Datos.NombrePaquete);
    }

    [Theory]
    [InlineData(CodigosError.FechaNoDisponible, "La fecha ya no está disponible.")]
    [InlineData(CodigosError.NoEsCliente, "Solo los clientes pueden enviar solicitudes.")]
    [InlineData(CodigosError.SolicitudDuplicada, "Ya enviaste una solicitud para esa fecha.")]
    public async Task Crear_ConErrorDelSp_RegresaElCodigoSinDatos(int codigo, string mensaje)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new SolicitudRepositorio(conexion).Crear(new CrearSolicitudRequest { ClienteId = 2, SalonId = 1, FechaEvento = Fecha });

        // Afirmar
        Assert.Equal(codigo, r.Codigo);
        Assert.Equal(mensaje, r.Mensaje);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Obtener_MandaElIdYElClienteYRegresaLaSolicitud()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", SolicitudFila(15));

        // Actuar
        var r = await new SolicitudRepositorio(conexion).Obtener(new ObtenerSolicitudRequest { Id = 15, ClienteId = 2 });

        // Afirmar
        Assert.Equal("sp_Solicitud_Obtener", conexion.ComandoEjecutado);
        Assert.Equal(15, conexion.Parametros["Id"]);
        Assert.Equal(2, conexion.Parametros["ClienteId"]);
        Assert.Equal("Jardín Encanto", r.Datos!.NombreSalon);
    }

    [Fact]
    public async Task Obtener_SolicitudAjena_Regresa4005SinDatos()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.SolicitudAjena, "La solicitud no es tuya.");

        // Actuar
        var r = await new SolicitudRepositorio(conexion).Obtener(new ObtenerSolicitudRequest { Id = 15, ClienteId = 99 });

        // Afirmar
        Assert.Equal(CodigosError.SolicitudAjena, r.Codigo);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Listar_MandaLaPaginacionYMapeaLaPagina()
    {
        // Arreglar
        var paginado = ConexionFalsa.Tabla(("TotalRegistros", 11), ("TotalPaginas", 2), ("Pagina", 2), ("TamanoPagina", 10));
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", paginado, SolicitudFila(3));

        // Actuar
        var r = await new SolicitudRepositorio(conexion).Listar(new ListarSolicitudesRequest { ClienteId = 2, Pagina = 2, TamanoPagina = 10 });

        // Afirmar
        Assert.Equal("sp_Solicitud_ListarPorCliente", conexion.ComandoEjecutado);
        Assert.Equal(2, conexion.Parametros["ClienteId"]);
        Assert.Equal(2, conexion.Parametros["Pagina"]);
        Assert.Equal(11, r.TotalRegistros);
        Assert.Equal(2, r.TotalPaginas);
        Assert.Equal(3, Assert.Single(r.Datos!).Id);
    }

    [Fact]
    public async Task Listar_Y_ListarPorSalon_MapeanLaFotoPrincipalONull()
    {
        // Arreglar
        static DataTable Paginado() => ConexionFalsa.Tabla(("TotalRegistros", 2), ("TotalPaginas", 1), ("Pagina", 1), ("TamanoPagina", 10));
        static DataTable Pagina() => ConexionFalsa.Tabla([
            [("Id", 8), ("SalonId", 1), ("NombreSalon", "Jardín"), ("Estado", "pendiente"), ("FotoPrincipal", "https://fotos/1/a.jpg")],
            [("Id", 9), ("SalonId", 2), ("NombreSalon", "Terraza"), ("Estado", "pendiente"), ("FotoPrincipal", null)],
        ]);
        var cliente = new ConexionFalsa().Responder(0, "Consulta exitosa.", Paginado(), Pagina());
        var admin = new ConexionFalsa().Responder(0, "Consulta exitosa.", Paginado(), Pagina());

        // Actuar
        var r1 = await new SolicitudRepositorio(cliente).Listar(new ListarSolicitudesRequest { ClienteId = 2 });
        var r2 = await new SolicitudRepositorio(admin).ListarPorSalon(new ListarSolicitudesSalonRequest { SalonId = 1, AdminId = 3 });

        // Afirmar
        Assert.Equal(["https://fotos/1/a.jpg", null], r1.Datos!.Select(s => s.FotoPrincipal));
        Assert.Equal(["https://fotos/1/a.jpg", null], r2.Datos!.Select(s => s.FotoPrincipal));
    }
}
