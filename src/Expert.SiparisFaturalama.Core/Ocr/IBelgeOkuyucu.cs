using Expert.SiparisFaturalama.Core.Models;

namespace Expert.SiparisFaturalama.Core.Ocr;

public interface IBelgeOkuyucu
{
    string Ad { get; }

    Task<OkunanBelge> OkuAsync(
        Stream belgesi,
        string dosyaAdi,
        string? icerikTuru = null,
        CancellationToken cancellationToken = default);
}
