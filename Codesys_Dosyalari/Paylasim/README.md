# RVM CODESYS uygulaması

Bu klasör `RVM_Final_v1_0_0` uygulamasını içerir. Depodaki kurulum yollarını korumak için dosya adları `RVM_Unity_Dijital_Ikiz_v106` olarak tutulmuştur. Haberleşme protokolü 106'dır.

- **RVM_Unity_Dijital_Ikiz_v106.project**: CODESYS V3.5 SP22 Patch 3 ve Control Win V3 x64 3.5.22.30 için açılabilir proje. Kendi cihazınızı/bağlantınızı seçerek derleyip yükleyin; OPC UA hesabını ve sertifikalarını kendi bilgisayarınızda tanımlayın.
- **Application_v106.export**: Uygulamanın native aktarımı; Ladder ağlarını, ST kodlarını, görevleri, sembol yapılandırmasını ve operatör panelini birlikte korur.
- **RVM_Unity_Dijital_Ikiz_v106.plcopen.xml**: Uygulama kaynaklarının XML aktarımı.
- **RVM_Unity_Dijital_Ikiz_v106.Device.Application.xml**: Derlenmiş OPC UA sembolleri.
- **PLC_Kaynaklari/**: Güncel ST kodları ve Ladder bloklarının değişken bildirimleri. Ladder ağlarının tamamı `.project` ve `.export` dosyalarındadır.

`create_project.py` CODESYS Scripting içinde native aktarımdan yeni proje oluşturur. Mevcut dosyanın üzerine yazmaz; yeni çıktı için `RVM_CODESYS_OUTPUT` ortam değişkenini ayarlayın. Standart görselleştirme kütüphaneleri ve cihaz tanımı kurulu olmalıdır. Native aktarımda görülen `ProgramData/CODESYS` yolları standart kurulum kaynaklarına aittir.

Bu uygulamanın güncel kaynağı bu klasördeki proje/native aktarımdır. Depodaki `simulasyon_unity/plc/` önceki ST sürümünü içerir; bu proje yeniden üretilirken o dosyalar uygulanmaz. Unity ve Python köprüsü bu güncellemede değiştirilmemiştir.
