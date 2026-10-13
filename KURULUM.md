# Başka bir bilgisayarda kurulum

Bu depo kaynak projedir. Unity Editor, CODESYS runtime ve Python bilgisayarınıza ayrıca kurulur. Depo klasörünü istediğiniz konuma yerleştirebilirsiniz; kaynaklarda geliştiricinin bilgisayarına ait sabit bir yol kullanılmaz.

## 1. Gerekli yazılımlar

| Yazılım | Sürüm / kullanım |
|---|---|
| Windows | CODESYS Control Win ve PowerShell betikleri için. |
| Unity Hub + Unity Editor | **6000.6.4f1**; proje klasörü `Unity/RVM`. |
| CODESYS Development System | **V3.5 SP22 Patch 3**; cihaz tanımı **Control Win V3 x64 3.5.22.30**. |
| CODESYS Control Win V3 x64 | Yerel PLC runtime; OPC UA sunucusu erişilebilir olmalı. Kullanılan runtime'ın lisans/deneme koşulları geçerlidir. |
| Python | **3.12**, `venv` ve `pip` ile. |

İlk Python ve Unity paket kurulumu için internet gerekir. Blender yalnızca `.blend` modelini düzenlemek için gereklidir; Unity sahnesi paylaşılan FBX dosyasını kullanır.

## 2. CODESYS

1. `Codesys_Dosyalari/Paylasim/RVM_Unity_Dijital_Ikiz_v106.project` dosyasını açın. Bu proje kaynaklardan yeniden oluşturulmuştur; kayıtlı kişisel gateway veya PLC oturumu içermez.
2. Cihaz/kütüphane eksik görünürse yukarıdaki sürümleri kurun. Library Manager içinde `Standard, 3.5.22.0 (System)` çözülmüş olmalıdır. Projeyi **Build** ile derleyin.
3. Control Win runtime'ını başlatın. CODESYS **Device → Communication Settings** bölümünde kendi yerel gateway'inizi ve PLC'nizi seçin; gerekiyorsa kendi cihaz kullanıcı hesabınızı oluşturun veya giriş yapın.
4. **Application → Task Configuration → AnaGorev** altında 20 ms çevrimle yalnızca `PRG_UnityRVM` çağrıldığını kontrol edin. Panel için ayrıca 100 ms çevrimli `VISU_TASK` bulunur.
5. **Symbol Configuration** altında `GVL_RVMUnity` yayımlanır. Girişler ve paket başlangıç/bitiş işaretleri ReadWrite, çıkışlar Read olmalıdır. Beklenen sembol listesi aynı klasördeki `RVM_Unity_Dijital_Ikiz_v106.Device.Application.xml` dosyasındadır.
6. Kendi runtime'ınıza **Login / Download / Start** yapın. Bu işlem seçtiğiniz PLC'deki uygulamayı değiştirir; doğru cihazı seçin. OPC UA sunucu adresini, kullanıcı yetkilerini ve gerekiyorsa sertifika güvenini kendi runtime ayarlarınıza göre yapılandırın.

Köprünün varsayılan adresi `opc.tcp://localhost:4840` şeklindedir. CODESYS'in PLC'ye yükleme bağlantısı ile OPC UA bağlantısı farklıdır; CODESYS'e giriş yapabilmek tek başına OPC UA erişiminin hazır olduğu anlamına gelmez.

## 3. Python köprüsü

PowerShell'i indirilen deponun kökünde açın:

```powershell
.\simulasyon_unity\Kurulum.ps1
.\simulasyon_unity\Baslat.ps1
```

Kurulum, depo içinde `.venv-rvm-opcua` oluşturur, `requirements.txt` bağımlılıklarını yükler ve veri sözleşmesi testlerini çalıştırır. Python sistem yolunda değilse kendi Python yürütülebilir dosyanızı belirtin:

```powershell
$pythonYolu = Read-Host 'Kendi python.exe dosyanizin tam yolu'
.\simulasyon_unity\Kurulum.ps1 -PythonCommand $pythonYolu
```

PowerShell betik çalıştırmayı engellerse yalnızca bu pencere için `Set-ExecutionPolicy -Scope Process Bypass` kullanıp yeniden deneyebilirsiniz.

Farklı OPC UA adresi veya hesap gerekiyorsa `simulasyon_unity/ayarlar.local.ps1` dosyasını kendiniz oluşturun. Örneğin dosyada aşağıdaki komutları kullanarak kendi değerlerinizi başlangıçta girebilirsiniz:

```powershell
$env:RVM_OPC_ENDPOINT = Read-Host 'OPC UA sunucu adresi'
$env:RVM_OPC_USER = Read-Host 'OPC UA kullanici adi'
$kimlik = Get-Credential -UserName $env:RVM_OPC_USER -Message 'OPC UA parolasi'
$env:RVM_OPC_PASSWORD = $kimlik.GetNetworkCredential().Password
```

Bu dosya `.gitignore` ile dışlanır; `Baslat.ps1` varsa dosyayı yükler. Kimlik doğrulama gerekmeyen bir kurulumda kullanıcı/parola satırlarına gerek yoktur. Sertifikalı bağlantı kullanılıyorsa `RVM_OPC_SECURITY`, asyncua'nın `Policy,Mode,certificate,private_key[,server_certificate]` biçimindeki güvenlik ayarıdır; kendi sertifika yollarınızı kullanın. Sertifikalar ve özel anahtarlar depoya dahil edilmez.

Durum sorgusu ve kapatma:

```powershell
Invoke-RestMethod http://127.0.0.1:8766/health
.\simulasyon_unity\Durdur.ps1
```

Unity henüz Play modunda değilken `connected: false` görülmesi beklenir; sağlıklı bağlantı için yeni Unity sensör paketleri ve ilerleyen PLC sayacı birlikte gerekir. Köprü sorunlarının ayrıntıları yerel `simulasyon_unity/bridge.log` dosyasına yazılır.

## 4. Unity

1. Unity Hub'da **Add project from disk** ile `Unity/RVM` klasörünü seçin. Paketlerin indirilmesini ve içe aktarımın bitmesini bekleyin.
2. `Assets/RVM/Scenes/RVM_InlineMeasurement_v106.unity` sahnesini açın. **RVM → Paylaşım projesini doğrula** menüsüyle kamera, tartı, prob ve fiziksel numune bağlantılarını kontrol edebilirsiniz. Eski `RVM_DigitalTwin.unity` sahnesi yeni sürümün çalıştırma sahnesi değildir; `RVM_MeasurementLab_v106.unity` bağımsız laboratuvardır.
3. **Play** seçin. Boş istasyonun kamera kalibrasyonu tamamlanıp **PLC BAĞLI / VERİ GÜNCEL** ve protokol **106/106** göründüğünde **BAŞLAT**, ardından malzeme seçimi ve **NESNEYİ GİRİŞE KOY** ile çevrimi başlatın.
4. Hata sonrası nedeni kaldırın; sistemi durdurup gerekiyorsa nesneyi alın ve hazneyi başlangıca getirin. **RESET → BAŞLAT** ile devam edin.

Normal kullanım için kaynakta yol düzenlemeniz gerekmez. İsteğe bağlı `RvmCommissioning` test bileşeninin `reportPath` alanı boşsa rapor Unity'nin o bilgisayardaki `Application.persistentDataPath` klasörüne yazılır; farklı konum isterseniz Inspector'dan kendi yolunuzu belirtin.

## Kaynaklardan PLC projesini yeniden üretme

Hazır `.project` dosyası normal kurulum için yeterlidir. ST kaynaklarını değiştirdiğinizde `Codesys_Dosyalari/Paylasim/create_project.py` betiği CODESYS Scripting içinde yeni proje oluşturabilir. Bu betik normal Python ile çalışmaz ve PLC'ye yükleme yapmaz.

PowerShell'de kendi CODESYS yürütülebilir dosyanızı seçip yeni bir çıktı adı belirleyin:

```powershell
$codesysYolu = Read-Host 'Kendi CODESYS.exe dosyanizin tam yolu'
$env:RVM_CODESYS_OUTPUT = Join-Path $PWD 'Codesys_Dosyalari/Paylasim/Yeni_RVM.project'
$betik = (Resolve-Path 'Codesys_Dosyalari/Paylasim/create_project.py').Path
& $codesysYolu '--profile=CODESYS V3.5 SP22 Patch 3' '--noUI' "--runscript=$betik"
```

Betik mevcut proje dosyasının üzerine yazmaz. Derleme sonucu `build.log` dosyasındadır. Paneli de koruyan `Application_v106.export` dosyası yeni projeye aktarılır; ardından `simulasyon_unity/plc/` kaynakları uygulanır. `.plcopen.xml` dosyası metin kaynaklarını incelemek içindir; panelin eksiksiz taşınması için native `.export` kullanılır. Başka cihaz sürümü kullanacaksanız `RVM_DEVICE_VERSION` ortam değişkenini ayarlayıp uyumluluğu kendi kurulumunuzda kontrol edin.

Betiklerin dayandığı resmi API: [cihaz ekleme](https://forge.codesys.com/tol/scripting/snippets/13/), [kütüphane yönetimi](https://content.helpme-codesys.com/en/ScriptingEngine/ScriptLibManObject.html).

## Paylaşım kapsamı

Unity kaynakları, `.meta` dosyaları, paket tanımları, ortak proje ayarları, temiz PLC projesi, PLC kaynakları ve Python köprüsü paylaşılır. Unity Cloud organizasyon/proje kimlikleri temizlenmiştir. Eski yerel projeler, onların Git geçmişleri, kişisel CODESYS `.opt` dosyaları, günlükler, sanal ortamlar ve yerel bağlantı ayarları dışlanır.

Git commit'inin yazar adı ve e-postası dosya içeriğinden bağımsızdır. Gizlilik istiyorsanız GitHub'ın size verdiği `noreply` e-posta adresini Git ayarlarınızda kullanın. `.gitignore` daha önce commit edilmiş dosyaları veya geçmişteki bilgileri silmez.

## v106 ölçüm ve panel notları

- CODESYS içindeki `VIS_RVMOperator104` adı önceki sürümden korunur; bu panel güncel `GVL_RVMOperator` değişkenlerini kullanır. Protokol sürümü panelin adından değil `GVL_RVMUnity.uiProtokolSurumu` alanından kontrol edilir: **106** olmalıdır.
- Görselleştirme kütüphaneleri de gereklidir. Projeyi belirtilen CODESYS sürümünde açın; Library Manager içinde eksik bağımlılık kalmamalıdır. Native aktarımda standart CODESYS görselleştirme kaynaklarına ait kurulum yolları bulunabilir; bunlar kullanıcı hesabı veya kişisel dosya yolu değildir.
- `ModelKaynaklari/physical_reference.json` numunelerin fiziksel kurulum referansıdır; PLC'ye tanıma sonucu olarak gönderilmez. Yeni numune için tek dinamik Rigidbody, gerçekçi collider, pozitif kütle ve `RvmConductiveBody` gerekir. Sahnedeki `RvmInlineMeasurement → Custom Prefab` alanına atayıp **Diğer** seçeneğiyle deneyebilirsiniz.
- Ölçüm yaşı, geçerlilik ve ürün/oturum eşleşmesi tutmazsa PLC kabul yapmaz. Kamera 4 saniyede tamamlanmazsa tanıma zaman aşımı oluşabilir; önce boş kalibrasyonu ve numune bağlantılarını kontrol edin.
- `RvmEndToEndAcceptance` canlı test bileşeni normal sahnede etkin değildir. Yalnız kendi test PLC'niz hazırken ekleyin; `reportPath` boş bırakılırsa raporlar Unity'nin kullanıcı veri klasörüne yazılır.
