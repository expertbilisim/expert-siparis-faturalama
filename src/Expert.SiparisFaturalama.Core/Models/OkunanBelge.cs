namespace Expert.SiparisFaturalama.Core.Models;

public enum BelgeDurumu
{
    Taslak = 0,
    Okundu = 1,
    OnayBekliyor = 2,
    Onaylandi = 3,
    VegayaAktarildi = 4,
    Hata = 5
}

public sealed class OkunanBelge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public DateTime? BelgeTarihi { get; set; }
    public string? CariUnvan { get; set; }
    public string? CariKodu { get; set; }
    public string? BelgeNo { get; set; }
    public string BelgeTuru { get; set; } = "SIPARIS";
    public string? KaynakDosyaAdi { get; set; }
    public string? KaynakIcerikTuru { get; set; }
    public BelgeDurumu Durum { get; set; } = BelgeDurumu.Taslak;
    public string? OkumaNotu { get; set; }
    public List<BelgeSatiri> Satirlar { get; set; } = [];

    public decimal GenelToplam => Satirlar.Where(s => !s.Silindi).Sum(s => s.Tutar);

    public decimal HesaplananGenelToplam =>
        Satirlar.Where(s => !s.Silindi).Sum(s => s.HesaplananTutar);

    public IEnumerable<BelgeSatiri> AktifSatirlar => Satirlar.Where(s => !s.Silindi);
}
