public interface IExchangeRateService
{
    Task<(double Rate, string Date)?> GetRateAsync(string from, string to);
}

public class ExchangeRateService : IExchangeRateService
{
    private record CacheEntry(double Rate, string Date, DateTime FetchedAt);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _lock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public ExchangeRateService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<(double Rate, string Date)?> GetRateAsync(string from, string to)
    {
        var key = $"{from.ToUpperInvariant()}:{to.ToUpperInvariant()}";

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.FetchedAt < CacheTtl)
                return (entry.Rate, entry.Date);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetFromJsonAsync<ExchangeRateResponse>(
                $"https://api.frankfurter.dev/v2/rate/{from.ToUpperInvariant()}/{to.ToUpperInvariant()}");

            if (response is null) return null;

            var newEntry = new CacheEntry(response.Rate, response.Date, DateTime.UtcNow);
            lock (_lock)
            {
                _cache[key] = newEntry;
            }
            return (response.Rate, response.Date);
        }
        catch
        {
            return null;
        }
    }
}

public record ExchangeRateResponse(string Date, string Base, string Quote, double Rate);