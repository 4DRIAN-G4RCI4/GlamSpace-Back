namespace GlamSpaces.Tests.Infrastructure;

// HU-07 (fotos): el repositorio llama a sp_FotoSalon_Agregar / sp_FotoSalon_Eliminar con los
// parámetros correctos y traduce su @Codigo. Usa ConexionFalsa (Dapper real, sin base de datos).
public class FotoSalonRepositorioPruebas
{
    private const string Url = "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg";

    private static DataTable Foto() => ConexionFalsa.Tabla(("Id", 40), ("SalonId", 12), ("Url", Url));

    [Fact]
    public async Task Agregar_LlamaAlSpConSalonAdminYUrlYRegresaFotoIdYUrl()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Foto subida correctamente.", Foto());

        // Actuar
        var r = await new FotoSalonRepositorio(conexion).Agregar(12, 3, Url);

        // Afirmar
        Assert.Equal("sp_FotoSalon_Agregar", conexion.ComandoEjecutado);
        Assert.Equal(CommandType.StoredProcedure, conexion.TipoDeComando);
        Assert.Equal(12, conexion.Parametros["SalonId"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal(Url, conexion.Parametros["Url"]);
        Assert.True(r.Exito);
        Assert.Equal(40, r.Datos!.FotoId);
        Assert.Equal(Url, r.Datos.Url);
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado, "El salón no existe.")]
    [InlineData(CodigosError.NoEsDueno, "Solo el administrador dueño del salón puede modificarlo.")]
    [InlineData(CodigosError.LimiteDeFotos, "El salón ya tiene 8 fotos. Elimina una para subir otra.")]
    public async Task Agregar_ConErrorDelSp_RegresaElCodigoSinDatos(int codigo, string mensaje)
    {
        // Arreglar: con error el SP hace RETURN sin result set.
        var conexion = new ConexionFalsa().Responder(codigo, mensaje);

        // Actuar
        var r = await new FotoSalonRepositorio(conexion).Agregar(12, 9, Url);

        // Afirmar
        Assert.Equal(codigo, r.Codigo);
        Assert.Equal(mensaje, r.Mensaje);
        Assert.Null(r.Datos);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidarYEliminar_UsanElMismoSpConSoloValidar(bool soloValidar)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Foto encontrada.", Foto());
        var repo = new FotoSalonRepositorio(conexion);
        var request = new EliminarFotoRequest { SalonId = 12, FotoId = 40, AdminId = 3 };

        // Actuar
        var r = soloValidar ? await repo.Validar(request) : await repo.Eliminar(request);

        // Afirmar
        Assert.Equal("sp_FotoSalon_Eliminar", conexion.ComandoEjecutado);
        Assert.Equal(12, conexion.Parametros["SalonId"]);
        Assert.Equal(40, conexion.Parametros["FotoId"]);
        Assert.Equal(3, conexion.Parametros["AdminId"]);
        Assert.Equal(soloValidar, conexion.Parametros["SoloValidar"]);
        Assert.Equal(Url, r.Datos!.Url);
    }

    [Fact]
    public async Task Eliminar_FotoDeOtroSalon_Regresa2009()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.FotoNoEncontrada, "La foto no existe o no es de este salón.");

        // Actuar
        var r = await new FotoSalonRepositorio(conexion).Eliminar(new EliminarFotoRequest { SalonId = 12, FotoId = 99, AdminId = 3 });

        // Afirmar
        Assert.Equal(CodigosError.FotoNoEncontrada, r.Codigo);
        Assert.Null(r.Datos);
    }
}
