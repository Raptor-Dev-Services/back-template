// Uso: dotnet HealthProbe.dll [url]   (por omision http://127.0.0.1:8080/health/ready)
// Sale 0 si la respuesta es 2xx, 1 en cualquier otro caso (incluido no poder conectar). No imprime el cuerpo:
// el detalle de un fallo de readiness esta en el log de la API, no en la salida del healthcheck.
var url = args.Length > 0 ? args[0] : "http://127.0.0.1:8080/health/ready";

try
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    using var response = await client.GetAsync(url);
    Console.WriteLine($"{(int)response.StatusCode} {url}");
    return response.IsSuccessStatusCode ? 0 : 1;
}
catch (Exception ex)
{
    Console.WriteLine($"sin respuesta de {url}: {ex.GetType().Name}");
    return 1;
}
