using System.Collections.Concurrent;

public interface IExchangeRateService
{
    Task<(decimal Rate, string Date)?> GetRateAsync(string from, string to);
}

public class ExchangeRateService : IExchangeRateService
{
    private record CacheEntry(decimal Rate, string Date, DateTime FetchedAt);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExchangeRateService> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public ExchangeRateService(IHttpClientFactory httpClientFactory, ILogger<ExchangeRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(decimal Rate, string Date)?> GetRateAsync(string from, string to)
    {
        var key = $"{from.ToUpperInvariant()}:{to.ToUpperInvariant()}";

        if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.FetchedAt < CacheTtl)
            return (entry.Rate, entry.Date);

        await _semaphore.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out entry) && DateTime.UtcNow - entry.FetchedAt < CacheTtl)
                return (entry.Rate, entry.Date);

            var client = _httpClientFactory.CreateClient("frankfurter");
            var response = await client.GetFromJsonAsync<ExchangeRateResponse>(
                $"v2/rate/{from.ToUpperInvariant()}/{to.ToUpperInvariant()}");

            if (response is null) return null;

            var newEntry = new CacheEntry(response.Rate, response.Date, DateTime.UtcNow);
            _cache[key] = newEntry;
            return (response.Rate, response.Date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch exchange rate for {From}/{To}", from, to);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public record ExchangeRateResponse(string Date, decimal Rate);