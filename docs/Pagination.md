# Paginado

Todo listado se pagina y todos con la **misma forma**, para que el cliente tenga un solo componente.

## Contrato

`Shared.Kernel/Results/PagedResult.cs`:

```csharp
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => ...;   // calculado
}
```

En el JSON va dentro del `data` del envelope:

```json
{
  "data": { "items": [ ... ], "page": 1, "pageSize": 20, "totalCount": 57, "totalPages": 3 },
  "isSuccess": true, "message": null, "utcTimeStamp": "..."
}
```

## Limites

`Paging` (mismo archivo) centraliza los limites: `DefaultPageSize = 20`, `MaxPageSize = 100`.
`Paging.Normalize(page, pageSize)` lleva `page < 1` a 1, `pageSize <= 0` al default y recorta al maximo. Un cliente
que pide `pageSize=10000` recibe 100, no un error.

## Como se usa

Controller: `[FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize`, pasados al request.

Repositorio: normalizar, contar, ordenar de forma **estable** (con desempate por id) y cortar:

```csharp
var (p, size) = Paging.Normalize(page, pageSize);
var query = db.Set<X>().AsNoTracking();
var total = await query.CountAsync(ct);
var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
    .Skip((p - 1) * size).Take(size).Select(...).ToListAsync(ct);
return new PagedResult<XDto>(items, p, size, total);
```

Presenter: `ResultPresenter<TController, TResponse, PagedResult<XDto>>`.

Ejemplos reales: `GET /api/v1/users`, `GET /api/v1/audit-log` (con filtro `action`),
`GET /api/v1/automated-tasks/{code}/runs`.
