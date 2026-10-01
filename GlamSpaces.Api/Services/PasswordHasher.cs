using System.Security.Cryptography;

namespace GlamSpaces.Api.Services;

// Hashea y verifica contraseñas con PBKDF2 (sin paquetes externos).
// El resultado que se guarda en Usuario.PasswordHash tiene el formato:
//   {iteraciones}.{salt en base64}.{hash en base64}
public static class PasswordHasher
{
    private const int TamanoSalt = 16; // bytes
    private const int TamanoHash = 32; // bytes
    private const int Iteraciones = 100_000;

    public static string Hashear(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iteraciones,
            HashAlgorithmName.SHA256,
            TamanoHash
        );

        return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string password, string passwordHashGuardado)
    {
        string[] partes = passwordHashGuardado.Split('.', 3);
        if (partes.Length != 3) return false;

        int iteraciones = int.Parse(partes[0]);
        byte[] salt = Convert.FromBase64String(partes[1]);
        byte[] hashGuardado = Convert.FromBase64String(partes[2]);

        byte[] hashIntento = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iteraciones,
            HashAlgorithmName.SHA256,
            hashGuardado.Length
        );

        return CryptographicOperations.FixedTimeEquals(hashIntento, hashGuardado);
    }
}
