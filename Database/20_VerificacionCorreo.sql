/* =========================================================================
   GlamSpaces — Verificación del correo y recuperación de la cuenta con código (Sprint 10)
   Correr DESPUÉS de 19_TryCatchBitacora.sql. Idempotente: la columna, la tabla y sus restricciones
   solo se crean si no existen y los SP van con CREATE OR ALTER. La migración M015 ejecuta este script.
   Todos los SP siguen el patrón TRY/CATCH con bitácora del script 19.

   1) Usuarios.CorreoVerificado BIT NOT NULL DEFAULT 1: las cuentas que ya existen quedan verificadas;
      las nuevas se registran con 0 y verifican su correo con un código.
   2) Tabla CodigosCorreo: códigos de 6 dígitos que se mandan por correo. Solo se guarda su HASH
      (PBKDF2, lo calcula la API), vencen a los 15 minutos, sirven una vez y aguantan 5 intentos.
      Proposito: 'verificacion' (activar la cuenta) o 'recuperacion' (cambiar la contraseña).
   3) sp_Usuario_Registrar, sp_Usuario_ObtenerPorCorreo y sp_Usuario_Obtener regresan CorreoVerificado;
      el registro guarda la cuenta nueva con CorreoVerificado = 0.
   4) SP nuevos:
        sp_CodigoCorreo_Crear            guarda un código nuevo e invalida los anteriores (1011 si se
                                         pidió otro hace menos de @SegundosEspera)
        sp_CodigoCorreo_ObtenerVigente   el último código sin usar, con Vencido calculado en SQL
        sp_CodigoCorreo_RegistrarIntento correcto → usado · incorrecto → suma un intento
        sp_Usuario_MarcarCorreoVerificado
        sp_Usuario_CambiarContrasena     nuevo hash, cuenta verificada y códigos de recuperación invalidados

   Códigos: 1001 datos inválidos · 1005 usuario no existe · 1011 hay que esperar para pedir otro código.
   (1007 a 1010 los decide la API: correo sin verificar, código incorrecto, vencido, demasiados intentos.)
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1) Columna en Usuarios: con DEFAULT 1, las cuentas existentes quedan verificadas al agregarla.
IF COL_LENGTH(N'dbo.Usuarios', N'CorreoVerificado') IS NULL
    ALTER TABLE dbo.Usuarios
        ADD CorreoVerificado BIT NOT NULL CONSTRAINT DF_Usuarios_CorreoVerificado DEFAULT 1;
GO

-- 2) Códigos que se mandan por correo.
IF OBJECT_ID(N'dbo.CodigosCorreo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodigosCorreo (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CodigosCorreo PRIMARY KEY,
        UsuarioId     INT           NOT NULL CONSTRAINT FK_CodigosCorreo_Usuarios REFERENCES dbo.Usuarios (Id),
        Proposito     NVARCHAR(20)  NOT NULL CONSTRAINT CK_CodigosCorreo_Proposito CHECK (Proposito IN ('verificacion', 'recuperacion')),
        HashCodigo    NVARCHAR(200) NOT NULL,
        VenceEn       DATETIME2(0)  NOT NULL,
        Intentos      INT           NOT NULL CONSTRAINT DF_CodigosCorreo_Intentos DEFAULT 0,
        Usado         BIT           NOT NULL CONSTRAINT DF_CodigosCorreo_Usado DEFAULT 0,
        FechaCreacion DATETIME2(0)  NOT NULL CONSTRAINT DF_CodigosCorreo_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CodigosCorreo_Usuario_Proposito' AND object_id = OBJECT_ID(N'dbo.CodigosCorreo'))
    CREATE INDEX IX_CodigosCorreo_Usuario_Proposito ON dbo.CodigosCorreo (UsuarioId, Proposito, Id DESC);
GO

-- 3) Usuarios: CorreoVerificado en lo que regresan. La cuenta nueva nace sin verificar.
-- La API manda la contraseña ya hasheada (PBKDF2); el SP nunca ve el texto plano.
CREATE OR ALTER PROCEDURE sp_Usuario_Registrar
    @NombreCompleto NVARCHAR(150),
    @Correo         NVARCHAR(150),
    @PasswordHash   NVARCHAR(MAX),
    @TipoCuenta     NVARCHAR(20),
    @NombreSalon    NVARCHAR(150) = NULL,
    @Codigo         INT OUTPUT,
    @Mensaje        NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET @NombreCompleto = TRIM(@NombreCompleto);
        SET @Correo = LOWER(TRIM(@Correo));
        SET @TipoCuenta = LOWER(TRIM(@TipoCuenta));
        SET @NombreSalon = NULLIF(TRIM(@NombreSalon), '');

        IF ISNULL(@NombreCompleto, '') = ''
        BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe tu nombre completo.'; RETURN; END

        IF ISNULL(@Correo, '') NOT LIKE '_%@_%._%'
        BEGIN SELECT @Codigo = 1001, @Mensaje = N'El formato del correo no es válido.'; RETURN; END

        IF ISNULL(@TipoCuenta, '') NOT IN ('cliente', 'administrador')
        BEGIN SELECT @Codigo = 1001, @Mensaje = N'El tipo de cuenta debe ser "cliente" o "administrador".'; RETURN; END

        IF @TipoCuenta = 'administrador' AND @NombreSalon IS NULL
        BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe el nombre de tu salón.'; RETURN; END

        IF EXISTS (SELECT 1 FROM Usuarios WHERE Correo = @Correo)
        BEGIN SELECT @Codigo = 1002, @Mensaje = N'Ya existe una cuenta con este correo.'; RETURN; END

        INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro, CorreoVerificado)
        VALUES (@NombreCompleto, @Correo, @PasswordHash, @TipoCuenta,
                CASE WHEN @TipoCuenta = 'administrador' THEN @NombreSalon END, GETDATE(), 0);

        SELECT @Codigo = 0, @Mensaje = N'Cuenta creada correctamente.';

        SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro, CorreoVerificado
        FROM Usuarios WHERE Id = SCOPE_IDENTITY();
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_Usuario_Registrar';
        THROW;
    END CATCH
END
GO

-- Para el login: regresa el hash y la API lo verifica (el SP no puede verificar PBKDF2).
CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorCorreo
    @Correo  NVARCHAR(150),
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

        SELECT Id, NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro, CorreoVerificado
        FROM Usuarios WHERE Correo = LOWER(TRIM(@Correo));
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_Usuario_ObtenerPorCorreo';
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
        BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

        SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro, CorreoVerificado
        FROM Usuarios WHERE Id = @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_Usuario_Obtener';
        THROW;
    END CATCH
END
GO

-- 4) Códigos por correo.
-- Guarda un código nuevo (ya hasheado por la API) que vence en @MinutosVigencia. Antes invalida los
-- códigos sin usar del mismo propósito: solo sirve el último que se mandó.
-- 1011 si ya se pidió uno hace menos de @SegundosEspera (evita mandar correos sin parar).
CREATE OR ALTER PROCEDURE sp_CodigoCorreo_Crear
    @UsuarioId       INT,
    @Proposito       NVARCHAR(20),
    @HashCodigo      NVARCHAR(200),
    @MinutosVigencia INT = 15,
    @SegundosEspera  INT = 60,
    @Codigo          INT OUTPUT,
    @Mensaje         NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET XACT_ABORT ON;
        SET @Proposito = LOWER(TRIM(@Proposito));

        IF ISNULL(@Proposito, '') NOT IN ('verificacion', 'recuperacion')
        BEGIN SELECT @Codigo = 1001, @Mensaje = N'El propósito debe ser "verificacion" o "recuperacion".'; RETURN; END

        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId)
        BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

        BEGIN TRAN;

        -- UPDLOCK + HOLDLOCK: dos peticiones al mismo tiempo no mandan dos códigos.
        IF EXISTS (SELECT 1 FROM CodigosCorreo WITH (UPDLOCK, HOLDLOCK)
                   WHERE UsuarioId = @UsuarioId AND Proposito = @Proposito
                     AND FechaCreacion > DATEADD(SECOND, -@SegundosEspera, SYSUTCDATETIME()))
        BEGIN
            ROLLBACK;
            SELECT @Codigo = 1011, @Mensaje = N'Espera un momento antes de pedir otro código.';
            RETURN;
        END

        UPDATE CodigosCorreo SET Usado = 1
        WHERE UsuarioId = @UsuarioId AND Proposito = @Proposito AND Usado = 0;

        INSERT INTO CodigosCorreo (UsuarioId, Proposito, HashCodigo, VenceEn)
        VALUES (@UsuarioId, @Proposito, @HashCodigo, DATEADD(MINUTE, @MinutosVigencia, SYSUTCDATETIME()));

        COMMIT;

        SELECT @Codigo = 0, @Mensaje = N'Código guardado.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_CodigoCorreo_Crear';
        THROW;
    END CATCH
END
GO

-- El último código sin usar del usuario y propósito. Vencido se calcula aquí con el reloj de la BD.
-- Sin código NO es error: éxito sin filas.
CREATE OR ALTER PROCEDURE sp_CodigoCorreo_ObtenerVigente
    @UsuarioId INT,
    @Proposito NVARCHAR(20),
    @Codigo    INT OUTPUT,
    @Mensaje   NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

        SELECT TOP 1 Id, HashCodigo, Intentos,
               CAST(CASE WHEN VenceEn < SYSUTCDATETIME() THEN 1 ELSE 0 END AS BIT) AS Vencido
        FROM CodigosCorreo
        WHERE UsuarioId = @UsuarioId AND Proposito = LOWER(TRIM(@Proposito)) AND Usado = 0
        ORDER BY Id DESC;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_CodigoCorreo_ObtenerVigente';
        THROW;
    END CATCH
END
GO

-- @Correcto = 1: el código se usó y ya no sirve. @Correcto = 0: suma un intento.
-- Regresa los intentos que lleva (la API bloquea el código al llegar a 5).
CREATE OR ALTER PROCEDURE sp_CodigoCorreo_RegistrarIntento
    @Id       INT,
    @Correcto BIT,
    @Codigo   INT OUTPUT,
    @Mensaje  NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF @Correcto = 1
            UPDATE CodigosCorreo SET Usado = 1 WHERE Id = @Id;
        ELSE
            UPDATE CodigosCorreo SET Intentos = Intentos + 1 WHERE Id = @Id;

        SELECT @Codigo = 0, @Mensaje = N'Intento registrado.';

        SELECT Intentos FROM CodigosCorreo WHERE Id = @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_CodigoCorreo_RegistrarIntento';
        THROW;
    END CATCH
END
GO

-- La cuenta comprobó su correo con el código. Regresa el usuario (inicia sesión).
CREATE OR ALTER PROCEDURE sp_Usuario_MarcarCorreoVerificado
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        UPDATE Usuarios SET CorreoVerificado = 1 WHERE Id = @Id;

        IF @@ROWCOUNT = 0
        BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

        SELECT @Codigo = 0, @Mensaje = N'Tu correo quedó verificado.';

        SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro, CorreoVerificado
        FROM Usuarios WHERE Id = @Id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_Usuario_MarcarCorreoVerificado';
        THROW;
    END CATCH
END
GO

-- Nueva contraseña (ya hasheada por la API) después de comprobar el código de recuperación.
-- Como el código llegó a su correo, la cuenta también queda verificada. Se invalidan los demás
-- códigos de recuperación.
CREATE OR ALTER PROCEDURE sp_Usuario_CambiarContrasena
    @Id           INT,
    @PasswordHash NVARCHAR(MAX),
    @Codigo       INT OUTPUT,
    @Mensaje      NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        SET XACT_ABORT ON;

        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
        BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

        BEGIN TRAN;

        UPDATE Usuarios SET PasswordHash = @PasswordHash, CorreoVerificado = 1 WHERE Id = @Id;
        UPDATE CodigosCorreo SET Usado = 1 WHERE UsuarioId = @Id AND Proposito = 'recuperacion' AND Usado = 0;

        COMMIT;

        SELECT @Codigo = 0, @Mensaje = N'Tu contraseña se cambió correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        EXEC dbo.sp_Bitacora_RegistrarError @Procedimiento = N'sp_Usuario_CambiarContrasena';
        THROW;
    END CATCH
END
GO
