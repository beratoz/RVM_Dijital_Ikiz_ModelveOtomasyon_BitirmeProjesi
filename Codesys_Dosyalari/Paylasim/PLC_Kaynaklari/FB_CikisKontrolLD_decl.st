FUNCTION_BLOCK FB_CikisKontrolLD
VAR_INPUT
    // Diagnostic inputs: confirm scan updates without changing ladder logic.
    udiCevrimNo : UDINT;
    uiMakineDurumu : UINT;
	xCalismaIzni: BOOL;
	xBaglanti: BOOL;
	xVeriGecerli: BOOL;
	xAcilStop: BOOL;
	xSikisma: BOOL;
	xDurdur: BOOL;
	xGirisAsamasi: BOOL;
	xKabulAsamasi: BOOL;
	xIadeAsamasi: BOOL;
	xKirmaAsamasi: BOOL;
	xHazneAsamasi: BOOL;
	xEveDonusAsamasi: BOOL;
	xBosaltmaAsamasi: BOOL;
	xTanimaOnayi: BOOL;
	xKapakKapali: BOOL;
	xHedefte: BOOL;
END_VAR
VAR_OUTPUT
	xHareketIzni: BOOL;
	xIleriIstegi: BOOL;
	xGeriIstegi: BOOL;
	xKiriciCalis: BOOL;
	xHazneHareket: BOOL;
	xKapakAc: BOOL;
END_VAR
