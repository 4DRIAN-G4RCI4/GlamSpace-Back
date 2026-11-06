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
}
