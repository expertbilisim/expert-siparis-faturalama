using Expert.SiparisFaturalama.Core.Models;

namespace Expert.SiparisFaturalama.Core.Ocr;

/// <summary>
/// Vision API yapılandırılmışsa onu kullanır; aksi halde demo okuyucuya düşer.
/// </summary>
public sealed class AkilliBelgeOkuyucu : IBelgeOkuyucu
{
    private readonly OpenAiVisionBelgeOkuyucu _vision;
    private readonly DemoDefterBelgeOkuyucu _demo;

    public AkilliBelgeOkuyucu(OpenAiVisionBelgeOkuyucu vision, DemoDefterBelgeOkuyucu demo)
    {
        _vision = vision;
        _demo = demo;
    }

    public string Ad => _vision.Yapilandirilmis ? _vision.Ad : _demo.Ad;

    public async Task<OkunanBelge> OkuAsync(
        Stream belgesi,
        string dosyaAdi,
        string? icerikTuru = null,
        CancellationToken cancellationToken = default)
    {
        if (!_vision.Yapilandirilmis)
        {
            return await _demo.OkuAsync(belgesi, dosyaAdi, icerikTuru, cancellationToken);
        }

        try
        {
            return await _vision.OkuAsync(belgesi, dosyaAdi, icerikTuru, cancellationToken);
        }
        catch
        {
            // Vision başarısız olursa akışı kırma; demo ile devam et.
            if (belgesi.CanSeek)
            {
                belgesi.Position = 0;
            }

            var belge = await _demo.OkuAsync(belgesi, dosyaAdi, icerikTuru, cancellationToken);
            belge.OkumaNotu =
                (belge.OkumaNotu ?? string.Empty) +
                " | Uyarı: Vision okuma başarısız, demo satırlar yüklendi.";
            return belge;
        }
    }
}
