using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Expert.SiparisFaturalama.Core.Models;

namespace Expert.SiparisFaturalama.Core.Vega;

public sealed class VegaAktarimSonucu
{
    public required Guid BelgeId { get; init; }
    public required string Format { get; init; }
    public required string Icerik { get; init; }
    public required string DosyaAdi { get; init; }
    public DateTime OlusturmaZamani { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Onaylanmış belgeyi Vega'ya aktarılabilir XML / JSON paketlerine çevirir.
/// Canlı SOAP gönderimi müşteri Session/Security bilgileriyle ayrıca bağlanacak.
/// </summary>
public sealed class VegaBelgeAktarici
{
    public VegaAktarimSonucu XmlOlustur(OkunanBelge belge)
    {
        EnsureOnayli(belge);

        var root = new XElement("VegaBelge",
            new XAttribute("Kaynak", "Expert.SiparisFaturalama"),
            new XAttribute("Versiyon", "1.0"),
            new XElement("Baslik",
                new XElement("BelgeId", belge.Id),
                new XElement("BelgeTuru", belge.BelgeTuru),
                new XElement("BelgeNo", belge.BelgeNo ?? string.Empty),
                new XElement("BelgeTarihi", (belge.BelgeTarihi ?? DateTime.Today).ToString("yyyy-MM-dd")),
                new XElement("CariKodu", belge.CariKodu ?? string.Empty),
                new XElement("CariUnvan", belge.CariUnvan ?? string.Empty),
                new XElement("KaynakDosya", belge.KaynakDosyaAdi ?? string.Empty),
                new XElement("GenelToplam", F(belge.AktifSatirlar.Sum(s => s.Tutar))),
                new XElement("ParaBirimi", "TRY")),
            new XElement("Satirlar",
                belge.AktifSatirlar.Select(s =>
                    new XElement("Satir",
                        new XElement("SiraNo", s.SiraNo),
                        new XElement("StokKodu", s.UrunKodu),
                        new XElement("StokAdi", s.UrunAdi),
                        new XElement("Miktar", F(s.Miktar)),
                        new XElement("Birim", s.Birim),
                        new XElement("BirimFiyat", F(s.BirimFiyat)),
                        new XElement("Tutar", F(s.Tutar)),
                        new XElement("MusteriNotu", s.MusteriNotu ?? string.Empty),
                        new XElement("GrupNotu", s.GrupNotu ?? string.Empty)))));

        var xml = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        var sb = new StringBuilder();
        using (var writer = new Utf8StringWriter(sb))
        {
            xml.Save(writer);
        }

        return new VegaAktarimSonucu
        {
            BelgeId = belge.Id,
            Format = "application/xml",
            DosyaAdi = $"vega-belge-{belge.Id:N}.xml",
            Icerik = sb.ToString()
        };
    }

    public VegaAktarimSonucu JsonOlustur(OkunanBelge belge)
    {
        EnsureOnayli(belge);

        var payload = new
        {
            kaynak = "Expert.SiparisFaturalama",
            versiyon = "1.0",
            baslik = new
            {
                belgeId = belge.Id,
                belgeTuru = belge.BelgeTuru,
                belgeNo = belge.BelgeNo,
                belgeTarihi = (belge.BelgeTarihi ?? DateTime.Today).ToString("yyyy-MM-dd"),
                cariKodu = belge.CariKodu,
                cariUnvan = belge.CariUnvan,
                genelToplam = belge.AktifSatirlar.Sum(s => s.Tutar),
                paraBirimi = "TRY"
            },
            satirlar = belge.AktifSatirlar.Select(s => new
            {
                siraNo = s.SiraNo,
                stokKodu = s.UrunKodu,
                stokAdi = s.UrunAdi,
                miktar = s.Miktar,
                birim = s.Birim,
                birimFiyat = s.BirimFiyat,
                tutar = s.Tutar,
                musteriNotu = s.MusteriNotu,
                grupNotu = s.GrupNotu
            })
        };

        return new VegaAktarimSonucu
        {
            BelgeId = belge.Id,
            Format = "application/json",
            DosyaAdi = $"vega-belge-{belge.Id:N}.json",
            Icerik = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            })
        };
    }

    private static void EnsureOnayli(OkunanBelge belge)
    {
        if (belge.Durum is not (BelgeDurumu.Onaylandi or BelgeDurumu.VegayaAktarildi))
        {
            throw new InvalidOperationException("Belge onaylanmadan Vega aktarımı yapılamaz.");
        }

        if (!belge.AktifSatirlar.Any())
        {
            throw new InvalidOperationException("Aktarılacak satır yok.");
        }
    }

    private static string F(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed class Utf8StringWriter : StringWriter
    {
        public Utf8StringWriter(StringBuilder sb) : base(sb) { }
        public override Encoding Encoding => Encoding.UTF8;
    }
}
