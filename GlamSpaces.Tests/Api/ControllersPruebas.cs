namespace GlamSpaces.Tests.Api;

// Controllers con los repositorios reemplazados por dobles de las interfaces del Domain.
// Cada acción delega en su repositorio y traduce la respuesta a HTTP.
public class ControllersPruebas
{
    private static ObjectResult Resultado<T>(ActionResult<T> accion) => Assert.IsType<ObjectResult>(accion.Result);

    private static RespuestaApi<T> Exito<T>(T datos) => RespuestaApi<T>.Ok(datos, "Operación exitosa.");

    // ---------- Usuarios ----------

    [Fact]
    public async Task Usuarios_Registrar_Exitoso_Regresa200ConElUsuario()
    {
        // Arreglar
        var repo = Substitute.For<IUsuarioRepositorio>();
        var request = new RegistroRequest { NombreCompleto = "Ana", Correo = "ana@x.com", Password = "glam2026", TipoCuenta = "cliente" };
        repo.Registrar(request).Returns(Exito(new UsuarioResponse { Id = 1, Correo = "ana@x.com" }));

        // Actuar
        var resultado = Resultado(await new UsuariosController(repo).Registrar(request));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        Assert.Equal(1, Assert.IsType<RespuestaApi<UsuarioResponse>>(resultado.Value).Datos!.Id);
    }

    [Fact]
    public async Task Usuarios_Registrar_CorreoDuplicado_Regresa409()
    {
        // Arreglar
        var repo = Substitute.For<IUsuarioRepositorio>();
        repo.Registrar(Arg.Any<RegistroRequest>())
            .Returns(RespuestaApi<UsuarioResponse>.Error(CodigosError.CorreoDuplicado, "Ya existe una cuenta con este correo."));

        // Actuar
        var resultado = Resultado(await new UsuariosController(repo).Registrar(new RegistroRequest()));

        // Afirmar
        Assert.Equal(409, resultado.StatusCode);
    }

    [Fact]
    public async Task Usuarios_Login_CredencialesIncorrectas_Regresa401()
    {
        // Arreglar
        var repo = Substitute.For<IUsuarioRepositorio>();
        repo.Login(Arg.Any<LoginRequest>())
            .Returns(RespuestaApi<UsuarioResponse>.Error(CodigosError.CredencialesIncorrectas, "Correo o contraseña incorrectos."));

        // Actuar
        var resultado = Resultado(await new UsuariosController(repo).Login(new LoginRequest { Correo = "a@x.com", Password = "mala" }));

        // Afirmar
        Assert.Equal(401, resultado.StatusCode);
    }

    [Fact]
    public async Task Usuarios_Obtener_PasaElIdAlRepositorio()
    {
        // Arreglar
        var repo = Substitute.For<IUsuarioRepositorio>();
        repo.Obtener(5).Returns(Exito(new UsuarioResponse { Id = 5 }));

        // Actuar
        var resultado = Resultado(await new UsuariosController(repo).Obtener(new IdRequest { Id = 5 }));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        await repo.Received(1).Obtener(5);
    }

    // ---------- Salones ----------

    [Fact]
    public async Task Salones_Crear_Exitoso_Regresa200()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        repo.Crear(Arg.Any<SalonRequest>()).Returns(Exito(new SalonResponse { Id = 1, Estado = "no_publicado" }));

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Crear(new SalonRequest { AdminId = 1, Nombre = "J", Zona = "T", Capacidad = 10 }));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        Assert.Equal("no_publicado", Assert.IsType<RespuestaApi<SalonResponse>>(resultado.Value).Datos!.Estado);
    }

    [Fact]
    public async Task Salones_Obtener_NoExiste_Regresa404()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        repo.Obtener(99).Returns(RespuestaApi<SalonResponse>.Error(CodigosError.SalonNoEncontrado, "El salón no existe."));

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Obtener(new IdRequest { Id = 99 }));

        // Afirmar
        Assert.Equal(404, resultado.StatusCode);
    }

    [Fact]
    public async Task Salones_Actualizar_PublicarSinPaquetes_Regresa400()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        repo.Actualizar(Arg.Any<ActualizarSalonRequest>())
            .Returns(RespuestaApi<SalonResponse>.Error(CodigosError.SalonSinPaquetes, "Para publicar el salón se requiere al menos un paquete."));

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Actualizar(new ActualizarSalonRequest { Id = 1, Estado = "publicado" }));

        // Afirmar
        Assert.Equal(400, resultado.StatusCode);
    }

    [Fact]
    public async Task Salones_Listar_RegresaElPaginado()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        repo.Listar(Arg.Any<ListarSalonesRequest>()).Returns(new RespuestaPaginada<SalonResumenResponse>
        {
            Datos = [new SalonResumenResponse { Id = 1, Nombre = "Jardín" }],
            Pagina = 1,
            TamanoPagina = 10,
            TotalRegistros = 1,
            TotalPaginas = 1,
        });

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Listar(new ListarSalonesRequest()));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        var paginado = Assert.IsType<RespuestaPaginada<SalonResumenResponse>>(resultado.Value);
        Assert.Equal(1, paginado.TotalRegistros);
        Assert.Single(paginado.Datos!);
    }

    [Fact]
    public async Task Salones_Buscar_SinResultados_EsExitoConListaVacia()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        repo.Buscar(Arg.Any<BuscarSalonesRequest>()).Returns(new RespuestaPaginada<SalonBusquedaResponse>
        {
            Datos = [],
            Mensaje = "No se encontraron salones con esos filtros.",
        });

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Buscar(new BuscarSalonesRequest { CapacidadMinima = 100000 }));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        Assert.Empty(Assert.IsType<RespuestaPaginada<SalonBusquedaResponse>>(resultado.Value).Datos!);
    }

    [Fact]
    public async Task Salones_Disponibilidad_RegresaLasFechasOcupadas()
    {
        // Arreglar
        var repo = Substitute.For<ISalonRepositorio>();
        var request = new DisponibilidadRequest { SalonId = 1, Anio = 2026, Mes = 11 };
        repo.Disponibilidad(request).Returns(Exito(new DisponibilidadResponse
        {
            SalonId = 1, Anio = 2026, Mes = 11, FechasOcupadas = ["2026-11-14", "2026-11-21"],
        }));

        // Actuar
        var resultado = Resultado(await new SalonesController(repo).Disponibilidad(request));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        Assert.Equal(["2026-11-14", "2026-11-21"], Assert.IsType<RespuestaApi<DisponibilidadResponse>>(resultado.Value).Datos!.FechasOcupadas);
    }

    // ---------- Paquetes ----------

    [Fact]
    public async Task Paquetes_Crear_EnSalonAjeno_Regresa403()
    {
        // Arreglar
        var repo = Substitute.For<IPaqueteRepositorio>();
        repo.Crear(Arg.Any<CrearPaqueteRequest>())
            .Returns(RespuestaApi<PaqueteResponse>.Error(CodigosError.NoEsDueno, "Solo el administrador dueño del salón puede modificarlo."));

        // Actuar
        var resultado = Resultado(await new PaquetesController(repo).Crear(new CrearPaqueteRequest { SalonId = 1, AdminId = 2 }));

        // Afirmar
        Assert.Equal(403, resultado.StatusCode);
    }

    [Fact]
    public async Task Paquetes_Actualizar_Exitoso_Regresa200()
    {
        // Arreglar
        var repo = Substitute.For<IPaqueteRepositorio>();
        repo.Actualizar(Arg.Any<ActualizarPaqueteRequest>()).Returns(Exito(new PaqueteResponse { Id = 9, Precio = 9000m }));

        // Actuar
        var resultado = Resultado(await new PaquetesController(repo).Actualizar(new ActualizarPaqueteRequest { Id = 9, Precio = 9000m }));

        // Afirmar
        Assert.Equal(200, resultado.StatusCode);
        Assert.Equal(9000m, Assert.IsType<RespuestaApi<PaqueteResponse>>(resultado.Value).Datos!.Precio);
    }

    [Fact]
    public async Task Paquetes_Eliminar_UltimoDeSalonPublicado_Regresa400()
    {
        // Arreglar
        var repo = Substitute.For<IPaqueteRepositorio>();
        repo.Eliminar(Arg.Any<EliminarPaqueteRequest>())
            .Returns(RespuestaApi<int>.Error(CodigosError.UltimoPaquete, "Un salón publicado debe tener al menos un paquete."));

        // Actuar
        var resultado = Resultado(await new PaquetesController(repo).Eliminar(new EliminarPaqueteRequest { Id = 9, AdminId = 1 }));

        // Afirmar
        Assert.Equal(400, resultado.StatusCode);
    }
}
