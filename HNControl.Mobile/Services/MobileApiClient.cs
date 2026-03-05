using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HNControl.Mobile.Models;

namespace HNControl.Mobile.Services;

public sealed class MobileApiClient
{
    private readonly HttpClient _http;
    private readonly MobileApiSettings _settings;
    private readonly AuthSession _session;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MobileApiClient(HttpClient http, MobileApiSettings settings, AuthSession session)
    {
        _http = http;
        _settings = settings;
        _session = session;
        _http.Timeout = TimeSpan.FromSeconds(25);
    }

    public async Task<TResponse> PostJsonAsync<TRequest, TResponse>(string relativeUrl, TRequest payload, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUrl(relativeUrl))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }

        return JsonSerializer.Deserialize<TResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo leer la respuesta del servidor.");
    }

    public async Task<TResponse> GetJsonAsync<TResponse>(string relativeUrl, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl(relativeUrl));
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }

        return JsonSerializer.Deserialize<TResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo leer la respuesta del servidor.");
    }

    public async Task PostAsync(string relativeUrl, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUrl(relativeUrl));
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }
    }

    public async Task<TResponse> PutJsonAsync<TRequest, TResponse>(string relativeUrl, TRequest payload, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, BuildUrl(relativeUrl))
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }

        return JsonSerializer.Deserialize<TResponse>(raw, JsonOptions)
            ?? throw new InvalidOperationException("No se pudo leer la respuesta del servidor.");
    }

    public async Task DeleteAsync(string relativeUrl, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, BuildUrl(relativeUrl));
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }
    }

    public async Task<byte[]> GetBytesAsync(string relativeUrl, bool withAuth = true)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl(relativeUrl));
        AddAuth(req, withAuth);
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ResolveError(raw, (int)res.StatusCode));
        }

        return await res.Content.ReadAsByteArrayAsync();
    }

    private string BuildUrl(string relativeUrl)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        var path = relativeUrl.TrimStart('/');
        return $"{baseUrl}/{path}";
    }

    private void AddAuth(HttpRequestMessage req, bool withAuth)
    {
        if (!withAuth)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.Token))
        {
            throw new InvalidOperationException("Sesion expirada. Inicia sesion nuevamente.");
        }

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
    }

    private static string ResolveError(string raw, int statusCode)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<ApiMessageDto>(raw, JsonOptions);
            if (!string.IsNullOrWhiteSpace(msg?.Message))
            {
                return msg.Message;
            }
        }
        catch
        {
            // Ignorar parseo para devolver error base.
        }

        return statusCode switch
        {
            401 => "No autorizado. Verifica tu usuario y password.",
            404 => "No se encontro el recurso solicitado.",
            409 => "Conflicto de datos en la operacion.",
            _ => "Error de comunicacion con la API."
        };
    }
}
