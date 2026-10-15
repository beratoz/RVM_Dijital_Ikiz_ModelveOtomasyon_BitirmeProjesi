# 24 üründen oluşan test nesneleri

RVM deneyleri için 6 PET, 6 alüminyum, 6 cam ve 6 kabul edilmeme senaryosundan oluşan Blender model seti. Modeller özgün ve temsilîdir; ticari ürünlerin birebir kopyası değildir.

## Kullanım

- **24_Test_Objesi_Katalog.blend**: 24 nesneyi isim ve gramajlarıyla birlikte gösteren toplu sahne. Katalog düzenini korumak için fizik dünyası kapalıdır.
- **blend/**: Her biri bağımsız açılabilen 24 Blender dosyası. Tek nesne, önizleme kamerası ve ışıklar içerir. Rigid Body etkindir; fizik denemesi için kendi zeminini veya bandını ekleyin.
- **gramaj_ve_olculer.csv**: Noktalı virgülle ayrılmış ölçü ve kütle tablosu.
- **manifest.json**: Ölçüler, kilogram kütleleri, fizik parametreleri ve beklenen test sonuçları.
- **previews/24_test_objesi.png**: Toplu önizleme.

Blender **5.2.2 LTS** ile açılarak doğrulandı. Dokular `.blend` dosyalarına gömülüdür; ayrıca doku klasörü veya kişisel dosya yolu gerekmez. Ölçek **1 Blender birimi = 1 metre**. Kütle `Physics Properties → Rigid Body → Mass` alanında kilogram olarak bulunur (16 g = 0,016 kg).

## Test kapsamı

PET, alüminyum ve cam gruplarında farklı boyutlarda altışar boş ambalaj vardır. Diğer altı örnek: HDPE deterjan şişesi, çelik konserve, yarım PET, kırık cam, içinde su kalan PET ve aşırı ezilmiş alüminyum kutudur. Ezilmiş kutunun reddi bu sete ait test politikasıdır; evrensel bir iade kuralı değildir.

Kütleler tartılmış ticari numunelerden alınmamıştır; gerçek ambalajlara yakın temsilî değerlerdir. Nominal kapasite, mesh üzerinden hesaplanmış hacim değildir. `CONVEX_HULL` çarpışma yaklaşımı, kalibre edilmemiş sürtünme/sekme değerleri ve yaklaşık ağırlık merkezi kullanılır. Kırılma, ezilme ve sıvı çalkalanması çalışma anında simüle edilmez. Barkodlar dekoratiftir.

`material`, `expected_accept` ve `expected_bin` alanları testin beklenen cevabıdır; PLC veya kamera için tanıma girdisi olarak kullanılmamalıdır. Bu modeller mevcut Unity/CODESYS sahnesine otomatik bağlanmaz. Unity'ye aktarım için Blender'dan FBX/glTF dışa aktarın; kütle, collider ve sanal prob özelliklerini hedef sahnede ayrıca tanımlayın.

Format ve ölçü dayanakları `sources.json` ve her nesnenin `reference_urls` alanındadır. Bu kaynaklar tüm kütlelerin ölçülerek doğrulandığı anlamına gelmez.
