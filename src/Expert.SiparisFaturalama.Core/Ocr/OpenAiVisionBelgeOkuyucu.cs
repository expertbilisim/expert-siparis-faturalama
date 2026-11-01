using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Expert.SiparisFaturalama.Core.Models;

namespace Expert.SiparisFaturalama.Core.Ocr;

/// <summary>
/// OpenAI uyumlu vision API ile el yazısı / tarama belgelerini satır satır okur.
/// EXPERT_OPENAI_API_KEY veya OpenAI:ApiKey ayarı gerekir.
/// </summary>
public sealed class OpenAiVisionBelgeOkuyucu : IBelgeOkuyucu
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiVisionOptions _options;

    public OpenAiVisionBelgeOkuyucu(HttpClient httpClient, OpenAiVisionOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Ad => "OpenAiVision";

    public bool Yapilandirilmis =>
        !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<OkunanBelge> OkuAsync(
        Stream belgesi,
        string dosyaAdi,
        string? icerikTuru = null,
        CancellationToken cancellationToken = default)
    {
        if (!Yapilandirilmis)
        {
            throw new InvalidOperationException(
                "OpenAI API anahtarı yok. Demo okuyucu kullanılır veya EXPERT_OPENAI_API_KEY tanımlayın.");
        }

        await using var ms = new MemoryStream();
        await belgesi.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        var mime = ResolveMime(dosyaAdi, icerikTuru);
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

        var requestBody = new
        {
            model = _options.Model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "Sen Türkçe el yazısı sipariş/fatura defterlerini okuyan bir muhasebe asistanısın. " +
                        "Görseldeki ürün satırlarını JSON olarak çıkar. Sadece JSON döndür."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text =
                                """
                                Bu belgeyi satır satır oku. JSON şeması:
                                {
                                  "cariUnvan": string|null,
                                  "belgeTarihi": "yyyy-MM-dd"|null,
                                  "belgeNo": string|null,
                                  "okumaNotu": string|null,
                                  "satirlar": [
                                    {
                                      "siraNo": number,
                                      "urunKodu": string,
                                      "urunAdi": string,
                                      "miktar": number,
                                      "birim": string,
                                      "birimFiyat": number,
                                      "tutar": number,
                                      "musteriNotu": string|null,
                                      "grupNotu": string|null,
                                      "guvenSkoru": number
                                    }
                                  ]
                                }
                                Kurallar:
                                - Toner/drum kodlarını urunKodu alanına yaz.
                                - Adet bilgisini miktar yap; yoksa 1.
                                - Birim fiyat x miktar tutarsızsa guvenSkoru düşür.
                                - Yanındaki kişi/firma notlarını musteriNotu'na yaz.
                                - guvenSkoru 0-1 arası olsun.
                                """
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = dataUrl }
                        }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Vision API hata {(int)response.StatusCode}: {raw}");
        }

        using var doc = JsonDocument.Parse(raw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new InvalidOperationException("Vision API içerik döndürmedi.");

        var parsed = JsonSerializer.Deserialize<VisionBelgeDto>(content, JsonOptions)
                     ?? throw new InvalidOperationException("Vision JSON ayrıştırılamadı.");

        var belge = new OkunanBelge
        {
            KaynakDosyaAdi = dosyaAdi,
            KaynakIcerikTuru = mime,
            CariUnvan = parsed.CariUnvan,
            BelgeNo = parsed.BelgeNo,
            BelgeTarihi = TryParseDate(parsed.BelgeTarihi),
            OkumaNotu = parsed.OkumaNotu,
            Durum = BelgeDurumu.OnayBekliyor,
            Satirlar = parsed.Satirlar.Select(s => new BelgeSatiri
            {
                SiraNo = s.SiraNo,
                UrunKodu = s.UrunKodu?.Trim() ?? string.Empty,
                UrunAdi = string.IsNullOrWhiteSpace(s.UrunAdi) ? s.UrunKodu ?? string.Empty : s.UrunAdi,
                Miktar = s.Miktar <= 0 ? 1 : s.Miktar,
                Birim = string.IsNullOrWhiteSpace(s.Birim) ? "Adet" : s.Birim!,
                BirimFiyat = s.BirimFiyat,
                Tutar = s.Tutar > 0 ? s.Tutar : Math.Round(s.Miktar * s.BirimFiyat, 2),
                MusteriNotu = s.MusteriNotu,
                GrupNotu = s.GrupNotu,
                GuvenSkoru = Math.Clamp(s.GuvenSkoru, 0, 1),
                Onaylandi = false
            }).ToList()
        };

        return belge;
    }

    private static DateTime? TryParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.Date : null;

    private static string ResolveMime(string dosyaAdi, string? icerikTuru)
    {
        if (!string.IsNullOrWhiteSpace(icerikTuru) && icerikTuru.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return icerikTuru;
        }

        var ext = Path.GetExtension(dosyaAdi).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    private sealed class VisionBelgeDto
    {
        public string? CariUnvan { get; set; }
        public string? BelgeTarihi { get; set; }
        public string? BelgeNo { get; set; }
        public string? OkumaNotu { get; set; }
        public List<VisionSatirDto> Satirlar { get; set; } = [];
    }

    private sealed class VisionSatirDto
    {
        public int SiraNo { get; set; }
        public string? UrunKodu { get; set; }
        public string? UrunAdi { get; set; }
        public decimal Miktar { get; set; }
        public string? Birim { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal Tutar { get; set; }
        public string? MusteriNotu { get; set; }
        public string? GrupNotu { get; set; }
        public double GuvenSkoru { get; set; } = 0.5;
    }
}

public sealed class OpenAiVisionOptions
{
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
}
