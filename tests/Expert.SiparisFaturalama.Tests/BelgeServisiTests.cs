using Expert.SiparisFaturalama.Core.Models;
using Expert.SiparisFaturalama.Core.Ocr;
using Expert.SiparisFaturalama.Core.Services;
using Expert.SiparisFaturalama.Core.Vega;

namespace Expert.SiparisFaturalama.Tests;

public class BelgeServisiTests
{
    private static BelgeServisi CreateSut() =>
        new(new DemoDefterBelgeOkuyucu(), new VegaBelgeAktarici());

    [Fact]
    public async Task Demo_okuma_satirlari_ve_toplami_uretir()
    {
        var sut = CreateSut();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var belge = await sut.YukleVeOkuAsync(stream, "ornek-defter.jpg", "image/jpeg");

        Assert.Equal(BelgeDurumu.OnayBekliyor, belge.Durum);
        Assert.Equal(13, belge.AktifSatirlar.Count());
        Assert.Equal(15670m, belge.GenelToplam);
        Assert.Contains(belge.Satirlar, s => s.UrunKodu == "78A" && s.Miktar == 3);
    }

    [Fact]
    public async Task Onaysiz_vega_aktarimi_otomatik_onaylar_ve_xml_uretir()
    {
        var sut = CreateSut();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var belge = await sut.YukleVeOkuAsync(stream, "ornek.jpg");

        var aktarim = sut.VegayaHazirla(belge.Id, "xml");
        var guncel = sut.Getir(belge.Id);

        Assert.Equal(BelgeDurumu.VegayaAktarildi, guncel!.Durum);
        Assert.Equal("application/xml", aktarim.Format);
        Assert.Contains("<VegaBelge", aktarim.Icerik);
        Assert.Contains("<StokKodu>78A</StokKodu>", aktarim.Icerik);
        Assert.Contains("<GenelToplam>15670</GenelToplam>", aktarim.Icerik);
    }

    [Fact]
    public async Task Satir_guncelleme_tutari_degistirir()
    {
        var sut = CreateSut();
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var belge = await sut.YukleVeOkuAsync(stream, "ornek.jpg");
        var ilk = belge.Satirlar[0];

        var guncel = sut.Guncelle(belge.Id, new BelgeGuncellemeIstek
        {
            CariUnvan = "Demo Cari A.Ş.",
            Satirlar =
            [
                new SatirGuncellemeIstek
                {
                    Id = ilk.Id,
                    BirimFiyat = 400m,
                    Tutar = 1200m
                }
            ]
        });

        Assert.Equal("Demo Cari A.Ş.", guncel.CariUnvan);
        Assert.Equal(400m, guncel.Satirlar[0].BirimFiyat);
        Assert.Equal(1200m, guncel.Satirlar[0].Tutar);
    }
}
