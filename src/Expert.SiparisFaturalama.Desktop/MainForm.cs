using Expert.SiparisFaturalama.Core.Ocr;
using Expert.SiparisFaturalama.Core.Services;
using Expert.SiparisFaturalama.Core.Vega;

namespace Expert.SiparisFaturalama.Desktop;

public sealed class MainForm : Form
{
    private readonly BelgeServisi _belgeServisi;
    private readonly DataGridView _grid = new();
    private readonly TextBox _cariUnvan = new();
    private readonly TextBox _cariKodu = new();
    private readonly Label _durum = new();
    private readonly Label _toplam = new();
    private readonly PictureBox _preview = new();
    private Guid? _aktifBelgeId;

    public MainForm()
    {
        var options = new OpenAiVisionOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("EXPERT_OPENAI_API_KEY")
        };
        var http = new HttpClient();
        var vision = new OpenAiVisionBelgeOkuyucu(http, options);
        var demo = new DemoDefterBelgeOkuyucu();
        _belgeServisi = new BelgeServisi(new AkilliBelgeOkuyucu(vision, demo), new VegaBelgeAktarici());

        Text = "Expert Sipariş Faturalama";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };

        var btnAc = new Button { Text = "Belge aç", AutoSize = true };
        var btnDemo = new Button { Text = "Demo defter", AutoSize = true };
        var btnKaydet = new Button { Text = "Kaydet", AutoSize = true };
        var btnOnayla = new Button { Text = "Onayla", AutoSize = true };
        var btnVega = new Button { Text = "Vega XML kaydet", AutoSize = true };

        btnAc.Click += async (_, _) => await BelgeAcAsync();
        btnDemo.Click += async (_, _) => await DemoYukleAsync();
        btnKaydet.Click += (_, _) => Kaydet();
        btnOnayla.Click += (_, _) => Onayla();
        btnVega.Click += (_, _) => VegaKaydet();

        top.Controls.AddRange([btnAc, btnDemo, btnKaydet, btnOnayla, btnVega, _durum]);

        var meta = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 4,
            Padding = new Padding(8)
        };
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        meta.Controls.Add(Labeled("Cari unvan", _cariUnvan), 0, 0);
        meta.Controls.Add(Labeled("Cari kodu", _cariKodu), 1, 0);
        meta.Controls.Add(Labeled("Toplam", _toplam), 2, 0);

        _preview.Dock = DockStyle.Left;
        _preview.Width = 320;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BackColor = Color.WhiteSmoke;

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SiraNo", HeaderText = "#", Width = 40 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UrunKodu", HeaderText = "Stok kodu", Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UrunAdi", HeaderText = "Açıklama", Width = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Miktar", HeaderText = "Miktar", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BirimFiyat", HeaderText = "B.Fiyat", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Tutar", HeaderText = "Tutar", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "MusteriNotu", HeaderText = "Not", Width = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GuvenSkoru", HeaderText = "Güven", Width = 70 });

        Controls.Add(_grid);
        Controls.Add(_preview);
        Controls.Add(meta);
        Controls.Add(top);

        _durum.AutoSize = true;
        _durum.Text = $"Okuyucu: {_belgeServisi.OkuyucuAdi}";
        _toplam.Text = "0";
        _toplam.BorderStyle = BorderStyle.FixedSingle;
        _toplam.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static Control Labeled(string title, Control inner)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        var l = new Label { Text = title, Dock = DockStyle.Top, Height = 18 };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        p.Controls.Add(l);
        return p;
    }

    private async Task BelgeAcAsync()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Görseller|*.jpg;*.jpeg;*.png;*.webp|Tüm dosyalar|*.*"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        await using var fs = File.OpenRead(ofd.FileName);
        var belge = await _belgeServisi.YukleVeOkuAsync(fs, Path.GetFileName(ofd.FileName));
        _preview.ImageLocation = ofd.FileName;
        Goster(belge);
    }

    private async Task DemoYukleAsync()
    {
        await using var ms = new MemoryStream([1, 2, 3]);
        var belge = await _belgeServisi.YukleVeOkuAsync(ms, "ornek-defter.jpg");
        Goster(belge);
    }

    private void Goster(Core.Models.OkunanBelge belge)
    {
        _aktifBelgeId = belge.Id;
        _cariUnvan.Text = belge.CariUnvan ?? string.Empty;
        _cariKodu.Text = belge.CariKodu ?? string.Empty;
        _grid.DataSource = belge.AktifSatirlar.ToList();
        _toplam.Text = belge.GenelToplam.ToString("N2");
        _durum.Text = $"Durum: {belge.Durum} | Okuyucu: {_belgeServisi.OkuyucuAdi}";
    }

    private void Kaydet()
    {
        if (_aktifBelgeId is null) return;
        var rows = (List<Core.Models.BelgeSatiri>)_grid.DataSource!;
        var istek = new BelgeGuncellemeIstek
        {
            CariUnvan = _cariUnvan.Text,
            CariKodu = _cariKodu.Text,
            Satirlar = rows.Select(r => new SatirGuncellemeIstek
            {
                Id = r.Id,
                UrunKodu = r.UrunKodu,
                UrunAdi = r.UrunAdi,
                Miktar = r.Miktar,
                BirimFiyat = r.BirimFiyat,
                Tutar = r.Tutar,
                MusteriNotu = r.MusteriNotu
            }).ToList()
        };
        Goster(_belgeServisi.Guncelle(_aktifBelgeId.Value, istek));
    }

    private void Onayla()
    {
        if (_aktifBelgeId is null) return;
        Kaydet();
        Goster(_belgeServisi.Onayla(_aktifBelgeId.Value));
        MessageBox.Show(this, "Belge onaylandı.", "Expert");
    }

    private void VegaKaydet()
    {
        if (_aktifBelgeId is null) return;
        Kaydet();
        var sonuc = _belgeServisi.VegayaHazirla(_aktifBelgeId.Value, "xml");
        using var sfd = new SaveFileDialog
        {
            FileName = sonuc.DosyaAdi,
            Filter = "XML|*.xml"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(sfd.FileName, sonuc.Icerik);
        Goster(_belgeServisi.Getir(_aktifBelgeId.Value)!);
        MessageBox.Show(this, "Vega XML kaydedildi.", "Expert");
    }
}
