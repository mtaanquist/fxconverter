using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ConverterModel : PageModel
{
    [FromServices] public IExchangeRateService RateService { get; set; } = null!;

    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string ResultValue { get; set; } = string.Empty;
    public string OgDescription { get; set; } = string.Empty;
    public string OgImage { get; set; } = string.Empty;
    public string RateDate { get; set; } = string.Empty;
    public bool HasError { get; set; }

    public async Task OnGetAsync(string from, string to, double amount)
    {
        From = from.ToUpperInvariant();
        To = to.ToUpperInvariant();
        Amount = amount;

        var result = await RateService.GetRateAsync(From, To);
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
        OgImage = $"{Request.Scheme}://{Request.Host}/preview/image?from={From}&to={To}&amount={amount}";
    }
}