using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Threading;

public class ConverterModel : PageModel
{
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
        CultureTag = Thread.CurrentThread.CurrentCulture.Name;

        var result = await _rateService.GetRateAsync(From, To);
        if (result is null)
        {
            HasError = true;
            ResultValue = "Unavailable";
            return;
        }

        var converted = amount * result.Value.Rate;
        ResultValue = $"{converted:N2} {To}";
        RateDate = result.Value.Date;

        OgDescription = $"{amount:0.##} {From} = {ResultValue} (Updated: {RateDate})";
        OgImage = $"{Request.Scheme}://{Request.Host}/preview/image?from={From}&to={To}&amount={amount.ToString(CultureInfo.InvariantCulture)}";
    }
}