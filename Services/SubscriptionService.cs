using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using SubscriptionOverlayWidget.Models;

namespace SubscriptionOverlayWidget.Services;

public sealed class SubscriptionService
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://ai.wormsoft.ru/")
    };

    public async Task<SubscriptionLimitInfo> FetchSummaryAsync(string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/gpt/subscription-limit");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

            using var response = await Client.SendAsync(request);
            var rawJson = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new SubscriptionLimitInfo
                {
                    StatusText = "401 Unauthorized. Проверьте API key.",
                    ServerDateUtc = response.Headers.Date?.UtcDateTime
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new SubscriptionLimitInfo
                {
                    StatusText = $"Ошибка API: {(int)response.StatusCode} {response.ReasonPhrase}",
                    RawResponse = rawJson,
                    ServerDateUtc = response.Headers.Date?.UtcDateTime
                };
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return new SubscriptionLimitInfo
                {
                    StatusText = "Пустой ответ от API.",
                    ServerDateUtc = response.Headers.Date?.UtcDateTime
                };
            }

            using var document = JsonDocument.Parse(rawJson);
            return FormatResponse(document.RootElement, response.Headers.Date?.UtcDateTime);
        }
        catch (HttpRequestException ex)
        {
            return new SubscriptionLimitInfo
            {
                StatusText = $"Сетевая ошибка: {ex.Message}"
            };
        }
        catch (JsonException)
        {
            return new SubscriptionLimitInfo
            {
                StatusText = "Не удалось разобрать JSON ответ API."
            };
        }
        catch (Exception ex)
        {
            return new SubscriptionLimitInfo
            {
                StatusText = $"Ошибка: {ex.Message}"
            };
        }
    }

    private static SubscriptionLimitInfo FormatResponse(JsonElement root, DateTime? serverDateUtc)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            var hasType = root.TryGetProperty("subcriptionType", out var typeElement);
            var hasLimit = root.TryGetProperty("subcriptionLimit", out var limitElement);

            if (hasType && hasLimit)
            {
                var now = DateTime.Now;
                var remainingText = limitElement.ToString();
                long.TryParse(remainingText, out var remainingNumeric);
                return new SubscriptionLimitInfo
                {
                    IsSuccess = true,
                    SubscriptionType = typeElement.GetString() ?? "unknown",
                    RemainingLimit = remainingText,
                    RemainingLimitValue = remainingNumeric,
                    UpdatedAt = now,
                    ServerDateUtc = serverDateUtc,
                    StatusText = $"Обновлено в {now:HH:mm:ss}",
                    RawResponse = root.ToString()
                };
            }
        }

        return new SubscriptionLimitInfo
        {
            StatusText = "Получен неожиданный ответ API.",
            RawResponse = root.ToString(),
            ServerDateUtc = serverDateUtc
        };
    }
}
