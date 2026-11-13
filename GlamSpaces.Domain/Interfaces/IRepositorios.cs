namespace GlamSpaces.Domain.Interfaces;

// Contratos de acceso a datos. Domain solo define QUÉ se puede hacer;
// Infrastructure los implementa llamando stored procedures.

public interface IUsuarioRepositorio
{
    Task<RespuestaApi<UsuarioResponse>> Registrar(RegistroRequest request);
    Task<RespuestaApi<UsuarioResponse>> Login(LoginRequest request);
    Task<RespuestaApi<UsuarioResponse>> Obtener(int id);
    Task<RespuestaApi<UsuarioResponse>> ConvertirAAdministrador(ConvertirAdministradorRequest request);
}

public interface ISalonRepositorio
{
    Task<RespuestaApi<SalonResponse>> Crear(SalonRequest request);
    Task<RespuestaApi<SalonResponse>> Obtener(int id);
    Task<RespuestaApi<SalonResponse>> Actualizar(ActualizarSalonRequest request);
    Task<RespuestaPaginada<SalonResumenResponse>> Listar(ListarSalonesRequest request);
    Task<RespuestaPaginada<SalonBusquedaResponse>> Buscar(BuscarSalonesRequest request);
    Task<RespuestaApi<DisponibilidadResponse>> Disponibilidad(DisponibilidadRequest request);
    Task<RespuestaApi<int>> Eliminar(EliminarSalonRequest request); // Datos = id eliminado
}

// HU-07 (fotos): registro de las fotos de un salón (la URL; el archivo vive en IAlmacenamientoFotos).
public interface IFotoSalonRepositorio
{
    Task<RespuestaApi<FotoSalonResponse>> Agregar(int salonId, int adminId, string url);
    Task<RespuestaApi<FotoSalonResponse>> Validar(EliminarFotoRequest request);  // valida y regresa la foto sin borrarla
    Task<RespuestaApi<FotoSalonResponse>> Eliminar(EliminarFotoRequest request);
}

public interface IPaqueteRepositorio
{
    Task<RespuestaApi<PaqueteResponse>> Crear(CrearPaqueteRequest request);
    Task<RespuestaApi<PaqueteResponse>> Actualizar(ActualizarPaqueteRequest request);
    Task<RespuestaApi<int>> Eliminar(EliminarPaqueteRequest request); // Datos = id eliminado
}

// HU-15: solicitudes de reservación de un cliente. HU-18: el administrador las atiende.
public interface ISolicitudRepositorio
{
    Task<RespuestaApi<SolicitudResponse>> Crear(CrearSolicitudRequest request);
    Task<RespuestaApi<SolicitudResponse>> Obtener(ObtenerSolicitudRequest request);
    Task<RespuestaPaginada<SolicitudListadoResponse>> Listar(ListarSolicitudesRequest request);
    Task<RespuestaApi<SolicitudAtendidaResponse>> Aceptar(AtenderSolicitudRequest request);
    Task<RespuestaApi<SolicitudAtendidaResponse>> Rechazar(AtenderSolicitudRequest request);
    Task<RespuestaPaginada<SolicitudAdminResponse>> ListarPorSalon(ListarSolicitudesSalonRequest request);
}
