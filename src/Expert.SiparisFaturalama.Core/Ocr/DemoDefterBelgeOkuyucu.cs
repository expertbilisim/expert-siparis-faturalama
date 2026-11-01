using Expert.SiparisFaturalama.Core.Models;

namespace Expert.SiparisFaturalama.Core.Ocr;

/// <summary>
/// El yazısı defter örneği için deterministik demo okuyucu.
/// Gerçek OCR/AI anahtarı yokken onay akışını göstermek için kullanılır.
/// </summary>
public sealed class DemoDefterBelgeOkuyucu : IBelgeOkuyucu
{
    public string Ad => "DemoDefter";

    public Task<OkunanBelge> OkuAsync(
        Stream belgesi,
        string dosyaAdi,
        string? icerikTuru = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Stream tüketilsin; içerik demo için sabit fixture.
        using var _ = new MemoryStream();
        belgesi.CopyTo(_);

        var belge = new OkunanBelge
        {
            KaynakDosyaAdi = dosyaAdi,
            KaynakIcerikTuru = icerikTuru,
            BelgeTarihi = DateTime.Today,
            BelgeTuru = "SIPARIS",
            CariUnvan = null,
            Durum = BelgeDurumu.OnayBekliyor,
            OkumaNotu =
                "El yazısı defter sayfası demo okuması. Satırları kontrol edip onaylayın; " +
                "müşteri notları (Buhara, Balkan, Aytaç vb.) cari eşlemesi için kullanılabilir.",
            Satirlar =
            [
                Satir(1, "78A", "Toner 78A", 3, 380m, 1140m, null, null, 0.92),
                Satir(2, "111ML", "Toner 111ML", 1, 450m, 450m, null, "ENT", 0.78),
                Satir(3, "ML1610", "Toner ML 1610", 1, 650m, 650m, null, "ENT", 0.80),
                Satir(4, "XEROX3010", "Toner Xerox 3010", 1, 450m, 450m, null, "ENT", 0.82),
                Satir(5, "ML116", "Toner ML 116", 3, 420m, 1260m, null, "Y.G", 0.88),
                Satir(6, "285A", "Toner 285A", 2, 380m, 760m, "Kıyak", null, 0.84),
                Satir(7, "17K117", "Toner/Drum 17K 117 (2070)", 4, 580m, 2320m, null, null, 0.75),
                Satir(8, "070H", "Toner 070H", 1, 850m, 850m, "Buhara", null, 0.70),
                Satir(9, "783X", "Toner 783X", 4, 380m, 1520m, null, null, 0.77),
                Satir(10, "070H", "Toner 070H", 2, 850m, 1700m, "Balkan", null, 0.72),
                Satir(11, "103A", "Toner 103A", 1, 350m, 350m, "Y.G Grup", null, 0.68),
                Satir(12, "GL605Y", "Toner GL 605 Y", 4, 780m, 3120m, null, null, 0.74),
                Satir(13, "2675", "Drum 2675", 1, 1100m, 1100m, "Aytaç", null, 0.71)
            ]
        };

        return Task.FromResult(belge);
    }

    private static BelgeSatiri Satir(
        int sira,
        string kod,
        string ad,
        decimal miktar,
        decimal birimFiyat,
        decimal tutar,
        string? musteri,
        string? grup,
        double guven) => new()
    {
        SiraNo = sira,
        UrunKodu = kod,
        UrunAdi = ad,
        Miktar = miktar,
        Birim = "Adet",
        BirimFiyat = birimFiyat,
        Tutar = tutar,
        MusteriNotu = musteri,
        GrupNotu = grup,
        GuvenSkoru = guven,
        Onaylandi = false
    };
}
