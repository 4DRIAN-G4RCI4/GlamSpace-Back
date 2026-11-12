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
        Assert.Equal("https://x/7/a.jpg", Assert.Single(r.Datos!).FotoPrincipal);
        Assert.Empty(r.Datos[0].FotosPreview);
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
}
