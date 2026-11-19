using System.Globalization;
using System.Net;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using UltracarInventoryApi.PerformanceTests.Configuration;

namespace UltracarInventoryApi.PerformanceTests.Scenarios;

internal static class PartSearchRequest
{
    public const string Path = "/api/v1/pecas?busca=";

    public static HttpRequestMessage Create(int workshopId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("X-Oficina-Id", workshopId.ToString(CultureInfo.InvariantCulture));
        return request;
    }

    public static async Task<IResponse> ExecuteAsync(HttpClient client, int workshopId)
    {
        using var request = Create(workshopId);
        try
        {
            // ResponseContentRead includes the complete response body in the measurement.
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode != HttpStatusCode.OK)
                return Response.Fail(statusCode: ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));

            var body = await response.Content.ReadAsByteArrayAsync();
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array
                || items.GetArrayLength() != PerformanceSettings.PageSize)
                return Response.Fail(statusCode: "INVALID_PAGE");

            return Response.Ok(statusCode: "200", sizeBytes: body.Length);
        }
        catch (HttpRequestException) { return Response.Fail(statusCode: "HTTP_ERROR"); }
        catch (OperationCanceledException) { return Response.Fail(statusCode: "TIMEOUT"); }
        catch (JsonException) { return Response.Fail(statusCode: "INVALID_JSON"); }
        catch (InvalidOperationException) { return Response.Fail(statusCode: "INVALID_PAGE"); }
    }

    public static async Task ValidateWorkshopsAsync(HttpClient client)
    {
        for (var workshopId = 1; workshopId <= PerformanceSettings.Workshops; workshopId++)
        {
            using var request = Create(workshopId);
            using var response = await client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Pré-validação da oficina {workshopId}: HTTP {(int)response.StatusCode}. Verifique a URL final e o cabeçalho X-Oficina-Id.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
            var items = document.RootElement.GetProperty("items");
            if (items.GetArrayLength() != PerformanceSettings.PageSize
                || string.IsNullOrEmpty(document.RootElement.GetProperty("nextCursor").GetString()))
                throw new InvalidOperationException($"Página inicial inválida para a oficina {workshopId}.");

            var offset = (workshopId - 1L) * PerformanceSettings.PartsPerWorkshop;
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                var expectedId = offset + ++index;
                var expectedCode = "PERF-" + expectedId.ToString("D7", CultureInfo.InvariantCulture);
                if (item.GetProperty("id").GetInt64() != expectedId
                    || item.GetProperty("codigo").GetString() != expectedCode
                    || item.GetProperty("estoqueFisico").GetInt64() != 200
                    || item.GetProperty("estoqueReservado").GetInt64() != 0
                    || item.GetProperty("saldoDisponivel").GetInt64() != 200)
                    throw new InvalidOperationException($"A API temporária não retornou os dados esperados do seed para a oficina {workshopId}.");
            }
        }
    }
}
