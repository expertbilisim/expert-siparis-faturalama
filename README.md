# Expert Sipariş Faturalama

El yazısı / taranmış sipariş-fiş belgelerini okuyup operatör onayından sonra **Vega** aktarım paketi üreten Expert Bilişim uygulaması.

## Akış

1. Belge fotoğrafı veya taraması yüklenir  
2. OCR/AI satırları çıkarır (stok kodu, adet, birim fiyat, tutar, müşteri notu)  
3. Operatör onay ekranında düzeltir  
4. Onay sonrası Vega XML/JSON belgesi üretilir  

## Projeler

| Proje | Açıklama |
|-------|----------|
| `Expert.SiparisFaturalama.Core` | Model, belge okuyucu, Vega paketleyici |
| `Expert.SiparisFaturalama.Web` | Onay arayüzü + API (prototip / iç kullanım) |
| `Expert.SiparisFaturalama.Desktop` | Windows Forms masaüstü istemci (`net8.0-windows`) |
| `Expert.SiparisFaturalama.Tests` | Birim testler |

## Hızlı başlangıç (Web)

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet restore
dotnet test
dotnet run --project src/Expert.SiparisFaturalama.Web --urls http://0.0.0.0:5088
```

Tarayıcı: `http://localhost:5088`

- **Örnek defteri yükle**: repo içindeki el yazısı örnek satırlarıyla demo akışı  
- Gerçek vision okuma için: `EXPERT_OPENAI_API_KEY` veya `OpenAI:ApiKey`

## Windows masaüstü

Visual Studio / Windows SDK ile:

```bash
dotnet build src/Expert.SiparisFaturalama.Desktop/Expert.SiparisFaturalama.Desktop.csproj
```

## Vega aktarımı

Onaylanan belge `VegaBelge` XML şemasına çevrilir (`StokKodu`, `Miktar`, `BirimFiyat`, `CariKodu` …).  
Canlı SOAP gönderimi (`integration.vegayazilim.com.tr`) müşteri oturum bilgileri bağlandığında eklenecek; şu an indirilebilir aktarım paketi üretilir.

## Not

El yazısı defterlerde güven skoru düşük satırlar turuncu vurgulanır. Vega’ya basmadan önce onay zorunludur.
