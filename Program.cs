using SkiaSharp;

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

app.MapGet("/preview/image", async (HttpContext ctx, string from, string to, double amount, IExchangeRateService rateService) =>
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
        var converted = amount * result.Value.Rate;
        var line1 = $"{amount:0.##} {fromUpper} → {toUpper}";
        var line2 = $"{converted:N2} {toUpper}";
        var line3 = $"1 {fromUpper} = {result.Value.Rate:G6} {toUpper}  ·  {result.Value.Date}";
        png = BuildPng(line1, line2, line3, "#58a6ff");
    }

    ctx.Response.Headers.CacheControl = "public, max-age=3600";
    ctx.Response.ContentType = "image/png";
    await ctx.Response.Body.WriteAsync(png);
});

static byte[] BuildPng(string line1, string line2, string line3, string accentHex)
{
    const int W = 520, H = 160;
    using var surface = SKSurface.Create(new SKImageInfo(W, H));
    var canvas = surface.Canvas;

    canvas.Clear(SKColor.Parse("#161b22"));

    var accent = SKColor.Parse(accentHex);
    var gray   = SKColor.Parse("#8b949e");

    using var barPaint = new SKPaint { Color = accent };
    canvas.DrawRect(new SKRect(0, 0, 4, H), barPaint);

    var tf     = SKTypeface.FromFamilyName("DejaVu Sans") ?? SKTypeface.Default;
    var tfBold = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold) ?? tf;

    using var smallFont = new SKFont(tf, 14);
    using var largeFont = new SKFont(tfBold, 50);
    using var tinyFont  = new SKFont(tf, 12);

    using var grayPaint   = new SKPaint { Color = gray,   IsAntialias = true };
    using var accentPaint = new SKPaint { Color = accent, IsAntialias = true };

    canvas.DrawText(line1, 24, 36,  smallFont, grayPaint);
    canvas.DrawText(line2, 24, 98,  largeFont, accentPaint);
    canvas.DrawText(line3, 24, 138, tinyFont,  grayPaint);

    using var img  = surface.Snapshot();
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
