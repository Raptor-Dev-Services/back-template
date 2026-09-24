using System.Reflection;
using Authentication.Domain.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Kernel.Security;
using Shared.Web;
using Shared.Web.Authorization;
using Xunit;
using Xunit.Abstractions;

namespace Architecture.Tests;

/// <summary>
/// La autorizacion como MECANISMO y no como convencion. En un controller con <c>[Authorize]</c> a nivel de clase,
/// una accion sin policy queda solo AUTENTICADA: la abre cualquier usuario del tenant, sin error y sin aviso. Estas
/// pruebas hacen que la accion numero N no pueda nacer asi: o declara un permiso del catalogo, o es anonima, o
/// esta en la lista de excepciones con su motivo escrito.
/// </summary>
public sealed class AuthorizationSurfaceTests(ITestOutputHelper output)
{
    /// <summary>Ensamblados con controllers. Se cargan por NOMBRE: recorrer lo ya cargado pasaria en vacio.</summary>
    private static readonly string[] PresentationAssemblies =
    [
        "Authentication.Presentation",
        "Tenancy.Presentation",
        "Users.Presentation",
        "Host.Api",
    ];

    /// <summary>
    /// Acciones autenticadas SIN permiso RBAC, con el motivo. Formato <c>Controller.Accion</c>. Una entrada nueva aqui
    /// es una decision, no un descuido.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> WithoutPermissionOnPurpose = new Dictionary<string, string>
    {
        ["AccountController.Me"] = "la cuenta PROPIA, resuelta por el sub del token",
        ["AccountController.ChangePassword"] = "la contrasena PROPIA; exige la actual",
        ["AccountController.BeginTwoFactorSetup"] = "el 2FA PROPIO",
        ["AccountController.EnableTwoFactor"] = "el 2FA PROPIO; exige un codigo de la app",
        ["AccountController.DisableTwoFactor"] = "el 2FA PROPIO; exige un segundo factor vigente",
    };

    [Fact]
    public void Toda_accion_declara_un_permiso_o_es_anonima_o_esta_justificada()
    {
        var missing = AllActions()
            .Where(a => !IsAnonymous(a.Controller, a.Action)
                        && !HasPermissionPolicy(a.Action) && !HasPermissionPolicy(a.Controller)
                        && !WithoutPermissionOnPurpose.ContainsKey(Key(a)))
            .Select(a => $"{Key(a)}  ({RouteOf(a.Controller)})")
            .Order()
            .ToList();

        Assert.True(missing.Count == 0,
            "Estas acciones estan autenticadas pero NO exigen ningun permiso, asi que las abre cualquier usuario del " +
            "tenant:\n  " + string.Join("\n  ", missing) +
            "\n\nDeclara [Authorize(Policy = PermissionPolicy.Prefix + KnownPermissions.X)] o agregala a " +
            "WithoutPermissionOnPurpose con el motivo.");
    }

    [Fact]
    public void Las_excepciones_justificadas_siguen_existiendo()
    {
        // Una excepcion que ya no corresponde a ninguna accion es una puerta abierta esperando a que alguien
        // cree una accion con ese nombre.
        var actions = AllActions().Select(Key).ToHashSet();
        Assert.All(WithoutPermissionOnPurpose.Keys, key => Assert.Contains(key, actions));
    }

    [Fact]
    public void Todo_permiso_exigido_existe_en_el_catalogo_que_se_siembra()
    {
        // Un permiso que no se siembra no lo porta nadie: el endpoint responde 403 a TODO el mundo, en silencio.
        var seeded = RbacCatalog.Permissions.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);

        var orphans = AllActions()
            .SelectMany(a => PermissionCodes(a.Action).Concat(PermissionCodes(a.Controller)).Select(code => (Key: Key(a), code)))
            .Where(x => !seeded.Contains(x.code))
            .Select(x => $"{x.Key} exige '{x.code}'")
            .Distinct()
            .Order()
            .ToList();

        Assert.True(orphans.Count == 0,
            "Estos endpoints exigen un permiso que el catalogo RBAC no siembra:\n  " + string.Join("\n  ", orphans));
    }

    [Fact]
    public void El_catalogo_de_permisos_es_uno_solo()
    {
        var declared = typeof(KnownPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(RbacCatalog.Permissions, p => Assert.Contains(p.Code, declared));
        Assert.All(RbacCatalog.RolePermissions.SelectMany(r => r.Value), code => Assert.Contains(code, declared));
        Assert.All(declared, code => Assert.Contains(code, RbacCatalog.Permissions.Select(p => p.Code)));
    }

    [Fact]
    public void Toda_politica_de_tasa_citada_existe_en_el_catalogo()
    {
        var declared = typeof(RateLimitPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var cited = AllControllers().SelectMany(c =>
            c.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true).Select(a => (Where: c.Name, a.PolicyName))
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true)
                    .Select(a => (Where: $"{c.Name}.{m.Name}", a.PolicyName)))));

        var unknown = cited
            .Where(x => x.PolicyName is not null && !declared.Contains(x.PolicyName))
            .Select(x => $"{x.Where} cita '{x.PolicyName}'")
            .ToList();

        Assert.True(unknown.Count == 0, "Politicas de tasa que nadie registra:\n  " + string.Join("\n  ", unknown));
    }

    [Fact]
    public void La_superficie_de_credenciales_anonima_lleva_limite_de_tasa()
    {
        // Login, refresh, restablecer y bootstrap son superficie de fuerza bruta. Si alguien quita el atributo, el
        // limite desaparece sin que nada mas falle.
        foreach (var name in new[] { "AuthController", "BootstrapController" })
        {
            var controller = AllControllers().Single(c => c.Name == name);
            var policy = controller.GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true)?.PolicyName;
            Assert.Equal(RateLimitPolicies.Auth, policy);
        }
    }

    [Fact]
    public void Inventario_de_la_superficie_http()
    {
        var controllers = AllControllers().ToList();
        var actions = AllActions().ToList();
        output.WriteLine($"Controllers: {controllers.Count} | Acciones: {actions.Count} | Justificadas sin permiso: {WithoutPermissionOnPurpose.Count}");
        foreach (var action in actions.OrderBy(Key))
            output.WriteLine($"  {Key(action),-45} {Describe(action)}");

        // Cotas INFERIORES: rompe si la reflexion deja de ver la superficie (el fallo que haria pasar en vacio a
        // las pruebas de arriba), no cada vez que alguien agrega un endpoint.
        Assert.True(controllers.Count >= 6, $"Solo se vieron {controllers.Count} controllers.");
        Assert.True(actions.Count >= 15, $"Solo se vieron {actions.Count} acciones.");
    }

    private static string Describe((Type Controller, MethodInfo Action) a) =>
        IsAnonymous(a.Controller, a.Action) ? "anonima"
        : string.Join(", ", PermissionCodes(a.Action).Concat(PermissionCodes(a.Controller)).DefaultIfEmpty("(solo sesion)"));

    private static IEnumerable<Type> AllControllers() =>
        PresentationAssemblies
            .Select(RepoPaths.Load)
            .SelectMany(RepoPaths.SafeTypes)
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ControllerBase).IsAssignableFrom(t))
            .Distinct();

    private static IEnumerable<(Type Controller, MethodInfo Action)> AllActions() =>
        AllControllers().SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(m => (c, m)));

    private static string Key((Type Controller, MethodInfo Action) a) => $"{a.Controller.Name}.{a.Action.Name}";

    private static IEnumerable<string> PermissionCodes(MemberInfo target) =>
        target.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Policy)
            .Where(p => p?.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal) == true)
            .Select(p => p![PermissionPolicy.Prefix.Length..]);

    private static bool HasPermissionPolicy(MemberInfo target) => PermissionCodes(target).Any();

    private static bool IsAnonymous(Type controller, MethodInfo action) =>
        controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null
        || action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;

    private static string RouteOf(Type controller) =>
        controller.GetCustomAttribute<RouteAttribute>(inherit: true)?.Template ?? string.Empty;
}
