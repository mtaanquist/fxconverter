using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Concurrent;
using System.Globalization;

public class ConverterModel : PageModel
{
    private static readonly ConcurrentDictionary<string, CultureInfo?> _cultureByCurrency = new();

    private static CultureInfo? FindCultureForCurrency(string currencyCode) =>
        _cultureByCurrency.GetOrAdd(currencyCode, code =>
            CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .FirstOrDefault(c =>
                {
                    try { return new RegionInfo(c.Name).ISOCurrencySymbol == code; }
                    catch { return false; }
                }));

    private readonly IExchangeRateService _rateService;

    public ConverterModel(IExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CultureTag { get; set; } = string.Empty;
    public string ResultValue { get; set; } = string.Empty;
    public string OgDescription { get; set; } = string.Empty;
    public string OgImage { get; set; } = string.Empty;
    public string RateDate { get; set; } = string.Empty;
    public bool HasError { get; set; }

    public async Task OnGetAsync(string from, string to, decimal amount)
    {
        From = from.ToUpperInvariant();
        To = to.ToUpperInvariant();
        Amount = amount;

        var toCulture = FindCultureForCurrency(To);
        CultureTag = toCulture?.Name ?? string.Empty;

        var result = await _rateService.GetRateAsync(From, To);
        if (result is null)
        {
            HasError = true;
            ResultValue = "Unavailable";
            return;
        }

        var converted = amount * result.Value.Rate;
        ResultValue = toCulture is not null
            ? converted.ToString("C2", toCulture)
            : $"{converted:N2} {To}";
        RateDate = result.Value.Date;

        OgDescription = $"{amount:0.##} {From} = {ResultValue} (Updated: {RateDate})";
        OgImage = $"{Request.Scheme}://{Request.Host}/preview/image?from={From}&to={To}&amount={amount.ToString(CultureInfo.InvariantCulture)}";
    }
}