using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("frankfurter", c =>
{
    c.BaseAddress = new Uri("https://api.frankfurter.dev/");
    c.Timeout = TimeSpan.FromSeconds(5);
});
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

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()]
});

app.UseRouting();

app.UseAuthorization();

app.MapGet("/api/rate/{from:regex(^[a-zA-Z]{{3}}$)}/{to:regex(^[a-zA-Z]{{3}}$)}", async (string from, string to, IExchangeRateService rateService) =>
{
    var result = await rateService.GetRateAsync(from, to);
    if (result is null) return Results.NotFound();
    return Results.Json(new { rate = result.Value.Rate, date = result.Value.Date });
});

app.MapGet("/preview/image", async (HttpContext ctx, string from, string to, decimal amount, IExchangeRateService rateService, IMemoryCache cache) =>
{
    var fromUpper = from.ToUpperInvariant();
    var toUpper = to.ToUpperInvariant();
    var result = await rateService.GetRateAsync(fromUpper, toUpper);

    byte[] png;
    if (result is null)
    {
        png = BuildPng($"{amount} {fromUpper} → {toUpper}", "Error fetching rate", "Could not retrieve exchange rate", "#e05252");
    }
    else
    {
        var cacheKey = $"png:{fromUpper}:{toUpper}:{amount}:{result.Value.Date}";
        if (!cache.TryGetValue(cacheKey, out png!))
        {
            var inv = CultureInfo.InvariantCulture;
            var toCulture = FindCultureForCurrency(toUpper);
            var converted = amount * result.Value.Rate;
            var line1 = $"{amount.ToString("0.##", inv)} {fromUpper} → {toUpper}";
            var line2 = toCulture is not null
                ? converted.ToString("C2", toCulture)
                : $"{converted.ToString("N2", inv)} {toUpper}";
            var line3 = $"1 {fromUpper} = {result.Value.Rate.ToString("G6", inv)} {toUpper}  ·  {result.Value.Date}";
            png = BuildPng(line1, line2, line3, "#58a6ff");

            var midnight = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
            cache.Set(cacheKey, png, new MemoryCacheEntryOptions { AbsoluteExpiration = midnight });
        }
    }

    ctx.Response.Headers.CacheControl = "public, max-age=86400";
    ctx.Response.ContentType = "image/png";
    await ctx.Response.Body.WriteAsync(png);
});

static byte[] BuildPng(string line1, string line2, string line3, string accentHex)
{
    const int W = 1040, H = 320;
    using var surface = SKSurface.Create(new SKImageInfo(W, H));
    var canvas = surface.Canvas;

    canvas.Clear(SKColor.Parse("#161b22"));

    var accent = SKColor.Parse(accentHex);
    var gray   = SKColor.Parse("#8b949e");

    using var barPaint = new SKPaint { Color = accent };
    canvas.DrawRect(new SKRect(0, 0, 8, H), barPaint);

    var tf     = SKTypeface.FromFamilyName("DejaVu Sans") ?? SKTypeface.Default;
    var tfBold = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold) ?? tf;

    using var smallFont = new SKFont(tf, 28);
    using var largeFont = new SKFont(tfBold, 100);
    using var tinyFont  = new SKFont(tf, 24);

    using var grayPaint   = new SKPaint { Color = gray,   IsAntialias = true };
    using var accentPaint = new SKPaint { Color = accent, IsAntialias = true };

    canvas.DrawText(line1, 48, 72,  smallFont, grayPaint);
    canvas.DrawText(line2, 48, 196, largeFont, accentPaint);
    canvas.DrawText(line3, 48, 276, tinyFont,  grayPaint);

    using var img  = surface.Snapshot();
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static CultureInfo? FindCultureForCurrency(string currencyCode) =>
    CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .FirstOrDefault(c =>
        {
            try { return new RegionInfo(c.Name).ISOCurrencySymbol == currencyCode; }
            catch { return false; }
        });
