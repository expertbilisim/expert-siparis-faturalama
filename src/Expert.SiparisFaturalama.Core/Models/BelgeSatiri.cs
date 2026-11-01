namespace Expert.SiparisFaturalama.Core.Models;

public sealed class BelgeSatiri
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SiraNo { get; set; }
    public string UrunKodu { get; set; } = string.Empty;
    public string UrunAdi { get; set; } = string.Empty;
    public decimal Miktar { get; set; } = 1;
    public string Birim { get; set; } = "Adet";
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }
    public string? MusteriNotu { get; set; }
    public string? GrupNotu { get; set; }
    public double GuvenSkoru { get; set; } = 1.0;
    public bool Onaylandi { get; set; }
    public bool Silindi { get; set; }

    public decimal HesaplananTutar => Math.Round(Miktar * BirimFiyat, 2);

    public bool TutarUyumsuz => Math.Abs(Tutar - HesaplananTutar) > 0.01m;
}
