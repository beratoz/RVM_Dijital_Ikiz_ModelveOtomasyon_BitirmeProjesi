# Protokol 106 haberleşme kaynakları

- `bridge.py`: yalnız yerel bilgisayarda 8766 portunu dinleyen HTTP / OPC UA köprüsü.
- `contract.json`: alanlar, tipler, fiziksel sınırlar ve uygulama sürümü.
- `plc/`: v106 Structured Text kaynakları; sıralama `FB_RVMControl106`, malzeme kararı `F_RVMMaterial106` içindedir.
- `test_contract.py`: paket doğrulama, tekrar/oturum kuralları ve derlenmiş PLC sembol eşleşmesi testleri.
- `Kurulum.ps1`, `Baslat.ps1`, `Durdur.ps1`: taşınabilir Python ortamı ve yerel köprü yönetimi.

Klasör yolları betiğin konumundan hesaplanır. Kendi OPC UA hesabınızı veya sunucu adresinizi `ayarlar.local.ps1` içinde tanımlayabilirsiniz; bu dosya Git takibinden dışlanır. `localhost` ve `127.0.0.1` herkesin kendi bilgisayarını gösterir.

[Kurulum](../KURULUM.md) · [Alan listesi](SINYALLER.md) · [Mimari](../README.md)

PLC kaynakları değiştiğinde CODESYS projesini de yeniden üretin. Alan değişiklikleri `contract.json`, PLC veri yapıları ve `Unity/RVM/Assets/RVM/Scripts/RvmContract.cs` arasında birlikte uygulanmalıdır. Eski protokol 103 üretim betiği bu sürümde kullanılmaz.
