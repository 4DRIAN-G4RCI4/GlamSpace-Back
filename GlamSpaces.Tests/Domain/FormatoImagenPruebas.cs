namespace GlamSpaces.Tests.Domain;

// HU-07 (fotos): el tipo de la imagen se reconoce por su contenido (magic bytes), no por la extensión.
public class FormatoImagenPruebas
{
    public static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    public static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    [Fact]
    public void Detectar_ReconoceJpgPngYWebpConSuExtensionYContentType()
    {
        Assert.Equal(new FormatoImagen(".jpg", "image/jpeg"), FormatoImagen.Detectar(Jpg));
        Assert.Equal(new FormatoImagen(".png", "image/png"), FormatoImagen.Detectar(Png));
        Assert.Equal(new FormatoImagen(".webp", "image/webp"), FormatoImagen.Detectar(Webp));
    }

    [Theory]
    [InlineData("texto que se hace pasar por foto.jpg")]   // archivo falso con extensión .jpg
    [InlineData("GIF89a......")]                           // GIF: no se acepta
    [InlineData("%PDF-1.7....")]                           // PDF
    [InlineData("RIFF....WAVE")]                           // RIFF que no es WEBP (audio)
    public void Detectar_RechazaLoQueNoEsJpgPngNiWebp(string contenido)
    {
        Assert.Null(FormatoImagen.Detectar(System.Text.Encoding.ASCII.GetBytes(contenido)));
    }

    [Fact]
    public void Detectar_ArchivosVaciosOMuyCortosNoSonImagen()
    {
        Assert.Null(FormatoImagen.Detectar([]));
        Assert.Null(FormatoImagen.Detectar([0xFF, 0xD8]));
        Assert.Null(FormatoImagen.Detectar("RIFF"u8));
    }
}
