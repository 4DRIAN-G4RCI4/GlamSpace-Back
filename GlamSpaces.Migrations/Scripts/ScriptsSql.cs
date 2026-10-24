namespace GlamSpaces.Migrations.Scripts;

// Lee los scripts .sql de Database/ (incrustados en el ensamblado) y los separa en lotes.
// FluentMigrator no acepta el separador "GO" dentro de un Execute.Sql, así que cada lote
// entre dos "GO" se ejecuta por separado, igual que lo haría sqlcmd o SSMS.
internal static class ScriptsSql
{
    private static readonly Regex SeparadorGo = new(
        @"^\s*GO\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public static IReadOnlyList<string> Lotes(string recurso)
    {
        using var flujo = typeof(ScriptsSql).Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"No se encontró el script incrustado '{recurso}'.");
        using var lector = new StreamReader(flujo);

        return SeparadorGo.Split(lector.ReadToEnd())
            .Select(lote => lote.Trim())
            .Where(lote => lote.Length > 0)
            .ToList();
    }
}
