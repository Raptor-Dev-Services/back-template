using Shared.Kernel.Storage;
using Xunit;

namespace Shared.Tests.Storage;

public sealed class StoragePrimitivesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Pdf = "%PDF-1.7\n..."u8.ToArray();
    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
    private static readonly byte[] Html = "<html><script>"u8.ToArray();

    [Fact]
    public void La_firma_de_bytes_tiene_que_coincidir_con_el_tipo_declarado()
    {
        Assert.Equal(".png", AllowedFileTypes.ExtensionFor("image/png", Png));
        Assert.Equal(".png", AllowedFileTypes.ExtensionFor("IMAGE/PNG", Png));
        Assert.Equal(".pdf", AllowedFileTypes.ExtensionFor("application/pdf", Pdf));
        Assert.Equal(".webp", AllowedFileTypes.ExtensionFor("image/webp", Webp));

        // Un HTML que dice ser PNG, un PDF que dice ser imagen, un tipo no admitido.
        Assert.Null(AllowedFileTypes.ExtensionFor("image/png", Html));
        Assert.Null(AllowedFileTypes.ExtensionFor("image/jpeg", Pdf));
        Assert.Null(AllowedFileTypes.ExtensionFor("text/html", Html));
        Assert.Null(AllowedFileTypes.ExtensionFor(null, Png));
        Assert.Null(AllowedFileTypes.ExtensionFor("image/png", Png.AsMemory(0, 3)));
    }

    [Fact]
    public void Las_claves_nuevas_llevan_el_tenant_y_la_extension_validada()
    {
        var key = ObjectKeys.NewFor(42, ".png", new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("42/2026/09/", key);
        Assert.EndsWith(".png", key);
        Assert.True(ObjectKeys.IsWellFormed(key));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/../b.png")]
    [InlineData("./a.png")]
    [InlineData("a//b.png")]
    [InlineData("/a.png")]
    [InlineData("a\\b.png")]
    public void Una_clave_con_segmentos_raros_no_esta_bien_formada(string key) =>
        Assert.False(ObjectKeys.IsWellFormed(key));
}
