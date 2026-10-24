namespace GlamSpaces.Tests.Infrastructure;

// Helper de Dapper que agrega @Codigo y @Mensaje (OUTPUT) y arma la RespuestaApi.
// Se prueba con DynamicParameters en memoria: no hace falta base de datos.
public class StoredProcedurePruebas
{
    // Simula lo que el SP deja en los parámetros OUTPUT después de ejecutarse.
    private static DynamicParameters ParametrosConSalida(int codigo, string mensaje)
    {
        var p = new DynamicParameters();
        p.Add("Codigo", codigo, DbType.Int32, ParameterDirection.Output);
        p.Add("Mensaje", mensaje, DbType.String, ParameterDirection.Output, 200);
        return p;
    }

    [Fact]
    public void Parametros_AgregaLosParametrosDeSalidaCodigoYMensaje()
    {
        // Actuar
        var p = StoredProcedure.Parametros(new { Id = 5, Zona = "Tula" });

        // Afirmar: Dapper guarda la entrada como plantilla y la convierte en parámetros al
        // ejecutar el SP; los OUTPUT @Codigo y @Mensaje sí quedan registrados desde ya.
        Assert.Equal(["Codigo", "Mensaje"], p.ParameterNames.Order());
    }

    [Fact]
    public void CodigoYMensaje_LeenLosParametrosDeSalida()
    {
        // Arreglar
        var p = ParametrosConSalida(2002, "Solo el dueño.");

        // Actuar / Afirmar
        Assert.Equal(2002, p.Codigo());
        Assert.Equal("Solo el dueño.", p.Mensaje());
    }

    [Fact]
    public void Respuesta_ConCodigoCero_MapeaLaEntidadAlDto()
    {
        // Arreglar
        var p = ParametrosConSalida(CodigosError.Exito, "Paquete agregado correctamente.");
        var paquete = new Paquete { Id = 9, SalonId = 1, NombrePaquete = "Básico", Precio = 8500m };

        // Actuar
        var respuesta = p.Respuesta(paquete, PaqueteResponse.Desde);

        // Afirmar
        Assert.True(respuesta.Exito);
        Assert.Equal("Paquete agregado correctamente.", respuesta.Mensaje);
        Assert.Equal(9, respuesta.Datos!.Id);
    }

    [Fact]
    public void Respuesta_ConCodigoDeError_RegresaElErrorSinMapear()
    {
        // Arreglar
        var p = ParametrosConSalida(CodigosError.PaqueteNoEncontrado, "El paquete no existe.");
        var mapeoLlamado = false;

        // Actuar
        var respuesta = p.Respuesta<Paquete, PaqueteResponse>(null, x =>
        {
            mapeoLlamado = true;
            return PaqueteResponse.Desde(x);
        });

        // Afirmar
        Assert.False(respuesta.Exito);
        Assert.Equal(3001, respuesta.Codigo);
        Assert.Equal("El paquete no existe.", respuesta.Mensaje);
        Assert.Null(respuesta.Datos);
        Assert.False(mapeoLlamado);
    }
}
