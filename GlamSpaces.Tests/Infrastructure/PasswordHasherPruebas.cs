namespace GlamSpaces.Tests.Infrastructure;

// Hash de contraseñas con PBKDF2: formato "{iteraciones}.{salt}.{hash}".
public class PasswordHasherPruebas
{
    private const string Password = "glam2026";

    [Fact]
    public void Hashear_NoGuardaLaContrasenaEnTextoPlano()
    {
        // Actuar
        var hash = PasswordHasher.Hashear(Password);

        // Afirmar
        Assert.DoesNotContain(Password, hash);
        Assert.Equal(3, hash.Split('.').Length);
        Assert.Equal("100000", hash.Split('.')[0]);
    }

    [Fact]
    public void Hashear_UsaUnaSalDistintaEnCadaHash()
    {
        // Actuar
        var hash1 = PasswordHasher.Hashear(Password);
        var hash2 = PasswordHasher.Hashear(Password);

        // Afirmar: misma contraseña, sal y hash distintos.
        Assert.NotEqual(hash1, hash2);
        Assert.NotEqual(hash1.Split('.')[1], hash2.Split('.')[1]);
    }

    [Fact]
    public void Verificar_AceptaLaContrasenaCorrecta()
    {
        // Arreglar
        var hash = PasswordHasher.Hashear(Password);

        // Actuar / Afirmar
        Assert.True(PasswordHasher.Verificar(Password, hash));
    }

    [Fact]
    public void Verificar_RechazaUnaContrasenaIncorrecta()
    {
        // Arreglar
        var hash = PasswordHasher.Hashear(Password);

        // Actuar / Afirmar
        Assert.False(PasswordHasher.Verificar("otraClave1", hash));
    }

    [Theory]
    [InlineData("sin-login")]   // cuenta de prueba sin hash válido (04_DatosPrueba.sql)
    [InlineData("100000.abc")]  // le falta una parte
    [InlineData("")]
    public void Verificar_RechazaUnHashConFormatoInvalido(string hashGuardado)
    {
        // Actuar / Afirmar
        Assert.False(PasswordHasher.Verificar(Password, hashGuardado));
    }
}
