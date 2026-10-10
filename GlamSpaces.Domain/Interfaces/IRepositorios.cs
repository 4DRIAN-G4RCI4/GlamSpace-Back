namespace GlamSpaces.Domain.Interfaces;

// Contratos de acceso a datos. Domain solo define QUÉ se puede hacer;
// Infrastructure los implementa llamando stored procedures.

public interface IUsuarioRepositorio
{
    Task<RespuestaApi<UsuarioResponse>> Registrar(RegistroRequest request);
    Task<RespuestaApi<UsuarioResponse>> Login(LoginRequest request);
    Task<RespuestaApi<UsuarioResponse>> Obtener(int id);
}

public interface ISalonRepositorio
{
    Task<RespuestaApi<SalonResponse>> Crear(SalonRequest request);
    Task<RespuestaApi<SalonResponse>> Obtener(int id);
    Task<RespuestaApi<SalonResponse>> Actualizar(ActualizarSalonRequest request);
    Task<RespuestaPaginada<SalonResumenResponse>> Listar(ListarSalonesRequest request);
}

public interface IPaqueteRepositorio
{
    Task<RespuestaApi<PaqueteResponse>> Crear(CrearPaqueteRequest request);
    Task<RespuestaApi<PaqueteResponse>> Actualizar(ActualizarPaqueteRequest request);
    Task<RespuestaApi<int>> Eliminar(EliminarPaqueteRequest request); // Datos = id eliminado
}
