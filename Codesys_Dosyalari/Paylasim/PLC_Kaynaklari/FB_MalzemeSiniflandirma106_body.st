// Last completed classification attempt. Inputs and results remain visible.
// Each attempt clears intermediate values, including early rejection paths.
xDegerlendirildi := TRUE;
IF udiDegerlendirmeSayaci = UDINT#4294967295 THEN
    udiDegerlendirmeSayaci := 1;
ELSE
    udiDegerlendirmeSayaci := udiDegerlendirmeSayaci + 1;
END_IF;
uiMalzeme := 0; uiGoruntuKarari := 2; uiRetNedeni := 0;
rBoyCm := 0; rEnCm := 0; rYuzeyAlani := 0; rHacim := 0;
rKabukYogunluk := 0; rHacimYogunluk := 0;
xTemelSinirlarUygun := FALSE; xAlanUygun := FALSE;
xPetKosullari := FALSE; xCamKosullari := FALSE;
xAluminyumKosullari := FALSE; xMetalBelirsiz := FALSE;

// Same order as the previous controller guards.
IF NOT xVeriYasiUygun THEN uiRetNedeni := 10; uiGoruntuKarari := 4; RETURN; END_IF;
IF NOT xSensorlerGecerli THEN uiRetNedeni := 11; uiGoruntuKarari := 3; RETURN; END_IF;
IF NOT xSekilUygun THEN uiRetNedeni := 12; RETURN; END_IF;

xTemelSinirlarUygun := (rMetal>=0) AND (rMetal<=1) AND (rManyetik>=0) AND (rManyetik<=0.1)
    AND (rKutleGram>=5) AND (rKutleGram<=1000) AND (rGecirgenlik>=0) AND (rGecirgenlik<=1)
    AND (rYansima>=0) AND (rYansima<=1) AND (rBoyMetre>=0.10) AND (rBoyMetre<=0.30)
    AND (rEnMetre>=0.02) AND (rEnMetre<=0.09) AND (rDoluluk>0) AND (rDoluluk<=1);
IF NOT xTemelSinirlarUygun THEN uiRetNedeni := 1; RETURN; END_IF;
rBoyCm := rBoyMetre * 100.0;
rEnCm := rEnMetre * 100.0;
rYuzeyAlani := 3.14159265 * rEnCm * rBoyCm * rDoluluk;
xAlanUygun := rYuzeyAlani > 1.0;
IF NOT xAlanUygun THEN uiRetNedeni := 2; RETURN; END_IF;
rHacim := rYuzeyAlani * rEnCm / 4.0;
rKabukYogunluk := rKutleGram / rYuzeyAlani;
rHacimYogunluk := rKutleGram / rHacim;

xAluminyumKosullari := (rMetal>=0.5) AND (rGecirgenlik<=0.08)
    AND (rKutleGram>=8) AND (rKutleGram<=45) AND (rBoyMetre<=0.20);
xPetKosullari := (rMetal<=0.1) AND (rKutleGram>=8) AND (rKutleGram<=75)
    AND (rKabukYogunluk>=0.040) AND (rKabukYogunluk<=0.095) AND (rGecirgenlik>=0.35);
xCamKosullari := (rMetal<=0.1) AND (rKutleGram>=100) AND (rKutleGram<=800)
    AND (rKabukYogunluk>=0.450) AND (rKabukYogunluk<=0.680) AND (rDoluluk>=0.875)
    AND (rGecirgenlik>=0.45) AND (rGecirgenlik<=0.85) AND (rYansima>=0.12);
xMetalBelirsiz := (rMetal>0.1) AND (rMetal<0.5);
IF rMetal>=0.5 THEN
    IF xAluminyumKosullari THEN uiMalzeme := 2; ELSE uiRetNedeni := 3; END_IF;
ELSIF rMetal<=0.1 THEN
    IF xPetKosullari THEN uiMalzeme := 1;
    ELSIF xCamKosullari THEN uiMalzeme := 3;
    ELSE uiRetNedeni := 5;
    END_IF;
ELSE
    uiRetNedeni := 4;
END_IF;
IF uiMalzeme<>0 THEN uiGoruntuKarari := 1; END_IF;
