namespace GlamSpaces.Tests.Apoyo;

// Conexión ADO.NET falsa para probar los repositorios SIN base de datos: Dapper corre de verdad
// sobre ella, pero en lugar de ejecutar el stored procedure regresa las tablas y el @Codigo /
// @Mensaje que se le configuran, y guarda qué SP se llamó y con qué parámetros.
public sealed class ConexionFalsa : DbConnection
{
    private ConnectionState _estado = ConnectionState.Closed;
    private DataTable[] _tablas = [];
    private int _codigo;
    private string _mensaje = string.Empty;

    // El último comando ejecutado: nombre del SP, tipo y parámetros de entrada.
    public string? ComandoEjecutado { get; private set; }
    public CommandType? TipoDeComando { get; private set; }
    public Dictionary<string, object?> Parametros { get; } = [];
    // Todos los SP ejecutados, en orden (para repositorios que llaman a más de uno).
    public List<string> ComandosEjecutados { get; } = [];
    private readonly Queue<(int Codigo, string Mensaje, DataTable[] Tablas)> _siguientes = new();

    // Lo que "regresa el SP" en la siguiente ejecución.
    public ConexionFalsa Responder(int codigo, string mensaje, params DataTable[] tablas)
    {
        _codigo = codigo;
        _mensaje = mensaje;
        _tablas = tablas;
        return this;
    }

    // Lo que "regresa el SP" en la ejecución que sigue a la configurada con Responder.
    public ConexionFalsa Despues(int codigo, string mensaje, params DataTable[] tablas)
    {
        _siguientes.Enqueue((codigo, mensaje, tablas));
        return this;
    }

    // Arma una tabla con una sola fila a partir de pares (columna, valor).
    public static DataTable Tabla(params (string Columna, object? Valor)[] celdas) => Tabla([celdas]);

    // Arma una tabla con varias filas; todas deben traer las mismas columnas en el mismo orden.
    public static DataTable Tabla(IEnumerable<(string Columna, object? Valor)[]> filas)
    {
        var tabla = new DataTable();
        foreach (var fila in filas)
        {
            if (tabla.Columns.Count == 0)
            {
                foreach (var (columna, valor) in fila)
                {
                    tabla.Columns.Add(columna, valor?.GetType() ?? typeof(string));
                }
            }
            tabla.Rows.Add(fila.Select(c => c.Valor ?? DBNull.Value).ToArray());
        }
        return tabla;
    }

    internal DbDataReader Ejecutar(ComandoFalso comando)
    {
        ComandoEjecutado = comando.CommandText;
        ComandosEjecutados.Add(comando.CommandText);
        TipoDeComando = comando.CommandType;
        Parametros.Clear();
        foreach (DbParameter parametro in comando.Parameters)
        {
            if (parametro.Direction == ParameterDirection.Output)
            {
                parametro.Value = parametro.ParameterName.TrimStart('@') == "Codigo" ? _codigo : _mensaje;
            }
            else
            {
                Parametros[parametro.ParameterName.TrimStart('@')] = parametro.Value is DBNull ? null : parametro.Value;
            }
        }

        var datos = new DataSet();
        foreach (var tabla in _tablas)
        {
            datos.Tables.Add(tabla.Copy());
        }
        // Un SP que hace RETURN sin SELECT no regresa filas: se simula con una tabla vacía.
        if (datos.Tables.Count == 0)
        {
            datos.Tables.Add(new DataTable());
        }
        if (_siguientes.Count > 0)
        {
            (_codigo, _mensaje, _tablas) = _siguientes.Dequeue();
        }
        return datos.CreateDataReader();
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "Falsa";
    public override string DataSource => "Falsa";
    public override string ServerVersion => "0";
    public override ConnectionState State => _estado;
    public override void Open() => _estado = ConnectionState.Open;
    public override void Close() => _estado = ConnectionState.Closed;
    public override void ChangeDatabase(string databaseName) { }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    protected override DbCommand CreateDbCommand() => new ComandoFalso(this);
}

// Comando de la ConexionFalsa. Los parámetros los guarda un SqlCommand interno (Dapper los
// crea como cualquier DbParameter); al ejecutarse, delega en la conexión.
public sealed class ComandoFalso(ConexionFalsa conexion) : DbCommand
{
    private readonly SqlCommand _parametros = new();

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get; set; } = conexion;
    protected override DbParameterCollection DbParameterCollection => _parametros.Parameters;
    protected override DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => _parametros.CreateParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => conexion.Ejecutar(this);
    // Un SP sin SELECT (ExecuteAsync): se registra igual y llena los OUTPUT.
    public override int ExecuteNonQuery() { using var lector = conexion.Ejecutar(this); return 1; }
    public override object? ExecuteScalar() => throw new NotSupportedException();
}
