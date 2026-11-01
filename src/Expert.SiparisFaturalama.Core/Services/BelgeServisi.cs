using Expert.SiparisFaturalama.Core.Models;
using Expert.SiparisFaturalama.Core.Ocr;
using Expert.SiparisFaturalama.Core.Vega;

namespace Expert.SiparisFaturalama.Core.Services;

public sealed class BelgeServisi
{
    private readonly IBelgeOkuyucu _okuyucu;
    private readonly VegaBelgeAktarici _vega;
    private readonly Dictionary<Guid, OkunanBelge> _belgeler = new();
    private readonly Dictionary<Guid, VegaAktarimSonucu> _aktarimlar = new();
    private readonly object _lock = new();

    public BelgeServisi(IBelgeOkuyucu okuyucu, VegaBelgeAktarici vega)
    {
        _okuyucu = okuyucu;
        _vega = vega;
    }

    public string OkuyucuAdi => _okuyucu.Ad;

    public IReadOnlyList<OkunanBelge> Listele()
    {
        lock (_lock)
        {
            return _belgeler.Values
                .OrderByDescending(b => b.OlusturmaZamani)
                .ToList();
        }
    }

    public OkunanBelge? Getir(Guid id)
    {
        lock (_lock)
        {
            return _belgeler.TryGetValue(id, out var belge) ? Clone(belge) : null;
        }
    }

    public async Task<OkunanBelge> YukleVeOkuAsync(
        Stream stream,
        string dosyaAdi,
        string? icerikTuru = null,
        CancellationToken cancellationToken = default)
    {
        var belge = await _okuyucu.OkuAsync(stream, dosyaAdi, icerikTuru, cancellationToken);
        belge.Durum = BelgeDurumu.OnayBekliyor;

        lock (_lock)
        {
            _belgeler[belge.Id] = belge;
        }

        return Clone(belge);
    }

    public OkunanBelge Guncelle(Guid id, BelgeGuncellemeIstek istek)
    {
        lock (_lock)
        {
            if (!_belgeler.TryGetValue(id, out var belge))
            {
                throw new KeyNotFoundException("Belge bulunamadı.");
            }

            if (belge.Durum == BelgeDurumu.VegayaAktarildi)
            {
                throw new InvalidOperationException("Vega'ya aktarılmış belge düzenlenemez.");
            }

            if (istek.CariUnvan is not null) belge.CariUnvan = istek.CariUnvan;
            if (istek.CariKodu is not null) belge.CariKodu = istek.CariKodu;
            if (istek.BelgeNo is not null) belge.BelgeNo = istek.BelgeNo;
            if (istek.BelgeTuru is not null) belge.BelgeTuru = istek.BelgeTuru;
            if (istek.BelgeTarihi.HasValue) belge.BelgeTarihi = istek.BelgeTarihi;

            if (istek.Satirlar is { Count: > 0 })
            {
                foreach (var g in istek.Satirlar)
                {
                    var satir = belge.Satirlar.FirstOrDefault(s => s.Id == g.Id);
                    if (satir is null) continue;

                    if (g.UrunKodu is not null) satir.UrunKodu = g.UrunKodu;
                    if (g.UrunAdi is not null) satir.UrunAdi = g.UrunAdi;
                    if (g.Miktar.HasValue) satir.Miktar = g.Miktar.Value;
                    if (g.Birim is not null) satir.Birim = g.Birim;
                    if (g.BirimFiyat.HasValue) satir.BirimFiyat = g.BirimFiyat.Value;
                    if (g.Tutar.HasValue) satir.Tutar = g.Tutar.Value;
                    if (g.MusteriNotu is not null) satir.MusteriNotu = g.MusteriNotu;
                    if (g.GrupNotu is not null) satir.GrupNotu = g.GrupNotu;
                    if (g.Silindi.HasValue) satir.Silindi = g.Silindi.Value;
                    if (g.Onaylandi.HasValue) satir.Onaylandi = g.Onaylandi.Value;
                }
            }

            belge.Durum = BelgeDurumu.OnayBekliyor;
            return Clone(belge);
        }
    }

    public OkunanBelge Onayla(Guid id, bool eksikSatirlariOnayla = true)
    {
        lock (_lock)
        {
            if (!_belgeler.TryGetValue(id, out var belge))
            {
                throw new KeyNotFoundException("Belge bulunamadı.");
            }

            var aktif = belge.AktifSatirlar.ToList();
            if (aktif.Count == 0)
            {
                throw new InvalidOperationException("Onaylanacak satır yok.");
            }

            if (eksikSatirlariOnayla)
            {
                foreach (var s in aktif)
                {
                    s.Onaylandi = true;
                }
            }
            else if (aktif.Any(s => !s.Onaylandi))
            {
                throw new InvalidOperationException("Tüm aktif satırlar onaylanmalı.");
            }

            belge.Durum = BelgeDurumu.Onaylandi;
            return Clone(belge);
        }
    }

    public VegaAktarimSonucu VegayaHazirla(Guid id, string format = "xml")
    {
        lock (_lock)
        {
            if (!_belgeler.TryGetValue(id, out var belge))
            {
                throw new KeyNotFoundException("Belge bulunamadı.");
            }

            if (belge.Durum == BelgeDurumu.OnayBekliyor)
            {
                Onayla(id);
                belge = _belgeler[id];
            }

            var sonuc = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? _vega.JsonOlustur(belge)
                : _vega.XmlOlustur(belge);

            belge.Durum = BelgeDurumu.VegayaAktarildi;
            _aktarimlar[belge.Id] = sonuc;
            return sonuc;
        }
    }

    public VegaAktarimSonucu? AktarimGetir(Guid id)
    {
        lock (_lock)
        {
            return _aktarimlar.TryGetValue(id, out var a) ? a : null;
        }
    }

    private static OkunanBelge Clone(OkunanBelge kaynak) => new()
    {
        Id = kaynak.Id,
        OlusturmaZamani = kaynak.OlusturmaZamani,
        BelgeTarihi = kaynak.BelgeTarihi,
        CariUnvan = kaynak.CariUnvan,
        CariKodu = kaynak.CariKodu,
        BelgeNo = kaynak.BelgeNo,
        BelgeTuru = kaynak.BelgeTuru,
        KaynakDosyaAdi = kaynak.KaynakDosyaAdi,
        KaynakIcerikTuru = kaynak.KaynakIcerikTuru,
        Durum = kaynak.Durum,
        OkumaNotu = kaynak.OkumaNotu,
        Satirlar = kaynak.Satirlar.Select(s => new BelgeSatiri
        {
            Id = s.Id,
            SiraNo = s.SiraNo,
            UrunKodu = s.UrunKodu,
            UrunAdi = s.UrunAdi,
            Miktar = s.Miktar,
            Birim = s.Birim,
            BirimFiyat = s.BirimFiyat,
            Tutar = s.Tutar,
            MusteriNotu = s.MusteriNotu,
            GrupNotu = s.GrupNotu,
            GuvenSkoru = s.GuvenSkoru,
            Onaylandi = s.Onaylandi,
            Silindi = s.Silindi
        }).ToList()
    };
}

public sealed class BelgeGuncellemeIstek
{
    public string? CariUnvan { get; set; }
    public string? CariKodu { get; set; }
    public string? BelgeNo { get; set; }
    public string? BelgeTuru { get; set; }
    public DateTime? BelgeTarihi { get; set; }
    public List<SatirGuncellemeIstek>? Satirlar { get; set; }
}

public sealed class SatirGuncellemeIstek
{
    public Guid Id { get; set; }
    public string? UrunKodu { get; set; }
    public string? UrunAdi { get; set; }
    public decimal? Miktar { get; set; }
    public string? Birim { get; set; }
    public decimal? BirimFiyat { get; set; }
    public decimal? Tutar { get; set; }
    public string? MusteriNotu { get; set; }
    public string? GrupNotu { get; set; }
    public bool? Silindi { get; set; }
    public bool? Onaylandi { get; set; }
}
