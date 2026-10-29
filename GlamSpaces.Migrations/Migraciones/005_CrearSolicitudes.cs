namespace GlamSpaces.Migrations.Migraciones;

// 005 · HU-14 (Sprint 5): tabla Solicitudes, las solicitudes de reservación que manda un cliente.
// La tabla ya se creó a mano en producción (glamspaces-db), así que Up() es idempotente:
//  - En una BD nueva crea la tabla con sus llaves, restricciones e índice.
//  - En producción se salta lo que ya existe y solo queda registrada en VersionInfo.
// Cada parte va en su propio lote para que las verificaciones IF NOT EXISTS vean la tabla ya creada.
[Migration(202610291000, "HU-14: tabla Solicitudes con restricciones e índice único de pendientes")]
public class M005_CrearSolicitudes : Migration
{
    private const string Tabla = "Solicitudes";

    public override void Up()
    {
        Execute.Sql(@"
IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Solicitudes (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id),
        ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id),
        PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id),
        FechaEvento     DATE          NOT NULL,
        TipoEvento      NVARCHAR(100) NOT NULL,
        NumeroInvitados INT           NOT NULL,
        Comentarios     NVARCHAR(500) NULL,
        Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente',
        FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END");

        Execute.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0);");

        Execute.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'));");

        // Índice único filtrado: un cliente no puede tener dos solicitudes pendientes para el mismo
        // salón y la misma fecha. Las confirmadas o rechazadas no cuentan. Los índices filtrados
        // exigen QUOTED_IDENTIFIER ON.
        Execute.Sql(@"
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'
               AND object_id = OBJECT_ID(N'dbo.Solicitudes'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente
        ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)
        WHERE Estado = 'pendiente';");
    }

    public override void Down()
    {
        // Borra la tabla completa; sus restricciones e índice se van con ella.
        Delete.Table(Tabla);
    }
}
