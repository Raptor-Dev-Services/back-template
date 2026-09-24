using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shared.Kernel.Context;
using Shared.Kernel.Storage;
using Tenancy.Application.UseCases.Files.GetFileUrl;
using Tenancy.Application.UseCases.Files.GetFileUrl.Responses;
using Tenancy.Application.UseCases.Files.UploadFile;
using Tenancy.Application.UseCases.Files.UploadFile.Responses;
using Xunit;

namespace Tenancy.Tests.UseCases;

public sealed class FileHandlerTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3];

    private readonly IObjectStorage _storage = Substitute.For<IObjectStorage>();
    private readonly IStoredFileRegistry _registry = Substitute.For<IStoredFileRegistry>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static ICurrentUser TenantUser(long? tenantId)
    {
        var user = Substitute.For<ICurrentUser>();
        user.TenantId.Returns(tenantId);
        return user;
    }

    private UploadFileHandler Upload(long? tenantId = 5) =>
        new(_storage, _registry, _unitOfWork, TenantUser(tenantId), NullLogger<UploadFileHandler>.Instance);

    private static UploadFileRequest Request(byte[] bytes, string? contentType, string? fileName = "foto.png") =>
        new(new MemoryStream(bytes), bytes.Length, fileName, contentType);

    [Fact]
    public async Task Upload_SinTenant_EsForbiddenYNoSubeNada()
    {
        var result = await Upload(tenantId: null).Handle(Request(Png, "image/png"), default);

        Assert.IsType<UploadFileForbiddenFailure>(result);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Upload_TipoNoPermitido_EsValidacion()
    {
        var result = await Upload().Handle(Request(Png, "text/html"), default);

        Assert.IsType<UploadFileValidationFailure>(result);
    }

    [Fact]
    public async Task Upload_BytesQueNoCorrespondenAlTipoDeclarado_EsValidacionYNoSube()
    {
        // Un HTML con Content-Type image/png: la firma de bytes lo delata.
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        var result = await Upload().Handle(Request(html, "image/png"), default);

        Assert.IsType<UploadFileValidationFailure>(result);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Upload_Valido_SubeConPrefijoDelTenantYRegistraAlDueno()
    {
        _storage.GetPresignedUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("https://firmada");

        var result = await Upload(tenantId: 5).Handle(Request(Png, "IMAGE/PNG", fileName: "C:\\fakepath\\foto.png"), default);

        var success = Assert.IsType<UploadFileSuccess>(result);
        Assert.StartsWith("5/", success.Data.ObjectKey);
        Assert.EndsWith(".png", success.Data.ObjectKey);
        await _storage.Received(1).UploadAsync(success.Data.ObjectKey, Arg.Any<Stream>(), Png.Length, "image/png", Arg.Any<CancellationToken>());
        // El nombre visible se limpia de rutas del cliente.
        _registry.Received(1).Add(success.Data.ObjectKey, "image/png", Png.Length, "foto.png");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_SiFallaElRegistro_BorraElObjetoSubidoYPropagaElError()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db caida"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Upload().Handle(Request(Png, "image/png"), default));

        await _storage.Received(1).DeleteAsync(Arg.Is<string>(k => k.StartsWith("5/")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_SiTambienFallaElBorrado_PropagaElErrorOriginal()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db caida"));
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("minio caido"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Upload().Handle(Request(Png, "image/png"), default));

        Assert.Equal("db caida", error.Message);
    }

    [Theory]
    [InlineData("../5/2026/09/x.png")]
    [InlineData("/5/2026/09/x.png")]
    [InlineData("5\\2026\\x.png")]
    public async Task GetUrl_ClaveMalFormada_EsNotFoundSinConsultar(string objectKey)
    {
        var result = await new GetFileUrlHandler(_storage, _registry).Handle(new GetFileUrlRequest(objectKey), default);

        Assert.IsType<GetFileUrlNotFoundFailure>(result);
        await _registry.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
    }

    [Fact]
    public async Task GetUrl_ClaveAjenaONoRegistrada_EsNotFoundYNoFirma()
    {
        // El registro filtra por tenant (RLS + query filter): una clave de otro tenant se ve igual que una inexistente.
        _registry.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((StoredFileDto?)null);

        var result = await new GetFileUrlHandler(_storage, _registry).Handle(new GetFileUrlRequest("9/2026/09/abc.png"), default);

        Assert.IsType<GetFileUrlNotFoundFailure>(result);
        await _storage.DidNotReceiveWithAnyArgs().GetPresignedUrlAsync(default!, default);
    }

    [Fact]
    public async Task GetUrl_ClavePropia_DevuelveLaUrlFirmada()
    {
        const string key = "5/2026/09/abc.png";
        _registry.FindAsync(key, Arg.Any<CancellationToken>())
            .Returns(new StoredFileDto(key, "image/png", 10, "a.png", DateTime.UtcNow));
        _storage.GetPresignedUrlAsync(key, Arg.Any<CancellationToken>()).Returns("https://firmada");

        var result = await new GetFileUrlHandler(_storage, _registry).Handle(new GetFileUrlRequest(key), default);

        var success = Assert.IsType<GetFileUrlSuccess>(result);
        Assert.Equal("https://firmada", success.Data.Url);
    }
}
