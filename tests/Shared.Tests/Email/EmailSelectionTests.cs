using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Email;
using Shared.Kernel.Email;
using Xunit;

namespace Shared.Tests.Email;

public sealed class EmailSelectionTests
{
    private static IEmailSender Resolve(string environment, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment(environment));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAppEmail(configuration);
        return services.BuildServiceProvider().GetRequiredService<IEmailSender>();
    }

    [Fact]
    public void Con_Smtp_Host_se_usa_el_envio_real_en_cualquier_entorno() =>
        Assert.IsType<SmtpEmailSender>(Resolve("Development", ("Smtp:Host", "localhost"), ("Smtp:From", "no-reply@example.test")));

    [Fact]
    public void Sin_SMTP_en_desarrollo_se_registra_en_el_log() =>
        Assert.IsType<LoggingEmailSender>(Resolve("Development"));

    [Fact]
    public async Task Sin_SMTP_fuera_de_desarrollo_cada_envio_falla_ruidosamente()
    {
        var sender = Resolve("Production");

        Assert.IsType<FailFastEmailSender>(sender);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync("a@b.test", "s", "b"));
    }

    [Fact]
    public void SMTP_a_medias_no_arranca()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Resolve("Production", ("Smtp:Host", "smtp.example.test")));
        Assert.Contains("Smtp:From", ex.Message);
    }

    [Fact]
    public void El_enlace_codifica_el_token()
    {
        var link = new WebOptions { BaseUrl = "https://app.example.test/" }.Link("/reset-password", "token", "a+b/c=");
        Assert.Equal("https://app.example.test/reset-password?token=a%2Bb%2Fc%3D", link);
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
