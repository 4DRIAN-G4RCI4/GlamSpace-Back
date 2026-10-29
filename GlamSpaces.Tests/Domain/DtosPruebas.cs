namespace GlamSpaces.Tests.Domain;

// Mapeos de entidades (filas de la BD) a DTOs (lo que sale del API) y valores por defecto.
public class DtosPruebas
{
    [Fact]
    public void UsuarioResponse_Desde_CopiaLosDatosYNuncaIncluyeElHash()
    {
        // Arreglar
        var usuario = new Usuario
        {
            Id = 3,
            NombreCompleto = "Ana Admin",
            Correo = "ana@glamspaces.com",
            PasswordHash = "100000.c2FsdA==.aGFzaA==",
            TipoCuenta = "administrador",
            NombreSalon = "Salón Ana",
            FechaRegistro = new DateTime(2026, 10, 1),
        };

        // Actuar
        var respuesta = UsuarioResponse.Desde(usuario);

        // Afirmar
        Assert.Equal(3, respuesta.Id);
        Assert.Equal("Ana Admin", respuesta.NombreCompleto);
        Assert.Equal("ana@glamspaces.com", respuesta.Correo);
        Assert.Equal("administrador", respuesta.TipoCuenta);
        Assert.Equal("Salón Ana", respuesta.NombreSalon);
        Assert.Equal(new DateTime(2026, 10, 1), respuesta.FechaRegistro);
        Assert.Null(typeof(UsuarioResponse).GetProperty("PasswordHash"));
    }

    [Fact]
    public void PaqueteResponse_Desde_CopiaTodosLosCampos()
    {
        // Arreglar
        var paquete = new Paquete { Id = 9, SalonId = 1, NombrePaquete = "Básico", Descripcion = "6 horas", Precio = 8500m };

        // Actuar
        var respuesta = PaqueteResponse.Desde(paquete);

        // Afirmar
        Assert.Equal(9, respuesta.Id);
        Assert.Equal(1, respuesta.SalonId);
        Assert.Equal("Básico", respuesta.NombrePaquete);
        Assert.Equal("6 horas", respuesta.Descripcion);
        Assert.Equal(8500m, respuesta.Precio);
    }

    [Fact]
    public void SalonResponse_Desde_IncluyePaquetesYSoloLasUrlDeLasFotos()
    {
        // Arreglar
        var salon = new Salon
        {
            Id = 1,
            AdminId = 3,
            Nombre = "Jardín Encanto",
            Zona = "Tula de Allende, Hgo.",
            Capacidad = 150,
            Descripcion = "Con jardín",
            Estado = "publicado",
            FechaCreacion = new DateTime(2026, 10, 2),
            Paquetes = [new Paquete { Id = 9, SalonId = 1, NombrePaquete = "Básico", Precio = 8500m }],
            Fotos = [new FotoSalon { Id = 4, SalonId = 1, Url = "https://x/foto.jpg" }],
        };

        // Actuar
        var respuesta = SalonResponse.Desde(salon);

        // Afirmar
        Assert.Equal(1, respuesta.Id);
        Assert.Equal(3, respuesta.AdminId);
        Assert.Equal("Jardín Encanto", respuesta.Nombre);
        Assert.Equal("Tula de Allende, Hgo.", respuesta.Zona);
        Assert.Equal(150, respuesta.Capacidad);
        Assert.Equal("Con jardín", respuesta.Descripcion);
        Assert.Equal("publicado", respuesta.Estado);
        Assert.Equal(new DateTime(2026, 10, 2), respuesta.FechaCreacion);
        Assert.Equal("Básico", Assert.Single(respuesta.Paquetes).NombrePaquete);
        Assert.Equal("https://x/foto.jpg", Assert.Single(respuesta.Fotos));
    }

    [Fact]
    public void SalonNuevo_NaceNoPublicadoYSinPaquetesNiFotos()
    {
        // Actuar
        var salon = new Salon();

        // Afirmar
        Assert.Equal("no_publicado", salon.Estado);
        Assert.Empty(salon.Paquetes);
        Assert.Empty(salon.Fotos);
    }

    [Fact]
    public void ReservacionNueva_NacePendienteYSinPaquete()
    {
        // Actuar
        var reservacion = new Reservacion { SalonId = 1, ClienteId = 2, FechaEvento = new DateTime(2026, 11, 14) };

        // Afirmar: solo las "confirmada" ocupan la fecha (HU-11); una nueva todavía no la bloquea.
        Assert.Equal("pendiente", reservacion.Estado);
        Assert.Null(reservacion.PaqueteId);
        Assert.Equal(0, reservacion.Id);
        Assert.Equal(1, reservacion.SalonId);
        Assert.Equal(2, reservacion.ClienteId);
        Assert.Equal(new DateTime(2026, 11, 14), reservacion.FechaEvento);
        Assert.True(reservacion.FechaCreacion > DateTime.MinValue);
    }

    // Contrato con el frontend: la API serializa en camelCase (opciones web de System.Text.Json).
    private static readonly System.Text.Json.JsonSerializerOptions OpcionesWeb = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public void SalonBusquedaResponse_SeSerializaConLosNombresQueUsaLaPantallaDeBusqueda()
    {
        // Arreglar
        var salon = new SalonBusquedaResponse
        {
            Id = 1, Nombre = "Jardín", Zona = "Tula", Capacidad = 150, PrecioDesde = 8500m, FotoPrincipal = "https://x/f.jpg",
        };

        // Actuar
        var json = System.Text.Json.JsonSerializer.Serialize(salon, OpcionesWeb);

        // Afirmar
        Assert.Equal(
            "{\"id\":1,\"nombre\":\"Jardín\",\"zona\":\"Tula\",\"capacidad\":150,\"precioDesde\":8500,\"fotoPrincipal\":\"https://x/f.jpg\"}",
            json.Replace("\\u00ED", "í"));
    }

    [Fact]
    public void SalonResumenResponse_SeSerializaConElTotalDePaquetes()
    {
        // Arreglar
        var salon = new SalonResumenResponse { Id = 2, Nombre = "Terraza", Zona = "Tepeji", Capacidad = 80, Estado = "publicado", TotalPaquetes = 3 };

        // Actuar
        var json = System.Text.Json.JsonSerializer.Serialize(salon, OpcionesWeb);

        // Afirmar
        Assert.Equal("{\"id\":2,\"nombre\":\"Terraza\",\"zona\":\"Tepeji\",\"capacidad\":80,\"estado\":\"publicado\",\"totalPaquetes\":3}", json);
    }

    [Fact]
    public void Requests_SeLeenDelJsonEnCamelCaseQueMandaElFrontend()
    {
        // Actuar
        var paquete = System.Text.Json.JsonSerializer.Deserialize<CrearPaqueteRequest>(
            "{\"salonId\":1,\"adminId\":3,\"nombrePaquete\":\"Básico\",\"descripcion\":\"6 horas\",\"precio\":8500}", OpcionesWeb)!;
        var salon = System.Text.Json.JsonSerializer.Deserialize<ActualizarSalonRequest>(
            "{\"id\":1,\"adminId\":3,\"nombre\":\"Jardín\",\"zona\":\"Tula\",\"capacidad\":150,\"descripcion\":\"Con jardín\",\"estado\":\"publicado\"}", OpcionesWeb)!;
        var registro = System.Text.Json.JsonSerializer.Deserialize<RegistroRequest>(
            "{\"nombreCompleto\":\"Ana\",\"correo\":\"ana@x.com\",\"password\":\"glam2026\",\"tipoCuenta\":\"administrador\",\"nombreSalon\":\"Salón Ana\"}", OpcionesWeb)!;

        // Afirmar
        Assert.Equal((1, 3, "Básico", "6 horas", 8500m), (paquete.SalonId, paquete.AdminId, paquete.NombrePaquete, paquete.Descripcion, paquete.Precio));
        Assert.Equal((1, "Con jardín", "publicado"), (salon.Id, salon.Descripcion, salon.Estado));
        Assert.Equal(("administrador", "Salón Ana"), (registro.TipoCuenta, registro.NombreSalon));
    }

    [Fact]
    public void PaginacionRequest_PorDefectoPideLaPrimeraPaginaDeDiez()
    {
        // Actuar
        var listar = new ListarSalonesRequest();
        var buscar = new BuscarSalonesRequest();

        // Afirmar
        Assert.Equal(1, listar.Pagina);
        Assert.Equal(10, listar.TamanoPagina);
        Assert.Null(listar.AdminId);
        Assert.Equal(1, buscar.Pagina);
        Assert.Equal(10, buscar.TamanoPagina);
        Assert.Null(buscar.Zona);
        Assert.Null(buscar.CapacidadMinima);
        Assert.Null(buscar.PrecioMaximo);
    }

    // ---------- Solicitudes (HU-15) ----------

    [Fact]
    public void CrearSolicitudRequest_SeLeeDelJsonEnCamelCase_YElPaqueteYComentariosSonOpcionales()
    {
        // Actuar
        var completa = System.Text.Json.JsonSerializer.Deserialize<CrearSolicitudRequest>(
            "{\"clienteId\":2,\"salonId\":1,\"paqueteId\":9,\"fechaEvento\":\"2026-12-05\",\"tipoEvento\":\"Boda\"," +
            "\"numeroInvitados\":120,\"telefonoContacto\":\"7731234567\",\"comentarios\":\"En el jardín\"}", OpcionesWeb)!;
        var minima = System.Text.Json.JsonSerializer.Deserialize<CrearSolicitudRequest>(
            "{\"clienteId\":2,\"salonId\":1,\"fechaEvento\":\"2026-12-05\",\"tipoEvento\":\"Boda\",\"numeroInvitados\":120,\"telefonoContacto\":\"7731234567\"}", OpcionesWeb)!;

        // Afirmar
        Assert.Equal((2, 1, 9), (completa.ClienteId, completa.SalonId, completa.PaqueteId));
        Assert.Equal(new DateTime(2026, 12, 5), completa.FechaEvento);
        Assert.Equal(("Boda", 120, "7731234567", "En el jardín"),
            (completa.TipoEvento, completa.NumeroInvitados, completa.TelefonoContacto, completa.Comentarios));
        Assert.Null(minima.PaqueteId);
        Assert.Null(minima.Comentarios);
    }

    [Fact]
    public void ObtenerYListarSolicitudes_LeenElClienteDelBody()
    {
        // Actuar
        var obtener = System.Text.Json.JsonSerializer.Deserialize<ObtenerSolicitudRequest>("{\"id\":7,\"clienteId\":2}", OpcionesWeb)!;
        var listar = System.Text.Json.JsonSerializer.Deserialize<ListarSolicitudesRequest>("{\"clienteId\":2}", OpcionesWeb)!;

        // Afirmar: sin pagina ni tamanoPagina, el listado pide la primera página de 10.
        Assert.Equal((7, 2), (obtener.Id, obtener.ClienteId));
        Assert.Equal((2, 1, 10), (listar.ClienteId, listar.Pagina, listar.TamanoPagina));
    }

    [Fact]
    public void SolicitudResponse_SeSerializaConLosNombresQueUsaElFrontend()
    {
        // Arreglar
        var solicitud = new SolicitudResponse
        {
            Id = 7, SalonId = 1, NombreSalon = "Jardin", ClienteId = 2, PaqueteId = 9, NombrePaquete = "Basico",
            FechaEvento = new DateTime(2026, 12, 5), TipoEvento = "Boda", NumeroInvitados = 120,
            TelefonoContacto = "7731234567", Comentarios = null, Estado = "pendiente",
            FechaCreacion = new DateTime(2026, 10, 29, 18, 0, 0),
        };

        // Actuar
        var json = System.Text.Json.JsonSerializer.Serialize(solicitud, OpcionesWeb);

        // Afirmar
        Assert.Equal(
            "{\"id\":7,\"salonId\":1,\"nombreSalon\":\"Jardin\",\"clienteId\":2,\"paqueteId\":9,\"nombrePaquete\":\"Basico\"," +
            "\"fechaEvento\":\"2026-12-05T00:00:00\",\"tipoEvento\":\"Boda\",\"numeroInvitados\":120,\"telefonoContacto\":\"7731234567\"," +
            "\"comentarios\":null,\"estado\":\"pendiente\",\"fechaCreacion\":\"2026-10-29T18:00:00\"}",
            json);
    }
}
