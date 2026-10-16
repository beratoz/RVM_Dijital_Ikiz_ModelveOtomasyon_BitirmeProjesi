// Same command priorities; separate instances for Unity and CODESYS HMI.
CASE uiKomutKodu OF
2: uiOncelik := 1;
3: uiOncelik := 2;
5: uiOncelik := 3;
1: uiOncelik := 4;
4: uiOncelik := 5;
ELSE uiOncelik := 99;
END_CASE;
IF udiCevrimSayaci = UDINT#4294967295 THEN
    udiCevrimSayaci := 1;
ELSE
    udiCevrimSayaci := udiCevrimSayaci + 1;
END_IF;
IF xYeniKomut THEN
    uiSonKomutKodu := uiKomutKodu;
    uiSonOncelik := uiOncelik;
    udiSonKomutKimligi := udiKomutKimligi;
END_IF;
