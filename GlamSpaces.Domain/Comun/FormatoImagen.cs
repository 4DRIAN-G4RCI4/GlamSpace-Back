namespace GlamSpaces.Domain.Comun;

// HU-07 (fotos): reconoce el tipo REAL de una imagen por sus primeros bytes ("magic bytes").
// No se confía en la extensión del archivo ni en el Content-Type que manda el cliente.
public sealed record FormatoImagen(string Extension, string ContentType)
{
    public static readonly FormatoImagen Jpg = new(".jpg", "image/jpeg");
    public static readonly FormatoImagen Png = new(".png", "image/png");
    public static readonly FormatoImagen Webp = new(".webp", "image/webp");

    private static readonly byte[] FirmaJpg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] FirmaRiff = "RIFF"u8.ToArray();
    private static readonly byte[] FirmaWebp = "WEBP"u8.ToArray();

    // null si no es JPG, PNG ni WEBP.
    public static FormatoImagen? Detectar(ReadOnlySpan<byte> inicio)
    {
        if (inicio.StartsWith(FirmaJpg)) return Jpg;
        if (inicio.StartsWith(FirmaPng)) return Png;
        // WEBP: "RIFF" + 4 bytes de tamaño + "WEBP"
        if (inicio.Length >= 12 && inicio.StartsWith(FirmaRiff) && inicio.Slice(8, 4).SequenceEqual(FirmaWebp)) return Webp;
        return null;
    }
}
