# v106 PLC / Unity degisken sozlesmesi

Port 8766; OPC UA 4840. Tum alanlar GVL_RVMUnity altinda. PLC siralamasi ve komut onaylari korunur.

## input

| Alan | IEC tipi | Birim |
|---|---|---|
| uiUnityProtokol | UINT | code |
| udiUnityOturumu | UDINT | counter/id |
| udiUnitySayaci | UDINT | counter/id |
| udiKomutKimligi | UDINT | counter/id |
| uiOperatorKomutu | UINT | code |
| xGiris | BOOL | bool |
| xTanima | BOOL | bool |
| xKiriciGiris | BOOL | bool |
| xKirildi | BOOL | bool |
| xHazneDolu | BOOL | bool |
| xBosaltildi | BOOL | bool |
| xKapakAcik | BOOL | bool |
| xKapakKapali | BOOL | bool |
| xYolBos | BOOL | bool |
| xMetalAlgilandi | BOOL | bool |
| xSikisma | BOOL | bool |
| xAcilStop | BOOL | bool |
| xKutuPetDolu | BOOL | bool |
| xKutuAluminyumDolu | BOOL | bool |
| xKutuCamDolu | BOOL | bool |
| rKutleGram | REAL | g |
| rOptikGecirgenlik | REAL | ratio |
| rBoyMetre | REAL | m |
| rHazneKonumu | REAL | m |
| rGercekBantHizi | REAL | m/s |
| rGercekKiriciDevri | REAL | rpm |
| uiBosaltilanKutu | UINT | code |
| udiUrunKimligi | UDINT | counter/id |
| uiOperatorParametresi | UINT | code |
| udiGorulenPlcOturumu | UDINT | id |
| udiNesneOnayi | UDINT | id |
| uiNesneSonucu | UINT | code |
| udiNesneUrunKimligi | UDINT | id |
| uiPlantVeriKaynagi | UINT | code |
| xPlantVerisiGecerli | BOOL | bool |
| udiGoruntuOnayi | UDINT | 1 |
| udiGoruntuUrunu | UDINT | 1 |
| udiGoruntuPlcOturumu | UDINT | 1 |
| udiGoruntuUnityOturumu | UDINT | 1 |
| xGoruntuGecerli | BOOL | 1 |
| xGoruntuNesneVar | BOOL | 1 |
| rGoruntuBoy | REAL | m |
| rGoruntuEn | REAL | m |
| rGoruntuDoluluk | REAL | ratio |
| rGoruntuYasiMs | REAL | ms |
| xOlcumTartiGecerli | BOOL | valid |
| xOlcumProbGecerli | BOOL | valid |
| rOlcumYansima | REAL | index |
| rOlcumMetal | REAL | index |
| rOlcumManyetik | REAL | index |

## output

| Alan | IEC tipi | Birim |
|---|---|---|
| uiProtokolSurumu | UINT | code |
| udiPlcSayaci | UDINT | counter/id |
| udiUnityOnayOturumu | UDINT | counter/id |
| udiGirisOnayi | UDINT | counter/id |
| udiKomutOnayi | UDINT | counter/id |
| udiIslemKimligi | UDINT | counter/id |
| uiDurum | UINT | code |
| uiHataKodu | UINT | code |
| uiTaninmisMalzeme | UINT | code |
| uiHedefKutu | UINT | code |
| xCalismaIzni | BOOL | bool |
| xKonveyorIleri | BOOL | bool |
| xKonveyorGeri | BOOL | bool |
| xKiriciCalis | BOOL | bool |
| xHazneHareket | BOOL | bool |
| xKapakAc | BOOL | bool |
| xTanimaOnayi | BOOL | bool |
| rBantHizi | REAL | m/s |
| rKiriciDevri | REAL | rpm |
| rHazneHizi | REAL | m/s |
| rHazneHedefi | REAL | m |
| rKapakAcisi | REAL | degree |
| udiPetSayaci | UDINT | counter/id |
| udiAluminyumSayaci | UDINT | counter/id |
| udiCamSayaci | UDINT | counter/id |
| udiReddedilenSayaci | UDINT | counter/id |
| udiPlcOturumu | UDINT | id |
| uiKomutDurumu | UINT | code |
| uiKomutNedeni | UINT | code |
| udiPlcOperatorKomutKimligi | UDINT | id |
| uiPlcOperatorKomutu | UINT | code |
| uiPlcKomutDurumu | UINT | code |
| uiPlcKomutNedeni | UINT | code |
| uiSonKomutKaynagi | UINT | code |
| udiNesneIstegi | UDINT | id |
| uiNesneProfili | UINT | code |
| xNesneIstegiEtkin | BOOL | bool |
| uiVeriModu | UINT | code |
| uiDesteklenenModlar | UINT | bitmask |
| udiCikisBaslangic | UDINT | sequence |
| udiCikisBitis | UDINT | sequence |
| udiGoruntuIstegi | UDINT | 1 |
| uiGoruntuKarari | UINT | 1 |
