namespace GlamSpaces.Tests.Infrastructure;

// HU-08: crear, actualizar y eliminar paquetes. El repositorio manda los parámetros al SP y traduce
// su @Codigo (dueño, paquete inexistente, último paquete). Usa ConexionFalsa (Dapper real, sin BD).
public class PaqueteRepositorioPruebas
{
    private static DataTable PaqueteFila(int id, string nombre, decimal precio) => ConexionFalsa.Tabla(
        ("Id", id), ("SalonId", 12), ("NombrePaquete", nombre), ("Descripcion", "Incluye mobiliario"), ("Precio", precio));

    [Fact]
    public async Task Crear_MandaLosDatosAlSpYRegresaElPaqueteCreado()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Paquete creado correctamente.", PaqueteFila(9, "Básico", 8500m));
        var request = new CrearPaqueteRequest { SalonId = 12, AdminId = 3, NombrePaquete = "Básico", Descripcion = "Incluye mobiliario", Precio = 8500m };

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Crear(request);

        // Afirmar
        Assert.Equal("sp_Paquete_Crear", conexion.ComandoEjecutado);
        Assert.Equal(CommandType.StoredProcedure, conexion.TipoDeComando);
        Assert.Equal(12, conexion.Parametros["SalonId"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal("Básico", conexion.Parametros["NombrePaquete"]);
        Assert.Equal(8500m, conexion.Parametros["Precio"]);
        Assert.True(r.Exito);
        Assert.Equal(9, r.Datos!.Id);
        Assert.Equal(12, r.Datos.SalonId);
        Assert.Equal("Paquete creado correctamente.", r.Mensaje);
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado, "El salón no existe.")]
    [InlineData(CodigosError.NoEsDueno, "No eres el dueño de este salón.")]
    [InlineData(CodigosError.DatosInvalidos, "El precio debe ser mayor a 0.")]
    public async Task Crear_ConErrorDelSp_RegresaElCodigoSinDatos(int codigo, string mensaje)
    {
        // Arreglar: con error el SP hace RETURN sin result set.
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Crear(new CrearPaqueteRequest { SalonId = 12, AdminId = 9, NombrePaquete = "X", Precio = 1 });

        // Afirmar
        Assert.False(r.Exito);
        Assert.Equal(codigo, r.Codigo);
        Assert.Equal(mensaje, r.Mensaje);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Actualizar_MandaElIdYElDuenoYRegresaElPaqueteActualizado()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Paquete actualizado correctamente.", PaqueteFila(9, "Completo", 15000m));
        var request = new ActualizarPaqueteRequest { Id = 9, AdminId = 3, NombrePaquete = "Completo", Descripcion = null, Precio = 15000m };

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Actualizar(request);

        // Afirmar
        Assert.Equal("sp_Paquete_Actualizar", conexion.ComandoEjecutado);
        Assert.Equal(9, conexion.Parametros["Id"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Null(conexion.Parametros["Descripcion"]);
        Assert.True(r.Exito);
        Assert.Equal("Completo", r.Datos!.NombrePaquete);
        Assert.Equal(15000m, r.Datos.Precio);
    }

    [Theory]
    [InlineData(CodigosError.PaqueteNoEncontrado, "El paquete no existe.")]
    [InlineData(CodigosError.NoEsDueno, "No eres el dueño de este salón.")]
    public async Task Actualizar_ConErrorDelSp_RegresaElCodigoSinDatos(int codigo, string mensaje)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Actualizar(new ActualizarPaqueteRequest { Id = 999, AdminId = 9, NombrePaquete = "X", Precio = 1 });

        // Afirmar
        Assert.Equal(codigo, r.Codigo);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Eliminar_ConExito_RegresaElIdDelPaquete()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Paquete eliminado correctamente.");

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Eliminar(new EliminarPaqueteRequest { Id = 9, AdminId = 3 });

        // Afirmar
        Assert.Equal("sp_Paquete_Eliminar", conexion.ComandoEjecutado);
        Assert.Equal(9, conexion.Parametros["Id"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.True(r.Exito);
        Assert.Equal(9, r.Datos);
        Assert.Equal("Paquete eliminado correctamente.", r.Mensaje);
    }

    [Theory]
    [InlineData(CodigosError.PaqueteNoEncontrado, "El paquete no existe.")]
    [InlineData(CodigosError.NoEsDueno, "No eres el dueño de este salón.")]
    [InlineData(CodigosError.UltimoPaquete, "El salón debe tener al menos un paquete.")]
    public async Task Eliminar_ConErrorDelSp_RegresaElCodigo(int codigo, string mensaje)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new PaqueteRepositorio(conexion).Eliminar(new EliminarPaqueteRequest { Id = 9, AdminId = 9 });

        // Afirmar
        Assert.False(r.Exito);
        Assert.Equal(codigo, r.Codigo);
        Assert.Equal(mensaje, r.Mensaje);
    }
}
