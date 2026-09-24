# Usuario actual

## `ICurrentUser`

`Shared.Kernel/Context/ICurrentUser.cs`:

```csharp
public interface ICurrentUser
{
    Guid? UserId { get; }    // claim sub (PublicId)
    long? TenantId { get; }  // claim tenant_id
}
```

- En una peticion: `Shared.Web/HttpCurrentUser.cs` lo lee del JWT **ya validado** (registrado scoped en `Program.cs`).
  Sin sesion, todo es `null`.
- Fuera de una peticion (tareas programadas, arranque, `dotnet ef`): `NoCurrentUser.Instance`, todo `null`. Las
  marcas de "quien" quedan vacias, que es lo honesto.

## Quien lo usa

- `AppDbContext`: sella `CreatedByUserId`/`UpdatedByUserId`/`DeletedByUserId`.
- La bitacora (`IAuditLog`): el actor de cada linea.
- El enriquecedor de logs: `UserId` y `TenantId` en cada evento.
- Casos de uso que necesitan al actor (subir un archivo al tenant del token, "ejecutar ahora" una tarea).

**No se usa para autorizar.** Eso lo decide la policy del endpoint sobre los claims `permission`.

## En el controller

`BaseApiController` expone `CurrentTenantId`, `CurrentUserPublicId` y `ClientIp`. Si el handler necesita al actor
como dato de negocio (p. ej. "no puedes darte de baja a ti mismo"), el controller se lo pasa en el request:

```csharp
new DisableUserProfileRequest(CurrentUserPublicId, id)
```

El tenant **no** se pasa en el request: lo aplican el filtro de EF y RLS.
