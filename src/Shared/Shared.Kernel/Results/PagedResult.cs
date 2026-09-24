namespace Shared.Kernel.Results;

/// <summary>
/// Resultado paginado estandar: todo listado se pagina, y todos con la misma forma, para que el cliente tenga
/// UN componente de paginacion y no uno por endpoint. En el JSON: <c>{ items, page, pageSize, totalCount,
/// totalPages }</c> dentro del <c>data</c> del envelope (contrato acordado con la plantilla de frontend).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Limites de paginacion, en un solo lugar.</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize));
}
