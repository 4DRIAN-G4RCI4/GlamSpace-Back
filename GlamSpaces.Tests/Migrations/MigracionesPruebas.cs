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
}
