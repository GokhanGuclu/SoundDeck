# SoundPilot'u Microsoft Store'da Yayınlama

Bu belge uygulamanın Store (MSIX) sürümünü nasıl paketleyip göndereceğini anlatır.
Store'daki ad **SoundPilot**, GitHub'daki ad **SoundDeck** olarak kalır.
GitHub + Velopack dağıtımı olduğu gibi devam eder; Store sürümü ayrı bir derlemedir
(`-p:StoreBuild=true`).

---

## 1. Partner Center hesabı

> ⚠️ **Kayda mutlaka https://storedeveloper.microsoft.com adresinden başla.** Ücretsiz bireysel
> kayıt akışının desteklenen tek giriş noktası burasıdır. Doğrudan Partner Center'a
> (partner.microsoft.com/dashboard), Xbox veya Visual Studio üzerinden girersen **eski akış**
> açılır: yeni bir workspace/tenant oluşturmanı ister ve ücret çıkarır.

1. https://storedeveloper.microsoft.com → **Get started for free** → **Individual developer (free)**.
2. **Kişisel Microsoft hesabıyla (MSA)** giriş yap. İş/okul hesabı kullanma — o hesaplar Entra
   tenant'ına bağlı olduğu için Partner Center workspace oluşturma akışına düşersin.
3. Kimlik doğrulama: resmi kimlik + selfie (telefonla, iyi ışıkta, orijinal belgeyle).
4. Profil bilgileri otomatik dolar; kontrol edip tamamla.
5. **Go to Partner Center dashboard** → hesap seçicide *aynı* Microsoft hesabını seç.
   "Apps & Games" kutucuğu hemen görünmezse ~5 dakika bekleyip sayfayı yenile veya
   doğrudan https://aka.ms/submitwindowsapp adresine git.
6. Ardından **Apps and games > New product > MSIX or PWA app** ile ürünü oluştur.
7. **Product name** olarak kullanacağın adı rezerve et. "SoundDeck" adı başkası tarafından
   alınmış durumda — Partner Center'ın isim kutusu müsaitliği anında gösterir, birkaç
   alternatif dene (AudioDeck, MixDeck, SoundPilot gibi). Mevcut bir uygulamadan tek harf
   farklı adlardan kaçın: sertifikasyonda "karıştırılabilir isim" gerekçesiyle reddedilebilir.

### Store adını koda bağlamak

Rezerve ettiğin ad, MSIX manifest'indeki adla **birebir** aynı olmak zorunda. Tek yapman
gereken `AudioDeviceTrayApp\AppInfo.cs` içindeki tek satırı değiştirmek:

```csharp
public const string StoreDisplayName = "SoundPilot";   // <- rezerve ettiğin ad
```

Şu an ayarlı ad: **SoundPilot**. Store'da başka bir ad rezerve edersen bu satırı güncelle,
paketleme betiği gerisini halleder.

`build-msix.ps1` bu değeri okuyup manifest'e yazar; uygulamanın pencere başlığı, tray
ipucu, bildirimler ve mesaj kutuları da aynı addan beslenir (`AppInfo.DisplayName`).
GitHub sürümü etkilenmez — orada ad "SoundDeck" kalır. Çalıştırılabilir dosya adı
(`SoundDeck.exe`), `%AppData%\SoundDeck` ayar klasörü ve Equalizer APO dosya adı bilerek
değişmez, böylece ad değişikliği kimsenin ayarlarını kaybettirmez.

Kayıt akışında takılırsan (yalnızca yeni bireysel onboarding için): storesupport@service.microsoft.com

## 2. Paket kimliğini al

Partner Center'da ürünün altında **Product management > Product identity** sayfası üç değer verir:

| Partner Center alanı | Nereye gider |
|---|---|
| Package/Identity/Name | `IdentityName` |
| Package/Identity/Publisher | `Publisher` (`CN=...`) |
| Package/Properties/PublisherDisplayName | `PublisherDisplayName` |

`packaging\identity.sample.json` dosyasını `packaging\identity.json` olarak kopyala ve bu üç
değeri birebir yapıştır. Bir harf bile farklıysa yükleme reddedilir. (`identity.json`
`.gitignore`'da.)

## 3. Paketi üret

```powershell
# Store'a gidecek paket (imzasız - imzalamayı Store yapar)
powershell -ExecutionPolicy Bypass -File packaging\build-msix.ps1 -Version 1.0.4
```

Çıktı: `packaging\out\SoundPilot-1.0.4.0-x64.msix`

Betik sırayla şunları yapar: `dotnet publish -p:StoreBuild=true` (self-contained, win-x64) →
`packaging\AppxManifest.xml` içindeki `__TOKEN__`'ları doldurur → `makeappx pack`.
Windows SDK kurulu değilse `makeappx`/`signtool` araçlarını NuGet'ten
`packaging\tools` altına indirir (tek seferlik, ~50 MB).

> **Sürüm numarası:** MSIX 4 parçalıdır ve Store son parçayı kendine ayırır — betik her zaman
> `x.y.z.0` üretir. Her gönderimde sürüm artmalıdır.

### Yerel test

```powershell
powershell -ExecutionPolicy Bypass -File packaging\build-msix.ps1 -Version 1.0.4 -SelfSign
powershell -ExecutionPolicy Bypass -File packaging\install-test.ps1   # UAC ister
```

Kaldırmak için: `Get-AppxPackage *SoundPilot* | Remove-AppxPackage`

Test ederken şunlara bak:

- [ ] Tray simgesi açılıyor, cihaz/mikrofon değiştirme çalışıyor
- [ ] Global kısayollar çalışıyor
- [ ] **Windows ile başlat** açılıp kapanıyor (Görev Yöneticisi > Başlangıç'ta "SoundPilot" görünür)
- [ ] Genel sayfasında "Güncellemeleri Denetle" düğmesi **yok** (Store sürümünde olmamalı)
- [ ] Efektler sayfasında kanal çevirme açılıp kapanıyor (Equalizer APO kuruluysa)

## 4. Store sürümündeki davranış farkları

`StoreBuild=true` ile derlenen sürüm, klasik sürümden şu noktalarda ayrılır:

| Konu | Klasik (GitHub) | Store (MSIX) |
|---|---|---|
| Güncelleme | Velopack, GitHub Releases | Store yapar; Velopack hiç derlenmez |
| Windows ile başlat | `HKCU\...\Run` kaydı | `windows.startupTask` uzantısı + `StartupTask` API |
| "Yenilikler" penceresi | GitHub'dan sürüm notu çeker | Kapalı (Store kendi "Yenilikler"ini gösterir) |
| Ayarlar | `%AppData%\SoundDeck` | `%LocalAppData%\Packages\<PFN>\LocalCache\Roaming\SoundDeck` |
| Kanal çevirme | Equalizer APO klasörüne yazar | Aynı şekilde çalışır (test edildi) |

### Kanal çevirme (Equalizer APO) — test edildi, çalışıyor

Paketli uygulamaların `C:\Program Files\EqualizerAPO\config` altına yazamayacağından
şüphelenmiştik (MSIX'in korumalı yolları paket kabına yönlendirmesi). Ölçüldü ve
**yönlendirme olmuyor** — full-trust MSIX uygulaması gerçek yola yazıyor, Equalizer APO
dosyayı görüyor. Bu yüzden özellik Store sürümünde de açık.

Ölçüm yöntemi (ileride tekrar gerekirse, yönetici PowerShell'de):

```powershell
Invoke-CommandInDesktopPackage -PackageFamilyName "GkhanGl.SoundPilot_vgra6j4qvbdc2" `
  -AppId "SoundDeck" -Command "cmd.exe" -Args '/c echo probe > "C:\Program Files\EqualizerAPO\config\probe.txt"'
Test-Path "C:\Program Files\EqualizerAPO\config\probe.txt"   # True = yönlendirme yok
```

## 5. Store listeleme bilgileri

Gönderimden önce Partner Center'da hazır olması gerekenler:

- **Ekran görüntüleri** — en az 1 adet, 1366x768 veya daha büyük (ayarlar penceresi + tray menüsü iyi bir set)
- **Store logosu** — 300x300 (`assets/logo.png`'den büyütülebilir)
- **Açıklama** — EN ve TR: hazır metinler [store-listing.md](store-listing.md) dosyasında
- **Gizlilik politikası URL'si** — sayfa hazır: `docs/privacy.html`, GitHub Pages ile yayınla
  (adımlar [store-listing.md](store-listing.md) sonunda)
- **Destek iletişimi** — GitHub Issues bağlantısı yeterli
- **Yaş derecelendirmesi** — IARC anketi (birkaç dakika)
- **Fiyat ve pazarlar** — ücretsiz + tüm pazarlar
- **Sertifikasyon notları** — inceleyiciye şunu yazmak faydalı: uygulama sistem varsayılan ses
  cihazını değiştirir, global kısayol kaydeder ve tray'de çalışır; test için Ayarlar'dan bir
  kulaklık/hoparlör seçip kısayol atamaları gerekir.

## 6. Gönderim

1. Paketi **Packages** sekmesine yükle (imzasız `.msix`).
2. Uyarılar çıkarsa çoğu bilgilendirmedir; **hata** varsa manifest kimliğini kontrol et.
3. Gönder. Sertifikasyon **3 iş gününe kadar** sürebilir; sonuç e-posta ile gelir.

## 7. Yeni sürüm çıkarken

```powershell
# 1) csproj içindeki <Version> değerini yükselt
# 2) GitHub sürümü
powershell -ExecutionPolicy Bypass -File assets\build-release.ps1 -Version 1.0.5
# 3) Store sürümü
powershell -ExecutionPolicy Bypass -File packaging\build-msix.ps1 -Version 1.0.5
```

İki kanalın sürüm numaraları aynı olmak zorunda değil, ama aynı tutmak takibi kolaylaştırır.
