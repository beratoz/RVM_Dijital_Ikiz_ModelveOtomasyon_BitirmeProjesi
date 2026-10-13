TYPE ST_RVMOutput106 :
STRUCT
    uiProtokolSurumu : UINT := 106;
    udiPlcSayaci : UDINT := 0;
    udiUnityOnayOturumu : UDINT := 0;
    udiGirisOnayi : UDINT := 0;
    udiKomutOnayi : UDINT := 0;
    udiIslemKimligi : UDINT := 0;
    uiDurum : UINT := 0;
    uiHataKodu : UINT := 0;
    uiTaninmisMalzeme : UINT := 0;
    uiHedefKutu : UINT := 0;
    xCalismaIzni : BOOL := FALSE;
    xKonveyorIleri : BOOL := FALSE;
    xKonveyorGeri : BOOL := FALSE;
    xKiriciCalis : BOOL := FALSE;
    xHazneHareket : BOOL := FALSE;
    xKapakAc : BOOL := FALSE;
    xTanimaOnayi : BOOL := FALSE;
    rBantHizi : REAL := 0.85;
    rKiriciDevri : REAL := 120.0;
    rHazneHizi : REAL := 1.5;
    rHazneHedefi : REAL := 0.0;
    rKapakAcisi : REAL := 75.0;
    udiPetSayaci : UDINT := 0;
    udiAluminyumSayaci : UDINT := 0;
    udiCamSayaci : UDINT := 0;
    udiReddedilenSayaci : UDINT := 0;
    udiPlcOturumu : UDINT := 0;
    uiKomutDurumu : UINT := 0;
    uiKomutNedeni : UINT := 0;
    udiPlcOperatorKomutKimligi : UDINT := 0;
    uiPlcOperatorKomutu : UINT := 0;
    uiPlcKomutDurumu : UINT := 0;
    uiPlcKomutNedeni : UINT := 0;
    uiSonKomutKaynagi : UINT := 0;
    udiNesneIstegi : UDINT := 0;
    uiNesneProfili : UINT := 0;
    xNesneIstegiEtkin : BOOL := FALSE;
    uiVeriModu : UINT := 1;
    uiDesteklenenModlar : UINT := 2;
    udiCikisBaslangic : UDINT := 0;
    udiCikisBitis : UDINT := 0;
    udiGoruntuIstegi : UDINT := 0;
    uiGoruntuKarari : UINT := 0;
END_STRUCT
END_TYPE