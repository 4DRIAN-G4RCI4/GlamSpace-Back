namespace GlamSpaces.Tests.Domain;

// HU-07 (fotos): reglas de la subida y el borrado con un almacenamiento falso (sin Azure) y el
// repositorio como doble (sin base de datos).
public class FotosSalonServicioPruebas
{
    private readonly IFotoSalonRepositorio _repo = Substitute.For<IFotoSalonRepositorio>();
    private readonly AlmacenamientoFalso _almacen = new();
    private FotosSalonServicio Servicio => new(_repo, _almacen);

    private static MemoryStream Archivo(byte[] contenido) => new(contenido);

    private static RespuestaApi<FotoSalonResponse> Registrada(int id, string url) =>
        RespuestaApi<FotoSalonResponse>.Ok(new FotoSalonResponse { FotoId = id, Url = url }, "Foto subida correctamente.");

    private static RespuestaApi<FotoSalonResponse> ErrorDelSp(int codigo) =>
        RespuestaApi<FotoSalonResponse>.Error(codigo, "Error del SP.");

    [Fact]
    public async Task Subir_JpgValido_LoGuardaEnLaCarpetaDelSalonConGuidYRegistraLaUrl()
    {
        // Arreglar
        _repo.Agregar(12, 3, Arg.Any<string>()).Returns(c => Registrada(40, c.ArgAt<string>(2)));

        // Actuar
        var r = await Servicio.Subir(12, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length);

        // Afirmar
        Assert.True(r.Exito);
        Assert.Equal(40, r.Datos!.FotoId);
        var (ruta, archivo) = Assert.Single(_almacen.Archivos);
        Assert.Matches(@"^12/[0-9a-f]{32}\.jpg$", ruta);
        Assert.Equal("image/jpeg", archivo.ContentType);
        Assert.Equal(FormatoImagenPruebas.Jpg, archivo.Contenido);
        Assert.Equal(AlmacenamientoFalso.Base + ruta, r.Datos.Url);
        await _repo.Received(1).Agregar(12, 3, AlmacenamientoFalso.Base + ruta);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("webp", "image/webp")]
    public async Task Subir_PngYWebp_UsanSuExtensionYContentTypeReales(string extension, string contentType)
    {
        // Arreglar
        var bytes = extension == "png" ? FormatoImagenPruebas.Png : FormatoImagenPruebas.Webp;
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(c => Registrada(1, c.ArgAt<string>(2)));

        // Actuar
        var r = await Servicio.Subir(5, 3, Archivo(bytes), bytes.Length);

        // Afirmar
        Assert.True(r.Exito);
        var (ruta, archivo) = Assert.Single(_almacen.Archivos);
        Assert.EndsWith("." + extension, ruta);
        Assert.Equal(contentType, archivo.ContentType);
    }

    [Fact]
    public async Task Subir_SinArchivoOVacio_Regresa2005SinSubirNada()
    {
        Assert.Equal(CodigosError.ArchivoRequerido, (await Servicio.Subir(1, 3, null, 0)).Codigo);
        Assert.Equal(CodigosError.ArchivoRequerido, (await Servicio.Subir(1, 3, Archivo([]), 0)).Codigo);
        // El cliente dice que trae bytes, pero el archivo llega vacío.
        Assert.Equal(CodigosError.ArchivoRequerido, (await Servicio.Subir(1, 3, Archivo([]), 10)).Codigo);
        Assert.Empty(_almacen.Archivos);
        await _repo.DidNotReceiveWithAnyArgs().Agregar(default, default, default!);
    }

    [Fact]
    public async Task Subir_MasDe5Mb_Regresa2007SinSubirNada()
    {
        // Arreglar: 5 MB + 1 byte con cabecera de JPG
        var grande = new byte[FotosSalonServicio.TamanoMaximo + 1];
        FormatoImagenPruebas.Jpg.CopyTo(grande, 0);

        // Actuar: por el tamaño declarado y por el tamaño real (si el declarado mintiera)
        var porDeclarado = await Servicio.Subir(1, 3, Archivo(grande), grande.Length);
        var porReal = await Servicio.Subir(1, 3, Archivo(grande), 100);

        // Afirmar
        Assert.Equal(CodigosError.FotoMuyPesada, porDeclarado.Codigo);
        Assert.Equal(CodigosError.FotoMuyPesada, porReal.Codigo);
        Assert.Empty(_almacen.Archivos);
    }

    [Fact]
    public async Task Subir_Exactamente5Mb_SeAcepta()
    {
        // Arreglar
        var justo = new byte[FotosSalonServicio.TamanoMaximo];
        FormatoImagenPruebas.Png.CopyTo(justo, 0);
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(c => Registrada(1, c.ArgAt<string>(2)));

        // Actuar / Afirmar
        Assert.True((await Servicio.Subir(1, 3, Archivo(justo), justo.Length)).Exito);
    }

    [Fact]
    public async Task Subir_ArchivoFalsoConExtensionJpg_Regresa2006SinSubirNada()
    {
        // Arreglar: un .txt renombrado a .jpg (la extensión no importa: se revisa el contenido)
        var falso = "esto no es una imagen"u8.ToArray();

        // Actuar
        var r = await Servicio.Subir(1, 3, Archivo(falso), falso.Length);

        // Afirmar
        Assert.Equal(CodigosError.FormatoFotoInvalido, r.Codigo);
        Assert.Empty(_almacen.Archivos);
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado)]
    [InlineData(CodigosError.NoEsDueno)]
    [InlineData(CodigosError.LimiteDeFotos)]
    public async Task Subir_SiElSpRechaza_BorraElArchivoSubidoYRegresaElCodigo(int codigo)
    {
        // Arreglar
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(ErrorDelSp(codigo));

        // Actuar
        var r = await Servicio.Subir(1, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length);

        // Afirmar: sin huérfanos
        Assert.Equal(codigo, r.Codigo);
        Assert.Empty(_almacen.Archivos);
        Assert.Single(_almacen.Eliminadas);
    }

    [Fact]
    public async Task Subir_SiLaBdNoResponde_BorraElArchivoYDejaPasarElError()
    {
        // Arreglar
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).ThrowsAsync(new TimeoutException("BD"));

        // Actuar / Afirmar: el error sigue su camino (la API responde 5000) y no queda archivo huérfano.
        await Assert.ThrowsAsync<TimeoutException>(() => Servicio.Subir(1, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length));
        Assert.Empty(_almacen.Archivos);
    }

    [Fact]
    public async Task Subir_SiFallaElAlmacenamiento_Regresa5000GenericoSinRegistrar()
    {
        // Arreglar
        _almacen.FallarAlSubir = true;

        // Actuar
        var r = await Servicio.Subir(1, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length);

        // Afirmar
        Assert.Equal(CodigosError.ErrorInterno, r.Codigo);
        Assert.DoesNotContain("Azure", r.Mensaje);
        await _repo.DidNotReceiveWithAnyArgs().Agregar(default, default, default!);
    }

    [Fact]
    public async Task Subir_SiElSpRechazaYTampocoSePuedeBorrar_RegresaElErrorDelSp()
    {
        // Arreglar
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(ErrorDelSp(CodigosError.NoEsDueno));
        _almacen.FallarAlEliminar = true;

        // Actuar
        var r = await Servicio.Subir(1, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length);

        // Afirmar
        Assert.Equal(CodigosError.NoEsDueno, r.Codigo);
    }

    [Fact]
    public async Task Eliminar_ValidaBorraElArchivoYLuegoElRegistro()
    {
        // Arreglar
        var request = new EliminarFotoRequest { SalonId = 1, FotoId = 7, AdminId = 3 };
        var url = AlmacenamientoFalso.Base + "1/a.jpg";
        _repo.Validar(request).Returns(Registrada(7, url));
        _repo.Eliminar(request).Returns(RespuestaApi<FotoSalonResponse>.Ok(new FotoSalonResponse { FotoId = 7, Url = url }, "Foto eliminada correctamente."));

        // Actuar
        var r = await Servicio.Eliminar(request);

        // Afirmar
        Assert.True(r.Exito);
        Assert.Equal([url], _almacen.Eliminadas);
        Received.InOrder(() =>
        {
            _repo.Validar(request);
            _repo.Eliminar(request);
        });
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado)]
    [InlineData(CodigosError.NoEsDueno)]
    [InlineData(CodigosError.FotoNoEncontrada)]
    public async Task Eliminar_SiNoPasaLaValidacion_NoBorraNada(int codigo)
    {
        // Arreglar
        _repo.Validar(Arg.Any<EliminarFotoRequest>()).Returns(ErrorDelSp(codigo));

        // Actuar
        var r = await Servicio.Eliminar(new EliminarFotoRequest { SalonId = 1, FotoId = 99, AdminId = 3 });

        // Afirmar
        Assert.Equal(codigo, r.Codigo);
        Assert.Empty(_almacen.Eliminadas);
        await _repo.DidNotReceive().Eliminar(Arg.Any<EliminarFotoRequest>());
    }

    [Fact]
    public async Task Eliminar_SiFallaElAlmacenamiento_Regresa5000YConservaElRegistro()
    {
        // Arreglar
        _repo.Validar(Arg.Any<EliminarFotoRequest>()).Returns(Registrada(7, AlmacenamientoFalso.Base + "1/a.jpg"));
        _almacen.FallarAlEliminar = true;

        // Actuar
        var r = await Servicio.Eliminar(new EliminarFotoRequest { SalonId = 1, FotoId = 7, AdminId = 3 });

        // Afirmar: se puede reintentar
        Assert.Equal(CodigosError.ErrorInterno, r.Codigo);
        await _repo.DidNotReceive().Eliminar(Arg.Any<EliminarFotoRequest>());
    }

    [Fact]
    public async Task EliminarFotosDeSalon_BorraSoloLaCarpetaDeEseSalon()
    {
        // Arreglar
        _repo.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(c => Registrada(1, c.ArgAt<string>(2)));
        await Servicio.Subir(4, 3, Archivo(FormatoImagenPruebas.Jpg), FormatoImagenPruebas.Jpg.Length);
        await Servicio.Subir(4, 3, Archivo(FormatoImagenPruebas.Png), FormatoImagenPruebas.Png.Length);
        await Servicio.Subir(40, 3, Archivo(FormatoImagenPruebas.Webp), FormatoImagenPruebas.Webp.Length);

        // Actuar
        await Servicio.EliminarFotosDeSalon(4);

        // Afirmar: la carpeta "40/" no se toca aunque empiece con "4"
        Assert.Equal([4], _almacen.CarpetasEliminadas);
        Assert.StartsWith("40/", Assert.Single(_almacen.Archivos).Key);
    }
}
