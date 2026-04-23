var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapGet("/api/rate/{from}/{to}", async (string from, string to, IExchangeRateService rateService) =>
{
    var result = await rateService.GetRateAsync(from, to);
    if (result is null) return Results.NotFound();
    return Results.Json(new { rate = result.Value.Rate, date = result.Value.Date });
});

app.MapGet("/preview/image", async (string from, string to, double amount, IExchangeRateService rateService) =>
{
    var fromUpper = from.ToUpperInvariant();
    var toUpper = to.ToUpperInvariant();
    var result = await rateService.GetRateAsync(fromUpper, toUpper);

    string svg;
    if (result is null)
    {
        svg = BuildSvg($"{amount} {fromUpper} → {toUpper}", "Error fetching rate", "Could not retrieve exchange rate", "#e05252");
    }
    else
    {
        var converted = amount * result.Value.Rate;
        var line1 = $"{amount:0.##} {fromUpper} → {toUpper}";
        var line2 = $"{converted:N2} {toUpper}";
        var line3 = $"1 {fromUpper} = {result.Value.Rate:G6} {toUpper}  ·  {result.Value.Date}";
        svg = BuildSvg(line1, line2, line3, "#58a6ff");
    }

    return Results.Content(svg, "image/svg+xml");
});

static string BuildSvg(string line1, string line2, string line3, string accentColor) => $"""
    <svg xmlns="http://www.w3.org/2000/svg" width="520" height="160" viewBox="0 0 520 160">
      <rect width="520" height="160" rx="12" fill="#161b22"/>
      <rect x="0" y="0" width="4" height="160" rx="2" fill="{accentColor}"/>
      <text x="24" y="36" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
            font-size="15" fill="#8b949e">{System.Web.HttpUtility.HtmlEncode(line1)}</text>
      <text x="24" y="98" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
            font-size="52" font-weight="700" fill="{accentColor}">{System.Web.HttpUtility.HtmlEncode(line2)}</text>
      <text x="24" y="138" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif"
            font-size="13" fill="#8b949e">{System.Web.HttpUtility.HtmlEncode(line3)}</text>
    </svg>
    """;

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
