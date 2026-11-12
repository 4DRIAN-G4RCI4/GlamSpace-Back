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
}
