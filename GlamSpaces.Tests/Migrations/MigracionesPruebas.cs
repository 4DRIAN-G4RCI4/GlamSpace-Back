namespace GlamSpaces.Tests.Migrations;

// Las migraciones se prueban SIN base de datos: se recolectan las expresiones que declara
// cada Up()/Down() y se genera el SQL que FluentMigrator mandaría a SQL Server 2016.
public class MigracionesPruebas
{
    private static List<IMigrationExpression> ExpresionesUp(IMigration migracion)
    {
        var contexto = new MigrationContext(Substitute.For<IQuerySchema>(), new ServiceCollection().BuildServiceProvider(), null!);
        migracion.GetUpExpressions(contexto);
        return contexto.Expressions.ToList();
    }

    private static List<IMigrationExpression> ExpresionesDown(IMigration migracion)
    {
        var contexto = new MigrationContext(Substitute.For<IQuerySchema>(), new ServiceCollection().BuildServiceProvider(), null!);
        migracion.GetDownExpressions(contexto);
        return contexto.Expressions.ToList();
    }

    private static readonly SqlServer2016Generator Generador = new();

    [Theory]
    [InlineData(typeof(M001_LineaBase), 202610230900)]
    [InlineData(typeof(M002_CrearTablaBusquedasFavoritas), 202610230910)]
    [InlineData(typeof(M003_AgregarColumnaTelefonoSalones), 202610230920)]
    [InlineData(typeof(M004_IndiceUnicoBusquedasFavoritas), 202610230930)]
    [InlineData(typeof(M005_CrearSolicitudes), 202610291000)]
    [InlineData(typeof(M006_AgregarTelefonoContactoSolicitudes), 202610291100)]
    [InlineData(typeof(M007_ProcedimientosSolicitudes), 202610291200)]
    public void CadaMigracion_TieneSuNumeroDeVersionYDescripcion(Type tipo, long version)
    {
        // Actuar
        var atributo = (MigrationAttribute)Attribute.GetCustomAttribute(tipo, typeof(MigrationAttribute))!;

        // Afirmar
        Assert.Equal(version, atributo.Version);
        Assert.False(string.IsNullOrWhiteSpace(atributo.Description));
    }

    [Fact]
    public void M001_Up_CreaUsuariosComoEfYLuegoCargaLosScriptsDeDatabase()
    {
        // Actuar
        var sentencias = ExpresionesUp(new M001_LineaBase())
            .Cast<ExecuteSqlStatementExpression>()
            .Select(e => e.SqlStatement)
            .ToList();

        // Afirmar: primero Usuarios sin DEFAULT y con IX_Usuarios_Correo (como la creó EF).
        Assert.Contains("CREATE TABLE dbo.Usuarios", sentencias[0]);
        Assert.Contains("CREATE UNIQUE INDEX IX_Usuarios_Correo", sentencias[0]);
        Assert.DoesNotContain("DEFAULT", sentencias[0]);

        // Después, los lotes de 01_Tablas, 02_StoredProcedures y 03_Indices, uno por uno.
        var lotesEsperados = ScriptsSql.Lotes("Database.01_Tablas.sql").Count
                           + ScriptsSql.Lotes("Database.02_StoredProcedures.sql").Count
                           + ScriptsSql.Lotes("Database.03_Indices.sql").Count;
        Assert.Equal(1 + lotesEsperados, sentencias.Count);
        Assert.Equal(12, sentencias.Count(s => s.Contains("CREATE OR ALTER PROCEDURE")));
        Assert.Contains(sentencias, s => s.Contains("UX_Reservaciones_Confirmada"));
        Assert.DoesNotContain(sentencias, s => s.Contains("\nGO"));
    }

    [Fact]
    public void M001_Down_EstaVacioParaNoBorrarLaLineaBase()
    {
        // Actuar / Afirmar
        Assert.Empty(ExpresionesDown(new M001_LineaBase()));
    }

    [Fact]
    public void M002_Up_CreaLaTablaConSusLlavesForaneasYFechaPorDefecto()
    {
        // Actuar
        var expresiones = ExpresionesUp(new M002_CrearTablaBusquedasFavoritas());
        var tabla = Assert.Single(expresiones.OfType<CreateTableExpression>());
        var sql = Generador.Generate(tabla);

        // Afirmar
        Assert.Equal("BusquedasFavoritas", tabla.TableName);
        Assert.Equal(["Id", "UsuarioId", "SalonId", "FechaCreacion"], tabla.Columns.Select(c => c.Name));
        Assert.Contains("CREATE TABLE [dbo].[BusquedasFavoritas]", sql);
        Assert.Contains("IDENTITY(1,1)", sql);
        Assert.Contains("DEFAULT GETDATE()", sql);
        Assert.Contains("CONSTRAINT [PK_BusquedasFavoritas] PRIMARY KEY", sql);

        var llaves = tabla.Columns.Where(c => c.ForeignKey != null).Select(c => c.ForeignKey!).ToList();
        Assert.Contains(llaves, f => f.Name == "FK_BusquedasFavoritas_Usuarios" && f.PrimaryTable == "Usuarios");
        Assert.Contains(llaves, f => f.Name == "FK_BusquedasFavoritas_Salones" && f.PrimaryTable == "Salones");
    }

    [Fact]
    public void M002_Down_BorraLaTabla()
    {
        // Actuar
        var borrar = Assert.Single(ExpresionesDown(new M002_CrearTablaBusquedasFavoritas()).OfType<DeleteTableExpression>());

        // Afirmar
        Assert.Equal("BusquedasFavoritas", borrar.TableName);
        Assert.Contains("DROP TABLE [dbo].[BusquedasFavoritas]", Generador.Generate(borrar));
    }

    [Fact]
    public void M003_Up_AgregaTelefonoComoNvarchar20Nulo()
    {
        // Actuar
        var columna = Assert.Single(ExpresionesUp(new M003_AgregarColumnaTelefonoSalones()).OfType<CreateColumnExpression>());
        var sql = Generador.Generate(columna);

        // Afirmar
        Assert.Equal("Salones", columna.TableName);
        Assert.Equal("Telefono", columna.Column.Name);
        Assert.Contains("ALTER TABLE [dbo].[Salones] ADD [Telefono] NVARCHAR(20)", sql);
        Assert.DoesNotContain("NOT NULL", sql);
    }

    [Fact]
    public void M003_Down_QuitaLaColumna()
    {
        // Actuar
        var borrar = Assert.Single(ExpresionesDown(new M003_AgregarColumnaTelefonoSalones()).OfType<DeleteColumnExpression>());

        // Afirmar
        Assert.Equal("Salones", borrar.TableName);
        Assert.Equal("Telefono", Assert.Single(borrar.ColumnNames));
    }

    [Fact]
    public void M004_Up_CreaUnIndiceUnicoSobreUsuarioYSalon()
    {
        // Actuar
        var indice = Assert.Single(ExpresionesUp(new M004_IndiceUnicoBusquedasFavoritas()).OfType<CreateIndexExpression>());
        var sql = Generador.Generate(indice);

        // Afirmar
        Assert.Equal("UX_BusquedasFavoritas_Usuario_Salon", indice.Index.Name);
        Assert.True(indice.Index.IsUnique);
        Assert.Equal(["UsuarioId", "SalonId"], indice.Index.Columns.Select(c => c.Name));
        Assert.Contains("CREATE UNIQUE", sql);
        Assert.Contains("[BusquedasFavoritas] ([UsuarioId] ASC, [SalonId] ASC)", sql);
    }

    [Fact]
    public void M004_Down_QuitaElIndice()
    {
        // Actuar
        var borrar = Assert.Single(ExpresionesDown(new M004_IndiceUnicoBusquedasFavoritas()).OfType<DeleteIndexExpression>());

        // Afirmar
        Assert.Equal("UX_BusquedasFavoritas_Usuario_Salon", borrar.Index.Name);
        Assert.Equal("BusquedasFavoritas", borrar.Index.TableName);
    }

    [Fact]
    public void M005_Up_CreaSolicitudesEnCuatroLotesIdempotentes()
    {
        // Actuar
        var sentencias = ExpresionesUp(new M005_CrearSolicitudes())
            .Cast<ExecuteSqlStatementExpression>()
            .Select(e => e.SqlStatement)
            .ToList();

        // Afirmar: tabla, dos CHECK e índice, cada uno protegido para no fallar si ya existe (Azure).
        Assert.Equal(4, sentencias.Count);
        Assert.StartsWith("IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL", sentencias[0].Trim());
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')", sentencias[1]);
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')", sentencias[2]);
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'", sentencias[3]);
        Assert.DoesNotContain(sentencias, s => s.Contains("\nGO"));
    }

    [Fact]
    public void M005_Up_DefineLasColumnasYLlavesDeLaHu14()
    {
        // Actuar
        var tabla = ExpresionesUp(new M005_CrearSolicitudes()).Cast<ExecuteSqlStatementExpression>().First().SqlStatement;

        // Afirmar
        Assert.Contains("CREATE TABLE dbo.Solicitudes", tabla);
        Assert.Contains("Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY", tabla);
        Assert.Contains("SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id)", tabla);
        Assert.Contains("ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id)", tabla);
        Assert.Contains("PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id)", tabla);
        Assert.Contains("FechaEvento     DATE          NOT NULL", tabla);
        Assert.Contains("TipoEvento      NVARCHAR(100) NOT NULL", tabla);
        Assert.Contains("NumeroInvitados INT           NOT NULL", tabla);
        Assert.Contains("Comentarios     NVARCHAR(500) NULL", tabla);
        Assert.Contains("Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente'", tabla);
        Assert.Contains("FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()", tabla);
    }

    [Fact]
    public void M005_Up_AgregaLosChecksYElIndiceUnicoFiltradoDePendientes()
    {
        // Actuar
        var sentencias = ExpresionesUp(new M005_CrearSolicitudes()).Cast<ExecuteSqlStatementExpression>().Select(e => e.SqlStatement).ToList();

        // Afirmar
        Assert.Contains("ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0)", sentencias[1]);
        Assert.Contains("ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'))", sentencias[2]);
        Assert.Contains("SET QUOTED_IDENTIFIER ON;", sentencias[3]);
        Assert.Contains("CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente", sentencias[3]);
        Assert.Contains("ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)", sentencias[3]);
        Assert.Contains("WHERE Estado = 'pendiente';", sentencias[3]);
    }

    [Fact]
    public void M005_Down_BorraLaTablaSolicitudes()
    {
        // Actuar
        var borrar = Assert.Single(ExpresionesDown(new M005_CrearSolicitudes()).OfType<DeleteTableExpression>());

        // Afirmar
        Assert.Equal("Solicitudes", borrar.TableName);
        Assert.Equal("DROP TABLE [dbo].[Solicitudes]", Generador.Generate(borrar).Trim().TrimEnd(';'));
    }

    [Fact]
    public void M006_Up_AgregaTelefonoContactoSoloSiNoExiste()
    {
        // Actuar
        var sentencia = Assert.Single(ExpresionesUp(new M006_AgregarTelefonoContactoSolicitudes()).OfType<ExecuteSqlStatementExpression>()).SqlStatement;

        // Afirmar: idempotente porque producción ya tiene la columna.
        Assert.StartsWith("IF COL_LENGTH(N'dbo.Solicitudes', N'TelefonoContacto') IS NULL", sentencia.Trim());
        Assert.Contains("ALTER TABLE dbo.Solicitudes ADD TelefonoContacto NVARCHAR(20) NULL;", sentencia);
    }

    [Fact]
    public void M006_Down_QuitaLaColumna()
    {
        // Actuar
        var borrar = Assert.Single(ExpresionesDown(new M006_AgregarTelefonoContactoSolicitudes()).OfType<DeleteColumnExpression>());

        // Afirmar
        Assert.Equal("Solicitudes", borrar.TableName);
        Assert.Equal("TelefonoContacto", Assert.Single(borrar.ColumnNames));
        Assert.Contains("ALTER TABLE [dbo].[Solicitudes] DROP COLUMN [TelefonoContacto]", Generador.Generate(borrar));
    }

    private static readonly string[] ProcedimientosHu15 = ["sp_Solicitud_Crear", "sp_Solicitud_Obtener", "sp_Solicitud_ListarPorCliente"];

    [Fact]
    public void M007_Up_CreaLosTresStoredProceduresConCreateOrAlter()
    {
        // Actuar
        var sentencias = ExpresionesUp(new M007_ProcedimientosSolicitudes())
            .Cast<ExecuteSqlStatementExpression>()
            .Select(e => e.SqlStatement.Trim())
            .ToList();

        // Afirmar: un lote por SP, cada uno empezando con CREATE OR ALTER y sin separador GO.
        Assert.Equal(3, sentencias.Count);
        for (var i = 0; i < ProcedimientosHu15.Length; i++)
        {
            Assert.StartsWith($"CREATE OR ALTER PROCEDURE {ProcedimientosHu15[i]}", sentencias[i]);
        }
        Assert.DoesNotContain(sentencias, s => s.Contains("\nGO"));
    }

    [Fact]
    public void M007_Up_CrearValidaFechaConBloqueoYTraduceElDuplicadoA4004()
    {
        // Actuar
        var crear = ExpresionesUp(new M007_ProcedimientosSolicitudes()).Cast<ExecuteSqlStatementExpression>().First().SqlStatement;

        // Afirmar
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", crear);
        Assert.Contains("Estado = 'confirmada'", crear);
        Assert.Contains("'pendiente', SYSUTCDATETIME()", crear);
        Assert.Contains("ERROR_NUMBER() IN (2601, 2627)", crear);
        Assert.Contains("@Codigo = 4004", crear);
    }

    [Fact]
    public void M007_Down_BorraLosTresStoredProcedures()
    {
        // Actuar
        var sentencias = ExpresionesDown(new M007_ProcedimientosSolicitudes())
            .Cast<ExecuteSqlStatementExpression>()
            .Select(e => e.SqlStatement)
            .ToList();

        // Afirmar
        Assert.Equal(ProcedimientosHu15.Select(sp => $"DROP PROCEDURE IF EXISTS dbo.{sp};"), sentencias);
    }
}
