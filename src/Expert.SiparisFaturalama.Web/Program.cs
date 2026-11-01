using Expert.SiparisFaturalama.Core.Ocr;
using Expert.SiparisFaturalama.Core.Services;
using Expert.SiparisFaturalama.Core.Vega;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    return new OpenAiVisionOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("EXPERT_OPENAI_API_KEY")
                 ?? cfg["OpenAI:ApiKey"],
        Endpoint = cfg["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
        Model = cfg["OpenAI:Model"] ?? "gpt-4o-mini"
    };
});
builder.Services.AddHttpClient<OpenAiVisionBelgeOkuyucu>();
builder.Services.AddSingleton<DemoDefterBelgeOkuyucu>();
builder.Services.AddSingleton<IBelgeOkuyucu>(sp =>
{
    var vision = sp.GetRequiredService<OpenAiVisionBelgeOkuyucu>();
    var demo = sp.GetRequiredService<DemoDefterBelgeOkuyucu>();
    return new AkilliBelgeOkuyucu(vision, demo);
});
builder.Services.AddSingleton<VegaBelgeAktarici>();
builder.Services.AddSingleton<BelgeServisi>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var samplesPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "samples"));
if (Directory.Exists(samplesPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(samplesPath),
        RequestPath = "/samples"
    });
}

app.MapGet("/api/health", (BelgeServisi belgeler) => Results.Ok(new
{
    status = "ok",
    okuyucu = belgeler.OkuyucuAdi,
    uygulama = "Expert.SiparisFaturalama"
}));

app.MapGet("/api/belgeler", (BelgeServisi belgeler) =>
    Results.Ok(belgeler.Listele().Select(Ozet)));

app.MapGet("/api/belgeler/{id:guid}", (Guid id, BelgeServisi belgeler) =>
{
    var belge = belgeler.Getir(id);
    return belge is null ? Results.NotFound() : Results.Ok(belge);
});

app.MapPost("/api/belgeler/oku", async (HttpRequest request, BelgeServisi belgeler) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { hata = "multipart/form-data bekleniyor." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("belge") ?? form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { hata = "Belge dosyası gerekli." });
    }

    await using var stream = file.OpenReadStream();
    var belge = await belgeler.YukleVeOkuAsync(stream, file.FileName, file.ContentType);
    return Results.Ok(belge);
});

app.MapPost("/api/belgeler/demo", async (BelgeServisi belgeler) =>
{
    var samplePath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "samples", "ornek-defter.jpg");
    samplePath = Path.GetFullPath(samplePath);
    if (!File.Exists(samplePath))
    {
        await using var empty = new MemoryStream(new byte[] { 1 });
        var fallback = await belgeler.YukleVeOkuAsync(empty, "ornek-defter.jpg", "image/jpeg");
        return Results.Ok(fallback);
    }

    await using var fs = File.OpenRead(samplePath);
    var belge = await belgeler.YukleVeOkuAsync(fs, "ornek-defter.jpg", "image/jpeg");
    return Results.Ok(belge);
});

app.MapPut("/api/belgeler/{id:guid}", (Guid id, BelgeGuncellemeIstek istek, BelgeServisi belgeler) =>
{
    try
    {
        return Results.Ok(belgeler.Guncelle(id, istek));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.MapPost("/api/belgeler/{id:guid}/onayla", (Guid id, BelgeServisi belgeler) =>
{
    try
    {
        return Results.Ok(belgeler.Onayla(id));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.MapPost("/api/belgeler/{id:guid}/vega", (Guid id, string? format, BelgeServisi belgeler) =>
{
    try
    {
        var sonuc = belgeler.VegayaHazirla(id, format ?? "xml");
        return Results.Ok(sonuc);
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { hata = ex.Message });
    }
});

app.MapGet("/api/belgeler/{id:guid}/vega/indir", (Guid id, BelgeServisi belgeler) =>
{
    var aktarim = belgeler.AktarimGetir(id);
    if (aktarim is null)
    {
        return Results.NotFound(new { hata = "Önce Vega aktarımı oluşturun." });
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(aktarim.Icerik);
    return Results.File(bytes, aktarim.Format, aktarim.DosyaAdi);
});

app.Run();

static object Ozet(Expert.SiparisFaturalama.Core.Models.OkunanBelge b) => new
{
    b.Id,
    b.KaynakDosyaAdi,
    b.CariUnvan,
    b.BelgeTuru,
    b.Durum,
    b.OlusturmaZamani,
    satirSayisi = b.AktifSatirlar.Count(),
    genelToplam = b.GenelToplam
};

public partial class Program;
