namespace GlamSpaces.Tests.Migrations;

// Lectura de los scripts de Database/ incrustados y su división en lotes por "GO".
public class ScriptsSqlPruebas
{
    [Fact]
    public void Lotes_DeLosStoredProcedures_DejaUnSpPorLoteYSinGo()
    {
        // Actuar
        var lotes = ScriptsSql.Lotes("Database.02_StoredProcedures.sql");

        // Afirmar
        Assert.Equal(12, lotes.Count(l => l.Contains("CREATE OR ALTER PROCEDURE")));
        Assert.All(lotes, l => Assert.DoesNotContain("\nGO", l, StringComparison.OrdinalIgnoreCase));
        Assert.All(lotes, l => Assert.Equal(l.Trim(), l));
        Assert.DoesNotContain(lotes, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public void Lotes_DeLasTablas_IncluyeLasCincoTablas()
    {
        // Actuar
        var texto = string.Join("\n", ScriptsSql.Lotes("Database.01_Tablas.sql"));

        // Afirmar
        foreach (var tabla in new[] { "Usuarios", "Salones", "Paquetes", "FotosSalon", "Reservaciones" })
        {
            Assert.Contains($"CREATE TABLE {tabla}", texto);
        }
    }

    [Fact]
    public void Lotes_DeUnScriptQueNoExiste_LanzaUnErrorClaro()
    {
        // Actuar
        var error = Assert.Throws<InvalidOperationException>(() => ScriptsSql.Lotes("Database.NoExiste.sql"));

        // Afirmar
        Assert.Contains("Database.NoExiste.sql", error.Message);
    }
}
