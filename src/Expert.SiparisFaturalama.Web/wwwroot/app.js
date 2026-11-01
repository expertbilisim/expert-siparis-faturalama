(() => {
  const state = {
    file: null,
    belge: null
  };

  const el = {
    dropZone: document.getElementById("dropZone"),
    fileInput: document.getElementById("fileInput"),
    btnOku: document.getElementById("btnOku"),
    btnDemo: document.getElementById("btnDemo"),
    btnKaydet: document.getElementById("btnKaydet"),
    btnOnayla: document.getElementById("btnOnayla"),
    btnVega: document.getElementById("btnVega"),
    previewWrap: document.getElementById("previewWrap"),
    preview: document.getElementById("preview"),
    status: document.getElementById("status"),
    reviewPanel: document.getElementById("reviewPanel"),
    okumaNotu: document.getElementById("okumaNotu"),
    satirSayisi: document.getElementById("satirSayisi"),
    genelToplam: document.getElementById("genelToplam"),
    belgeDurum: document.getElementById("belgeDurum"),
    cariUnvan: document.getElementById("cariUnvan"),
    cariKodu: document.getElementById("cariKodu"),
    belgeNo: document.getElementById("belgeNo"),
    belgeTuru: document.getElementById("belgeTuru"),
    tbody: document.querySelector("#satirTable tbody"),
    okuyucuMeta: document.getElementById("okuyucuMeta")
  };

  const durumAdi = {
    0: "Taslak",
    1: "Okundu",
    2: "Onay bekliyor",
    3: "Onaylandı",
    4: "Vega’ya aktarıldı",
    5: "Hata",
    Taslak: "Taslak",
    Okundu: "Okundu",
    OnayBekliyor: "Onay bekliyor",
    Onaylandi: "Onaylandı",
    VegayaAktarildi: "Vega’ya aktarıldı",
    Hata: "Hata"
  };

  function setStatus(text) {
    el.status.textContent = text;
  }

  function money(n) {
    return new Intl.NumberFormat("tr-TR", {
      minimumFractionDigits: 0,
      maximumFractionDigits: 2
    }).format(Number(n || 0));
  }

  function confidenceClass(score) {
    if (score < 0.75) return "low";
    if (score < 0.9) return "mid";
    return "high";
  }

  function setFile(file) {
    state.file = file;
    el.btnOku.disabled = !file;
    if (!file) {
      el.previewWrap.hidden = true;
      return;
    }
    const url = URL.createObjectURL(file);
    el.preview.src = url;
    el.previewWrap.hidden = false;
    setStatus(`Seçildi: ${file.name}`);
  }

  el.dropZone.addEventListener("click", () => el.fileInput.click());
  el.fileInput.addEventListener("change", () => {
    const file = el.fileInput.files?.[0];
    if (file) setFile(file);
  });

  ["dragenter", "dragover"].forEach((evt) => {
    el.dropZone.addEventListener(evt, (e) => {
      e.preventDefault();
      el.dropZone.classList.add("dragover");
    });
  });
  ["dragleave", "drop"].forEach((evt) => {
    el.dropZone.addEventListener(evt, (e) => {
      e.preventDefault();
      el.dropZone.classList.remove("dragover");
    });
  });
  el.dropZone.addEventListener("drop", (e) => {
    const file = e.dataTransfer?.files?.[0];
    if (file) setFile(file);
  });

  async function api(url, options) {
    const res = await fetch(url, options);
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
      throw new Error(data.hata || `İstek başarısız (${res.status})`);
    }
    return data;
  }

  function renderBelge(belge) {
    state.belge = belge;
    el.reviewPanel.hidden = false;
    el.okumaNotu.textContent = belge.okumaNotu || "Satırları kontrol edin.";
    el.cariUnvan.value = belge.cariUnvan || "";
    el.cariKodu.value = belgekodu(belge);
    el.belgeNo.value = belge.belgeNo || "";
    el.belgeTuru.value = belge.belgeTuru || "SIPARIS";
    el.belgeDurum.textContent = durumAdi[belge.durum] || String(belge.durum);
    el.btnVega.disabled = !(belge.durum === 3 || belge.durum === 4 || belge.durum === "Onaylandi" || belge.durum === "VegayaAktarildi");

    const aktif = (belge.satirlar || []).filter((s) => !s.silindi);
    el.satirSayisi.textContent = String(aktif.length);
    el.genelToplam.textContent = money(aktif.reduce((a, s) => a + Number(s.tutar || 0), 0));

    el.tbody.innerHTML = "";
    for (const s of belge.satirlar || []) {
      if (s.silindi) continue;
      const tr = document.createElement("tr");
      const hesap = Number(s.miktar || 0) * Number(s.birimFiyat || 0);
      const mismatch = Math.abs(hesap - Number(s.tutar || 0)) > 0.01;
      if ((s.guvenSkoru || 0) < 0.75) tr.classList.add("low-confidence");
      if (mismatch) tr.classList.add("mismatch");

      tr.dataset.id = s.id;
      tr.innerHTML = `
        <td>${s.siraNo}</td>
        <td><input data-f="urunKodu" value="${esc(s.urunKodu)}" /></td>
        <td><input data-f="urunAdi" value="${esc(s.urunAdi)}" /></td>
        <td><input data-f="miktar" type="number" step="1" value="${s.miktar}" /></td>
        <td><input data-f="birimFiyat" type="number" step="0.01" value="${s.birimFiyat}" /></td>
        <td><input data-f="tutar" type="number" step="0.01" value="${s.tutar}" /></td>
        <td><input data-f="musteriNotu" value="${esc(s.musteriNotu || s.grupNotu || "")}" /></td>
        <td><span class="confidence ${confidenceClass(s.guvenSkoru || 0)}">${Math.round((s.guvenSkoru || 0) * 100)}%</span></td>
        <td><button type="button" class="icon-btn" data-del title="Satırı sil">Sil</button></td>
      `;
      tr.querySelector("[data-del]").addEventListener("click", () => {
        s.silindi = true;
        renderBelge(belge);
      });
      el.tbody.appendChild(tr);
    }
  }

  function belgekodu(belge) {
    return belge.cariKodu || "";
  }

  function esc(v) {
    return String(v ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll('"', "&quot;")
      .replaceAll("<", "&lt;");
  }

  function collectUpdates() {
    const satirlar = [...el.tbody.querySelectorAll("tr")].map((tr) => {
      const get = (f) => tr.querySelector(`[data-f="${f}"]`)?.value;
      return {
        id: tr.dataset.id,
        urunKodu: get("urunKodu"),
        urunAdi: get("urunAdi"),
        miktar: Number(get("miktar") || 0),
        birimFiyat: Number(get("birimFiyat") || 0),
        tutar: Number(get("tutar") || 0),
        musteriNotu: get("musteriNotu"),
        silindi: false
      };
    });

    // silinenleri de gönder
    for (const s of state.belge.satirlar || []) {
      if (s.silindi && !satirlar.some((x) => x.id === s.id)) {
        satirlar.push({ id: s.id, silindi: true });
      }
    }

    return {
      cariUnvan: el.cariUnvan.value,
      cariKodu: el.cariKodu.value,
      belgeNo: el.belgeNo.value,
      belgeTuru: el.belgeTuru.value,
      satirlar
    };
  }

  el.btnOku.addEventListener("click", async () => {
    if (!state.file) return;
    setStatus("Belge okunuyor…");
    el.btnOku.disabled = true;
    try {
      const form = new FormData();
      form.append("belge", state.file);
      const belge = await api("/api/belgeler/oku", { method: "POST", body: form });
      renderBelge(belge);
      setStatus("Okuma tamam. Satırları kontrol edin.");
    } catch (err) {
      setStatus(err.message);
    } finally {
      el.btnOku.disabled = !state.file;
    }
  });

  el.btnDemo.addEventListener("click", async () => {
    setStatus("Örnek defter yükleniyor…");
    try {
      const belge = await api("/api/belgeler/demo", { method: "POST" });
      el.preview.src = "/samples/ornek-defter.jpg";
      el.previewWrap.hidden = false;
      renderBelge(belge);
      setStatus("Demo defter satırları yüklendi (API anahtarı yoksa demo okuyucu).");
    } catch (err) {
      setStatus(err.message);
    }
  });

  el.btnKaydet.addEventListener("click", async () => {
    if (!state.belge) return;
    try {
      const belge = await api(`/api/belgeler/${state.belge.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(collectUpdates())
      });
      renderBelge(belge);
      setStatus("Düzeltmeler kaydedildi.");
    } catch (err) {
      setStatus(err.message);
    }
  });

  el.btnOnayla.addEventListener("click", async () => {
    if (!state.belge) return;
    try {
      await api(`/api/belgeler/${state.belge.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(collectUpdates())
      });
      const belge = await api(`/api/belgeler/${state.belge.id}/onayla`, { method: "POST" });
      renderBelge(belge);
      setStatus("Belge onaylandı. Vega aktarımına hazır.");
    } catch (err) {
      setStatus(err.message);
    }
  });

  el.btnVega.addEventListener("click", async () => {
    if (!state.belge) return;
    try {
      const sonuc = await api(`/api/belgeler/${state.belge.id}/vega?format=xml`, { method: "POST" });
      const blob = new Blob([sonuc.icerik], { type: sonuc.format || "application/xml" });
      const a = document.createElement("a");
      a.href = URL.createObjectURL(blob);
      a.download = sonuc.dosyaAdi || "vega-belge.xml";
      a.click();
      const belge = await api(`/api/belgeler/${state.belge.id}`);
      renderBelge(belge);
      setStatus("Vega XML indirildi.");
    } catch (err) {
      setStatus(err.message);
    }
  });

  api("/api/health")
    .then((h) => {
      el.okuyucuMeta.textContent = `Okuyucu: ${h.okuyucu}`;
    })
    .catch(() => {
      el.okuyucuMeta.textContent = "API bağlantısı yok";
    });
})();
