FUNCTION_BLOCK FB_MalzemeSiniflandirma106
VAR_INPUT
    rMetal : REAL;
    rManyetik : REAL;
    rKutleGram : REAL;
    rGecirgenlik : REAL;
    rYansima : REAL;
    rBoyMetre : REAL;
    rEnMetre : REAL;
    rDoluluk : REAL;
    xVeriYasiUygun : BOOL := TRUE;
    xSensorlerGecerli : BOOL := TRUE;
    xSekilUygun : BOOL := TRUE;
    udiUrunKimligi : UDINT;
    udiPlcOturumu : UDINT;
    udiUnityOturumu : UDINT;
    udiGoruntuIstegi : UDINT;
END_VAR
VAR_OUTPUT
    xDegerlendirildi : BOOL;
    udiDegerlendirmeSayaci : UDINT;
    uiMalzeme : UINT;
    uiGoruntuKarari : UINT;
    uiRetNedeni : UINT;
    rBoyCm : REAL;
    rEnCm : REAL;
    rYuzeyAlani : REAL;
    rHacim : REAL;
    rKabukYogunluk : REAL;
    rHacimYogunluk : REAL;
    xTemelSinirlarUygun : BOOL;
    xAlanUygun : BOOL;
    xPetKosullari : BOOL;
    xCamKosullari : BOOL;
    xAluminyumKosullari : BOOL;
    xMetalBelirsiz : BOOL;
END_VAR
