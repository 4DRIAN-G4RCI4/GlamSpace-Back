namespace GlamSpaces.Tests.Infrastructure;

// HU-07 (fotos): el detalle del salón lee los 3 result sets de sp_Salon_Obtener (salón, paquetes,
// fotos) y regresa cada foto con su id en fotosDetalle. Usa ConexionFalsa (Dapper real, sin BD).
public class SalonRepositorioPruebas
{
    [Fact]
    public async Task Obtener_RegresaFotosYFotosDetalleConElIdDeCadaFoto()
    {
        // Arreglar
        var salon = ConexionFalsa.Tabla(("Id", 12), ("AdminId", 3), ("Nombre", "Jardín Encanto"), ("Zona", "Tula"),
            ("Capacidad", 150), ("Descripcion", "Con jardín"), ("Estado", "publicado"), ("FechaCreacion", new DateTime(2026, 10, 2)));
        var paquetes = ConexionFalsa.Tabla(("Id", 9), ("SalonId", 12), ("NombrePaquete", "Básico"), ("Descripcion", "x"), ("Precio", 8500m));
        var fotos = ConexionFalsa.Tabla(
        [
            [("Id", 40), ("SalonId", 12), ("Url", "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg")],
            [("Id", 41), ("SalonId", 12), ("Url", "https://cuenta.blob.core.windows.net/fotos-salones/12/b.png")],
        ]);
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", salon, paquetes, fotos);

        // Actuar
        var r = await new SalonRepositorio(conexion).Obtener(12);

        // Afirmar
        Assert.Equal("sp_Salon_Obtener", conexion.ComandoEjecutado);
        Assert.Equal(12, conexion.Parametros["Id"]);
        Assert.True(r.Exito);
        Assert.Equal("Básico", Assert.Single(r.Datos!.Paquetes).NombrePaquete);
        Assert.Equal(2, r.Datos.Fotos.Count);
        Assert.Equal([40, 41], r.Datos.FotosDetalle.Select(f => f.FotoId));
        Assert.Equal(r.Datos.Fotos, r.Datos.FotosDetalle.Select(f => f.Url));
    }

    [Fact]
    public async Task Obtener_SalonInexistente_Regresa2001SinDatos()
    {
        // Arreglar: el SP regresa los 3 result sets con sus columnas pero sin filas
        static DataTable SinFilas(DataTable t) { t.Rows.Clear(); return t; }
        var conexion = new ConexionFalsa().Responder(CodigosError.SalonNoEncontrado, "El salón no existe.",
            SinFilas(ConexionFalsa.Tabla(("Id", 0), ("AdminId", 0), ("Nombre", ""), ("Zona", ""), ("Capacidad", 0), ("Descripcion", ""), ("Estado", ""), ("FechaCreacion", DateTime.MinValue))),
            SinFilas(ConexionFalsa.Tabla(("Id", 0), ("SalonId", 0), ("NombrePaquete", ""), ("Descripcion", ""), ("Precio", 0m))),
            SinFilas(ConexionFalsa.Tabla(("Id", 0), ("SalonId", 0), ("Url", ""))));

        // Actuar
        var r = await new SalonRepositorio(conexion).Obtener(999);

        // Afirmar
        Assert.Equal(CodigosError.SalonNoEncontrado, r.Codigo);
        Assert.Null(r.Datos);
    }

    // HU-07 (fotos): la búsqueda trae hasta 5 fotos por salón en fotosPreview (tercer result set).
    private static DataTable Paginado(int total) =>
        ConexionFalsa.Tabla(("TotalRegistros", total), ("TotalPaginas", 1), ("Pagina", 1), ("TamanoPagina", 10));

    private static DataTable Pagina(params (int Id, string Nombre, string? Foto)[] salones) =>
        ConexionFalsa.Tabla(salones.Select(s => new (string, object?)[]
        {
            ("Id", s.Id), ("Nombre", s.Nombre), ("Zona", "Tula"), ("Capacidad", 100),
            ("PrecioDesde", 5000m), ("FotoPrincipal", s.Foto),
        }));

    [Fact]
    public async Task Buscar_AsignaFotosPreviewACadaSalonEnElOrdenDelSp()
    {
        // Arreglar: dos salones; el 7 con 3 fotos y el 8 con 1 (el SP ya limita a 5 por salón)
        var fotos = ConexionFalsa.Tabla(
        [
            [("SalonId", 7), ("Url", "https://x/7/a.jpg")],
            [("SalonId", 7), ("Url", "https://x/7/b.jpg")],
            [("SalonId", 7), ("Url", "https://x/7/c.jpg")],
            [("SalonId", 8), ("Url", "https://x/8/a.jpg")],
        ]);
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.",
            Paginado(2), Pagina((7, "Jardín", "https://x/7/a.jpg"), (8, "Terraza", "https://x/8/a.jpg")), fotos);

        // Actuar
        var r = await new SalonRepositorio(conexion).Buscar(new BuscarSalonesRequest());

        // Afirmar
        Assert.Equal("sp_Salon_Buscar", conexion.ComandoEjecutado);
        Assert.True(r.Exito);
        Assert.Equal(2, r.TotalRegistros);
        Assert.Equal(["https://x/7/a.jpg", "https://x/7/b.jpg", "https://x/7/c.jpg"], r.Datos![0].FotosPreview);
        Assert.Equal(["https://x/8/a.jpg"], r.Datos[1].FotosPreview);
        Assert.Equal("https://x/7/a.jpg", r.Datos[0].FotoPrincipal);
        Assert.Equal(r.Datos[0].FotoPrincipal, r.Datos[0].FotosPreview[0]);
    }

    [Fact]
    public async Task Buscar_SalonSinFotos_FotosPreviewVaciaYFotoPrincipalNula()
    {
        var fotos = ConexionFalsa.Tabla(("SalonId", 8), ("Url", "https://x/8/a.jpg"));
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.",
            Paginado(2), Pagina((7, "Sin fotos", null), (8, "Con foto", "https://x/8/a.jpg")), fotos);

        var r = await new SalonRepositorio(conexion).Buscar(new BuscarSalonesRequest());

        Assert.Empty(r.Datos![0].FotosPreview);
        Assert.Null(r.Datos[0].FotoPrincipal);
        Assert.Single(r.Datos[1].FotosPreview);
    }

    [Fact]
    public async Task Buscar_ConElSpAnteriorSinTercerResultSet_FotosPreviewVaciaYNadaSeRompe()
    {
        // Arreglar: el SP viejo solo regresa paginado y página (2 result sets)
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.",
            Paginado(1), Pagina((7, "Jardín", "https://x/7/a.jpg")));

        // Actuar
        var r = await new SalonRepositorio(conexion).Buscar(new BuscarSalonesRequest());

        // Afirmar
        Assert.True(r.Exito);
        var salon = Assert.Single(r.Datos!);
        Assert.Equal("https://x/7/a.jpg", salon.FotoPrincipal);
        Assert.Empty(salon.FotosPreview);
    }

    [Fact]
    public async Task Buscar_SinResultados_ListaVaciaYExito()
    {
        var conexion = new ConexionFalsa().Responder(0, "No se encontraron salones con esos filtros.",
            Paginado(0), Vacia(Pagina((1, "x", null))), Vacia(ConexionFalsa.Tabla(("SalonId", 1), ("Url", "x"))));

        var r = await new SalonRepositorio(conexion).Buscar(new BuscarSalonesRequest { CapacidadMinima = 100000 });

        Assert.True(r.Exito);
        Assert.Empty(r.Datos!);
    }

    private static DataTable Vacia(DataTable t) { t.Rows.Clear(); return t; }

    private static DataTable SalonFila(string nombre) => ConexionFalsa.Tabla(("Id", 12), ("AdminId", 3), ("Nombre", nombre),
        ("Zona", "Tula"), ("Capacidad", 150), ("Descripcion", "Con jardín"), ("Estado", "publicado"), ("FechaCreacion", new DateTime(2026, 10, 2)));

    [Fact]
    public async Task Crear_MandaLosDatosAlSpYRegresaElSalon()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Salón creado correctamente.", SalonFila("Jardín Encanto"));
        var request = new SalonRequest { AdminId = 3, Nombre = "Jardín Encanto", Zona = "Tula", Capacidad = 150, Descripcion = "Con jardín" };

        // Actuar
        var r = await new SalonRepositorio(conexion).Crear(request);

        // Afirmar
        Assert.Equal("sp_Salon_Crear", conexion.ComandoEjecutado);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal(150, conexion.Parametros["Capacidad"]);
        Assert.True(r.Exito);
        Assert.Equal(12, r.Datos!.Id);
    }

    [Fact]
    public async Task Crear_NoEsAdministrador_RegresaElCodigoSinDatos()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.NoEsAdministrador, "Solo un administrador puede crear salones.");

        // Actuar
        var r = await new SalonRepositorio(conexion).Crear(new SalonRequest { AdminId = 2, Nombre = "X", Zona = "Tula", Capacidad = 10 });

        // Afirmar
        Assert.Equal(CodigosError.NoEsAdministrador, r.Codigo);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Actualizar_ConExito_RegresaElDetalleCompletoConElMensajeDeActualizar()
    {
        // Arreglar: después del SP de actualizar se lee el detalle (salón, paquetes y fotos).
        var paquetes = ConexionFalsa.Tabla(("Id", 9), ("SalonId", 12), ("NombrePaquete", "Básico"), ("Descripcion", "x"), ("Precio", 8500m));
        var fotos = ConexionFalsa.Tabla(("Id", 40), ("SalonId", 12), ("Url", "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg"));
        var conexion = new ConexionFalsa().Responder(0, "Salón actualizado correctamente.", SalonFila("Jardín Renovado"), paquetes, fotos);
        var request = new ActualizarSalonRequest { Id = 12, AdminId = 3, Nombre = "Jardín Renovado", Zona = "Tula", Capacidad = 150, Estado = "publicado" };

        // Actuar
        var r = await new SalonRepositorio(conexion).Actualizar(request);

        // Afirmar
        Assert.Equal("sp_Salon_Obtener", conexion.ComandoEjecutado); // el último comando es la relectura
        Assert.True(r.Exito);
        Assert.Equal("Jardín Renovado", r.Datos!.Nombre);
        Assert.Single(r.Datos.Paquetes);
        Assert.Equal("Salón actualizado correctamente.", r.Mensaje);
    }

    [Fact]
    public async Task Actualizar_DeOtroAdministrador_RegresaElErrorSinReleerElSalon()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.NoEsDueno, "No eres el dueño de este salón.");

        // Actuar
        var r = await new SalonRepositorio(conexion).Actualizar(new ActualizarSalonRequest { Id = 12, AdminId = 9, Nombre = "X", Zona = "Tula", Capacidad = 10 });

        // Afirmar
        Assert.Equal("sp_Salon_Actualizar", conexion.ComandoEjecutado);
        Assert.Equal(9, conexion.Parametros["AdminId"]);
        Assert.Equal(CodigosError.NoEsDueno, r.Codigo);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Listar_MandaElAdminYLaPaginacionYMapeaLosSalones()
    {
        // Arreglar
        var pagina = ConexionFalsa.Tabla([
            [("Id", 12), ("Nombre", "Jardín Encanto"), ("Zona", "Tula"), ("Capacidad", 150), ("Estado", "publicado"),
             ("TotalPaquetes", 2), ("FotoPrincipal", "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg"), ("TotalFotos", 3)],
            [("Id", 11), ("Nombre", "Terraza"), ("Zona", "Tula"), ("Capacidad", 80), ("Estado", "no_publicado"),
             ("TotalPaquetes", 0), ("FotoPrincipal", null), ("TotalFotos", 0)],
        ]);
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", Paginado(1), pagina);

        // Actuar
        var r = await new SalonRepositorio(conexion).Listar(new ListarSalonesRequest { AdminId = 3, Pagina = 1, TamanoPagina = 10 });

        // Afirmar
        Assert.Equal("sp_Salon_Listar", conexion.ComandoEjecutado);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal(1, r.TotalRegistros);
        Assert.Equal(2, r.Datos![0].TotalPaquetes);
        // Mis salones: miniatura y número de fotos en el mismo result set (sin N+1)
        Assert.EndsWith("/12/a.jpg", r.Datos[0].FotoPrincipal);
        Assert.Equal(3, r.Datos[0].TotalFotos);
        Assert.Null(r.Datos[1].FotoPrincipal);
        Assert.Equal(0, r.Datos[1].TotalFotos);
    }

    [Fact]
    public async Task Eliminar_ConExito_MandaIdYDuenoYRegresaElId()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Salón eliminado correctamente.");

        // Actuar
        var r = await new SalonRepositorio(conexion).Eliminar(new EliminarSalonRequest { Id = 12, AdminId = 3 });

        // Afirmar
        Assert.Equal("sp_Salon_Eliminar", conexion.ComandoEjecutado);
        Assert.Equal(CommandType.StoredProcedure, conexion.TipoDeComando);
        Assert.Equal(12, conexion.Parametros["Id"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.True(r.Exito);
        Assert.Equal(12, r.Datos);
        Assert.Equal("Salón eliminado correctamente.", r.Mensaje);
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado, "El salón no existe.")]
    [InlineData(CodigosError.NoEsDueno, "No eres el dueño de este salón.")]
    [InlineData(CodigosError.SalonConSolicitudesActivas, "Tiene solicitudes pendientes o confirmadas; resuélvelas antes de eliminar el salón.")]
    [InlineData(CodigosError.ErrorInterno, "No se pudo eliminar el salón. Intenta de nuevo más tarde.")]
    public async Task Eliminar_ConErrorDelSp_RegresaElCodigoYElMensaje(int codigo, string mensaje)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new SalonRepositorio(conexion).Eliminar(new EliminarSalonRequest { Id = 12, AdminId = 9 });

        // Afirmar
        Assert.False(r.Exito);
        Assert.Equal(codigo, r.Codigo);
        Assert.Equal(mensaje, r.Mensaje);
    }

    [Fact]
    public async Task Disponibilidad_RegresaLasFechasOcupadasComoTexto()
    {
        // Arreglar
        var fechas = ConexionFalsa.Tabla([
            [("FechaEvento", new DateTime(2026, 12, 5))],
            [("FechaEvento", new DateTime(2026, 12, 19))],
        ]);
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", fechas);

        // Actuar
        var r = await new SalonRepositorio(conexion).Disponibilidad(new DisponibilidadRequest { SalonId = 12, Anio = 2026, Mes = 12 });

        // Afirmar
        Assert.Equal("sp_Salon_Disponibilidad", conexion.ComandoEjecutado);
        Assert.Equal(12, conexion.Parametros["Mes"]);
        Assert.True(r.Exito);
        Assert.Equal(["2026-12-05", "2026-12-19"], r.Datos!.FechasOcupadas);
        Assert.Equal(2026, r.Datos.Anio);
    }

    [Fact]
    public async Task Disponibilidad_SalonInexistente_Regresa2001SinDatos()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.SalonNoEncontrado, "El salón no existe.");

        // Actuar
        var r = await new SalonRepositorio(conexion).Disponibilidad(new DisponibilidadRequest { SalonId = 999, Anio = 2026, Mes = 12 });

        // Afirmar
        Assert.Equal(CodigosError.SalonNoEncontrado, r.Codigo);
        Assert.Null(r.Datos);
    }
}
