namespace GlamSpaces.Tests.Domain;

// Formato único de respuesta de la API y catálogo de códigos.
public class RespuestaApiPruebas
{
    [Fact]
    public void Ok_ArmaUnaRespuestaExitosaConCodigoCero()
    {
        // Arreglar
        var datos = new IdRequest { Id = 7 };

        // Actuar
        var respuesta = RespuestaApi<IdRequest>.Ok(datos, "Consulta exitosa.");

        // Afirmar
        Assert.Equal(CodigosError.Exito, respuesta.Codigo);
        Assert.Equal(0, respuesta.Codigo);
        Assert.True(respuesta.Exito);
        Assert.Equal("Consulta exitosa.", respuesta.Mensaje);
        Assert.Same(datos, respuesta.Datos);
    }

    [Fact]
    public void Error_ArmaUnaRespuestaFallidaSinDatos()
    {
        // Actuar
        var respuesta = RespuestaApi<IdRequest>.Error(CodigosError.SalonNoEncontrado, "El salón no existe.");

        // Afirmar
        Assert.Equal(2001, respuesta.Codigo);
        Assert.False(respuesta.Exito);
        Assert.Equal("El salón no existe.", respuesta.Mensaje);
        Assert.Null(respuesta.Datos);
    }

    [Theory]
    [InlineData(CodigosError.DatosInvalidos)]
    [InlineData(CodigosError.NoEsDueno)]
    [InlineData(CodigosError.ErrorInterno)]
    public void Exito_EsFalsoParaCualquierCodigoDistintoDeCero(int codigo)
    {
        // Arreglar
        var respuesta = new RespuestaApi<int> { Codigo = codigo };

        // Actuar / Afirmar
        Assert.False(respuesta.Exito);
    }

    [Fact]
    public void CodigosError_TienenLosNumerosDelCatalogoQueUsanLosStoredProcedures()
    {
        // Afirmar: los SP regresan estos mismos números en @Codigo.
        Assert.Equal(0, CodigosError.Exito);
        Assert.Equal(1001, CodigosError.DatosInvalidos);
        Assert.Equal(1002, CodigosError.CorreoDuplicado);
        Assert.Equal(1003, CodigosError.CredencialesIncorrectas);
        Assert.Equal(1004, CodigosError.NoEsAdministrador);
        Assert.Equal(1005, CodigosError.UsuarioNoEncontrado);
        Assert.Equal(2001, CodigosError.SalonNoEncontrado);
        Assert.Equal(2002, CodigosError.NoEsDueno);
        Assert.Equal(2003, CodigosError.SalonSinPaquetes);
        Assert.Equal(2004, CodigosError.EstadoInvalido);
        Assert.Equal(3001, CodigosError.PaqueteNoEncontrado);
        Assert.Equal(3002, CodigosError.UltimoPaquete);
        Assert.Equal(5000, CodigosError.ErrorInterno);
    }

    [Fact]
    public void RespuestaPaginada_GuardaLosDatosDelPaginadoYLaLista()
    {
        // Arreglar / Actuar
        var respuesta = new RespuestaPaginada<string>
        {
            Codigo = CodigosError.Exito,
            Datos = ["Jardín Encanto", "Terraza Monarca"],
            Pagina = 2,
            TamanoPagina = 2,
            TotalRegistros = 5,
            TotalPaginas = 3,
        };

        // Afirmar
        Assert.True(respuesta.Exito);
        Assert.Equal(2, respuesta.Datos!.Count);
        Assert.Equal(2, respuesta.Pagina);
        Assert.Equal(2, respuesta.TamanoPagina);
        Assert.Equal(5, respuesta.TotalRegistros);
        Assert.Equal(3, respuesta.TotalPaginas);
    }
}
