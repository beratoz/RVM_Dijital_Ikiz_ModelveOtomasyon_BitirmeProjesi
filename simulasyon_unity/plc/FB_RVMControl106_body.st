// Single process owner. No actuator command is derived from a UI bit directly.
q.uiProtokolSurumu := 106;
q.udiPlcOturumu := epoch;
q.uiDesteklenenModlar := 2; // Render camera + simulated physical sensors only.
q.uiVeriModu := 1;
q.rBantHizi := 0.85;
q.rKiriciDevri := 120.0;
q.rHazneHizi := 1.5;
q.rKapakAcisi := 75.0;
q.udiPlcSayaci := q.udiPlcSayaci + 1;
heartbeat := FALSE;
sessionChanged := FALSE;
newRequest[1] := FALSE;
newRequest[2] := FALSE;
insertConfirmed := FALSE;
faultNow := 0;
IF NOT initialized THEN
    initialized := TRUE;
    q.uiDurum := 0;
    q.xCalismaIzni := FALSE;
    q.rHazneHedefi := 0.225;
END_IF;
IF epoch = 0 THEN faultNow := 313; END_IF;

// A zero-epoch HELLO is mandatory before this PLC incarnation accepts commands.
// The HELLO'inp command watermark is consumed, never executed.
IF fresh AND (inp.udiUnityOturumu <> 0) AND (inp.udiUnitySayaci <> 0) THEN
    IF (inp.udiGorulenPlcOturumu = 0) AND ((session <> inp.udiUnityOturumu) OR NOT synchronized) THEN
        a := inp;
        sessionChanged := session <> inp.udiUnityOturumu;
        session := inp.udiUnityOturumu;
        lastProduct := 0;
        packet := inp.udiUnitySayaci;
        synchronized := TRUE;
        cmdId[1] := inp.udiKomutKimligi;
        cmdCode[1] := inp.uiOperatorKomutu;
        cmdParam[1] := inp.uiOperatorParametresi;
        cachedStatus[1] := 5;
        cachedReason[1] := 10;
        replyId[1] := cmdId[1];
        replyStatus[1] := 5;
        replyReason[1] := 10;
        q.xCalismaIzni := FALSE;
        IF q.uiHataKodu = 0 THEN q.uiDurum := 0; END_IF;
    ELSIF synchronized AND (session = inp.udiUnityOturumu)
        AND (inp.udiGorulenPlcOturumu = epoch) AND (inp.udiUnitySayaci > packet) THEN
        packet := inp.udiUnitySayaci;
        a := inp;
        heartbeat := TRUE;
        IF inp.udiKomutKimligi > cmdId[1] THEN
            cmdId[1] := inp.udiKomutKimligi;
            cmdCode[1] := inp.uiOperatorKomutu;
            cmdParam[1] := inp.uiOperatorParametresi;
            newRequest[1] := TRUE;
        ELSIF (inp.udiKomutKimligi <> cmdId[1]) OR (inp.uiOperatorKomutu <> cmdCode[1])
            OR (inp.uiOperatorParametresi <> cmdParam[1]) THEN
            replyId[1] := inp.udiKomutKimligi;
            replyStatus[1] := 3;
            replyReason[1] := 13;
        ELSE
            replyId[1] := cmdId[1];
            replyStatus[1] := cachedStatus[1];
            replyReason[1] := cachedReason[1];
        END_IF;
    END_IF;
END_IF;
q.udiUnityOnayOturumu := session;
waitLink(IN := NOT heartbeat, PT := T#750MS);
linked := synchronized AND (session = a.udiUnityOturumu) AND (a.udiGorulenPlcOturumu = epoch)
    AND (epoch <> 0) AND NOT waitLink.Q AND (a.uiUnityProtokol = 106);
home := ABS(a.rHazneKonumu - 0.225) < 0.008;
emptyHome := a.xYolBos AND NOT a.xGiris AND a.xKapakKapali AND NOT a.xKapakAcik AND home;
emergency := op.xAcilStop OR a.xAcilStop;
// Comparisons reject NaN as well as out-of-range values. No invalid data fallback.
dataOK := a.xPlantVerisiGecerli AND (a.uiPlantVeriKaynagi = 1)
    AND (a.rHazneKonumu >= -0.770001) AND (a.rHazneKonumu <= 0.310001)
    AND (a.rKutleGram >= 0.0) AND (a.rKutleGram <= 10000.0)
    AND (a.rBoyMetre >= 0.0) AND (a.rBoyMetre <= 2.000001)
    AND (a.rOptikGecirgenlik >= 0.0) AND (a.rOptikGecirgenlik <= 1.000001)
    AND (a.rGercekBantHizi >= -1.200001) AND (a.rGercekBantHizi <= 1.200001)
    AND (a.rGercekKiriciDevri >= 0.0) AND (a.rGercekKiriciDevri <= 180.0001)
    AND NOT (a.xKapakAcik AND a.xKapakKapali)
    AND NOT (a.xYolBos AND a.xGiris) AND (a.uiBosaltilanKutu <= 3);

// HMI: arm only after FALSE has been observed; held or startup TRUE cannot repeat.
buttons[1] := op.xBaslat; buttons[2] := op.xDurdur; buttons[3] := op.xSifirla;
buttons[4] := op.xNesneEkle; buttons[5] := op.xModUygula;
FOR i := 1 TO 5 DO
    IF NOT buttons[i] THEN armed[i] := TRUE; END_IF;
    edges[i] := armed[i] AND buttons[i] AND NOT previousButtons[i];
    previousButtons[i] := buttons[i];
END_FOR;
hmiCode := 0; hmiParam := 0;
IF edges[2] THEN hmiCode := 2;
ELSIF edges[3] THEN hmiCode := 3;
ELSIF edges[5] THEN hmiCode := 5; hmiParam := op.uiSeciliMod;
ELSIF edges[1] THEN hmiCode := 1;
ELSIF edges[4] THEN hmiCode := 4; hmiParam := op.uiSeciliNesne;
END_IF;
IF hmiCode <> 0 THEN
    IF cmdId[2] = UDINT#4294967295 THEN faultNow := 313;
    ELSE
        cmdId[2] := cmdId[2] + 1;
        cmdCode[2] := hmiCode; cmdParam[2] := hmiParam;
        newRequest[2] := TRUE;
    END_IF;
END_IF;
stopLevel := op.xDurdur OR (newRequest[1] AND (cmdCode[1] = 2));

// Fault priority is stable; reconnecting Unity never clears a latched fault.
IF q.xCalismaIzni THEN
    IF emergency THEN faultNow := 303;
    // Unity also raises its local motion interlock on an identity mismatch.
    // Preserve the specific cause while the session is valid, before generic jam/data flags.
    ELSIF linked AND (q.uiDurum >= 20) AND (q.uiDurum <= 100) AND (a.udiUrunKimligi <> q.udiIslemKimligi) THEN faultNow := 305;
    ELSIF a.xSikisma THEN faultNow := 304;
    ELSIF a.uiUnityProtokol <> 106 THEN faultNow := 302;
    ELSIF NOT linked THEN faultNow := 301;
    ELSIF NOT dataOK THEN faultNow := 310;
    END_IF;
END_IF;
waitStage(IN := (q.uiDurum = previousState) AND (q.uiDurum >= 20) AND (q.uiDurum <= 100), PT := T#8S);
previousState := q.uiDurum;
waitRecognition(IN := q.uiDurum = 30, PT := T#200MS);
waitVision(IN := q.uiDurum = 30, PT := T#4S);
visionMatch := (a.udiGoruntuOnayi = q.udiGoruntuIstegi) AND (q.udiGoruntuIstegi <> 0)
    AND (a.udiGoruntuUrunu = q.udiIslemKimligi) AND (a.udiGoruntuPlcOturumu = epoch)
    AND (a.udiGoruntuUnityOturumu = session);
visionShape := a.xGoruntuNesneVar AND (a.rGoruntuBoy >= 0.10) AND (a.rGoruntuBoy <= 0.30)
    AND (a.rGoruntuEn >= 0.02) AND (a.rGoruntuEn <= 0.09)
    AND (a.rGoruntuBoy >= 1.5*a.rGoruntuEn) AND (a.rGoruntuBoy <= 7.0*a.rGoruntuEn)
    AND (a.rGoruntuDoluluk >= 0.35) AND (a.rGoruntuDoluluk <= 1.0);
waitCycle(IN := (q.uiDurum >= 20) AND (q.uiDurum <= 90), PT := T#5600MS);
waitInsert(IN := q.xNesneIstegiEtkin, PT := T#2S);
IF waitStage.Q AND (faultNow = 0) THEN faultNow := 306; END_IF;
IF faultNow <> 0 AND q.uiHataKodu = 0 THEN q.uiHataKodu := faultNow; END_IF;
IF stopLevel OR emergency OR sessionChanged OR (q.uiHataKodu <> 0) THEN
    q.xCalismaIzni := FALSE;
    IF q.uiHataKodu = 0 THEN q.uiDurum := 0; ELSE q.uiDurum := 900; END_IF;
    IF q.xNesneIstegiEtkin THEN
        cachedStatus[pendingSource] := 5; cachedReason[pendingSource] := 1;
        IF replyId[pendingSource] = pendingId THEN
            replyStatus[pendingSource] := 5; replyReason[pendingSource] := 1;
        END_IF;
    END_IF;
    q.xNesneIstegiEtkin := FALSE;
END_IF;

// Arbitration is deterministic. No deferred queue can start work after a stop.
chosen := 0;
IF newRequest[2] THEN chosen := 2; END_IF;
IF newRequest[1] THEN
    IF chosen = 0 THEN chosen := 1;
    ELSIF F_RVMPriority106(cmdCode[1]) < F_RVMPriority106(cmdCode[2]) THEN chosen := 1;
    END_IF;
END_IF;
FOR source := 1 TO 2 DO
    IF newRequest[source] THEN
        resultStatus := 3; resultReason := 0;
        replyId[source] := cmdId[source];
        IF (source <> chosen) AND NOT (cmdCode[source] = 2) THEN
            IF stopLevel THEN resultReason := 1; ELSE resultReason := 9; END_IF;
        ELSIF cmdCode[source] = 2 THEN
            q.xCalismaIzni := FALSE;
            IF q.uiHataKodu = 0 THEN q.uiDurum := 0; ELSE q.uiDurum := 900; END_IF;
            resultStatus := 2;
        ELSIF (cmdCode[source] = 0) OR (cmdCode[source] > 5)
            OR ((cmdCode[source] <= 3) AND (cmdParam[source] <> 0))
            OR ((cmdCode[source] = 4) AND (cmdParam[source] > 3))
            OR ((cmdCode[source] = 5) AND (cmdParam[source] > 2)) THEN resultReason := 7;
        ELSIF stopLevel THEN resultReason := 1;
        ELSIF emergency OR a.xSikisma THEN resultReason := 5;
        ELSIF NOT linked OR NOT dataOK THEN resultReason := 2;
        ELSIF q.xNesneIstegiEtkin THEN resultReason := 6;
        ELSE
            CASE cmdCode[source] OF
            1:
                IF q.uiHataKodu <> 0 THEN resultReason := 5;
                ELSIF q.uiDurum <> 0 THEN resultReason := 3;
                ELSIF NOT emptyHome THEN resultReason := 4;
                ELSE q.xCalismaIzni := TRUE; q.uiDurum := 10; resultStatus := 2;
                END_IF;
            3:
                IF q.xCalismaIzni OR ((q.uiDurum <> 0) AND (q.uiDurum <> 900)) THEN resultReason := 3;
                ELSIF NOT emptyHome THEN resultReason := 4;
                ELSIF epoch = 0 THEN resultReason := 10;
                ELSE q.uiHataKodu := 0; q.uiDurum := 0; q.xCalismaIzni := FALSE; resultStatus := 2;
                END_IF;
            4:
                IF q.uiHataKodu <> 0 THEN resultReason := 5;
                ELSIF (q.uiDurum <> 10) OR NOT q.xCalismaIzni THEN resultReason := 3;
                ELSIF NOT emptyHome THEN resultReason := 4;
                ELSIF q.udiNesneIstegi = UDINT#4294967295 THEN resultReason := 10;
                ELSE
                    q.udiNesneIstegi := q.udiNesneIstegi + 1;
                    q.uiNesneProfili := cmdParam[source];
                    q.xNesneIstegiEtkin := TRUE;
                    pendingSource := source; pendingId := cmdId[source];
                    resultStatus := 1;
                END_IF;
            5:
                IF cmdParam[source] <> 1 THEN resultReason := 8;
                ELSIF (q.uiDurum <> 0) OR q.xCalismaIzni THEN resultReason := 3;
                ELSIF NOT emptyHome THEN resultReason := 4;
                ELSE q.uiVeriModu := 1; resultStatus := 2;
                END_IF;
            END_CASE;
        END_IF;
        cachedStatus[source] := resultStatus; cachedReason[source] := resultReason;
        replyStatus[source] := resultStatus; replyReason[source] := resultReason;
        IF source = chosen THEN q.uiSonKomutKaynagi := INT_TO_UINT(source); END_IF;
    END_IF;
END_FOR;

// Product admission is tied to the PLC-issued request, not just a presence bit.
IF q.xNesneIstegiEtkin THEN
    resultStatus := 1; resultReason := 0;
    IF waitInsert.Q THEN
        resultStatus := 4; resultReason := 11;
        q.uiHataKodu := 312; // A delayed physical creation must require explicit recovery.
    ELSIF heartbeat AND (a.udiNesneOnayi = q.udiNesneIstegi) THEN
        IF a.uiNesneSonucu = 2 THEN resultStatus := 3; resultReason := 12;
        ELSIF a.uiNesneSonucu = 1 THEN
            IF (a.udiNesneUrunKimligi <> 0) AND (a.udiNesneUrunKimligi = a.udiUrunKimligi)
                AND (a.udiUrunKimligi > lastProduct) AND a.xGiris AND NOT a.xYolBos THEN
                confirmedProduct := a.udiUrunKimligi;
                lastProduct := a.udiUrunKimligi;
                insertConfirmed := TRUE;
                resultStatus := 2;
            ELSE resultStatus := 3; resultReason := 12; q.uiHataKodu := 312;
            END_IF;
        END_IF;
    END_IF;
    IF resultStatus <> 1 THEN
        q.xNesneIstegiEtkin := FALSE;
        IF cmdId[pendingSource] = pendingId THEN
            cachedStatus[pendingSource] := resultStatus; cachedReason[pendingSource] := resultReason;
        END_IF;
        IF replyId[pendingSource] = pendingId THEN
            replyStatus[pendingSource] := resultStatus; replyReason[pendingSource] := resultReason;
        END_IF;
    END_IF;
END_IF;

targetFull := ((q.uiHedefKutu = 1) AND a.xKutuPetDolu)
    OR ((q.uiHedefKutu = 2) AND a.xKutuAluminyumDolu)
    OR ((q.uiHedefKutu = 3) AND a.xKutuCamDolu);
IF q.xCalismaIzni AND (q.uiDurum >= 40) AND (q.uiDurum <= 70) AND targetFull THEN q.uiHataKodu := 308; END_IF;
IF q.uiHataKodu = 0 AND q.xCalismaIzni THEN
    CASE q.uiDurum OF
    10:
        q.xTanimaOnayi := FALSE; q.uiTaninmisMalzeme := 0; q.uiHedefKutu := 0;
        IF insertConfirmed THEN q.udiIslemKimligi := confirmedProduct; q.uiDurum := 20;
        ELSIF NOT a.xYolBos AND NOT q.xNesneIstegiEtkin THEN q.uiHataKodu := 312;
        END_IF;
    20:
        IF a.xTanima THEN
            IF q.udiGoruntuIstegi = UDINT#4294967295 THEN q.uiHataKodu := 313;
            ELSE q.udiGoruntuIstegi := q.udiGoruntuIstegi+1; q.uiGoruntuKarari := 0; q.uiDurum := 30;
            END_IF;
        END_IF;
    30:
        IF NOT a.xTanima THEN q.uiHataKodu := 307;
        ELSIF waitVision.Q THEN
            q.uiGoruntuKarari := 5; q.uiTaninmisMalzeme := 0; q.uiHedefKutu := 0; q.uiDurum := 100;
        ELSIF waitRecognition.Q AND visionMatch THEN
            q.uiTaninmisMalzeme := 0;
            IF NOT ((a.rGoruntuYasiMs >= 0.0) AND (a.rGoruntuYasiMs <= 2000.0)) THEN q.uiGoruntuKarari := 4;
            ELSIF NOT a.xGoruntuGecerli OR NOT a.xOlcumTartiGecerli OR NOT a.xOlcumProbGecerli THEN q.uiGoruntuKarari := 3;
            ELSIF NOT visionShape THEN q.uiGoruntuKarari := 2;
            ELSE
                q.uiTaninmisMalzeme := F_RVMMaterial106(a.rOlcumMetal, a.rOlcumManyetik, a.rKutleGram, a.rOptikGecirgenlik, a.rOlcumYansima, a.rGoruntuBoy, a.rGoruntuEn);
                IF q.uiTaninmisMalzeme = 0 THEN q.uiGoruntuKarari := 2; ELSE q.uiGoruntuKarari := 1; END_IF;
            END_IF;
            q.uiHedefKutu := q.uiTaninmisMalzeme;
            CASE q.uiHedefKutu OF
            1: q.rHazneHedefi := -0.23;
            2: q.rHazneHedefi := -0.77;
            3: q.rHazneHedefi := 0.31;
            END_CASE;
            IF q.uiHedefKutu = 0 THEN q.uiDurum := 100;
            ELSIF ((q.uiHedefKutu=1) AND a.xKutuPetDolu) OR ((q.uiHedefKutu=2) AND a.xKutuAluminyumDolu)
                OR ((q.uiHedefKutu=3) AND a.xKutuCamDolu) THEN q.uiHataKodu := 308;
            ELSE q.xTanimaOnayi := TRUE; q.uiDurum := 40;
            END_IF;
        END_IF;
    40:
        IF NOT a.xKapakKapali THEN q.uiHataKodu := 310;
        ELSIF a.xKiriciGiris THEN q.uiDurum := 50; END_IF;
    50:
        IF a.xKirildi AND a.xHazneDolu AND a.xKapakKapali THEN q.uiDurum := 60; END_IF;
    60:
        IF NOT a.xKapakKapali OR NOT a.xHazneDolu THEN q.uiHataKodu := 310;
        ELSIF ABS(a.rHazneKonumu-q.rHazneHedefi)<0.008 THEN q.uiDurum := 70; END_IF;
    70:
        IF ABS(a.rHazneKonumu-q.rHazneHedefi)>=0.008 THEN q.uiHataKodu := 310;
        ELSIF a.xBosaltildi AND a.xKapakAcik AND NOT a.xHazneDolu AND a.xYolBos THEN
            IF a.uiBosaltilanKutu <> q.uiHedefKutu THEN q.uiHataKodu := 309;
            ELSE q.uiDurum := 80; END_IF;
        END_IF;
    80:
        IF a.xKapakKapali AND NOT a.xKapakAcik AND a.xYolBos AND NOT a.xHazneDolu THEN
            CASE q.uiHedefKutu OF
            1: q.udiPetSayaci := q.udiPetSayaci + 1;
            2: q.udiAluminyumSayaci := q.udiAluminyumSayaci + 1;
            3: q.udiCamSayaci := q.udiCamSayaci + 1;
            END_CASE;
            q.uiDurum := 90;
        END_IF;
    90:
        q.rHazneHedefi := 0.225;
        IF NOT a.xKapakKapali THEN q.uiHataKodu := 310;
        ELSIF home AND a.xYolBos AND waitCycle.Q THEN q.uiDurum := 10; END_IF;
    100:
        IF a.xYolBos THEN q.udiReddedilenSayaci := q.udiReddedilenSayaci+1; q.uiDurum := 10; END_IF;
    END_CASE;
END_IF;

// Apply fault dominance AFTER state processing, in this same scan.
IF q.uiHataKodu <> 0 THEN q.uiDurum := 900; q.xCalismaIzni := FALSE; q.xNesneIstegiEtkin := FALSE; END_IF;
q.xKonveyorIleri := FALSE; q.xKonveyorGeri := FALSE; q.xKiriciCalis := FALSE;
q.xHazneHareket := FALSE; q.xKapakAc := FALSE;
IF q.xCalismaIzni AND linked AND dataOK AND NOT emergency AND NOT a.xSikisma AND NOT stopLevel THEN
    q.xKonveyorIleri := (q.uiDurum=20) OR (q.uiDurum=40);
    q.xKonveyorGeri := q.uiDurum=100;
    q.xKiriciCalis := ((q.uiDurum=40) OR (q.uiDurum=50)) AND q.xTanimaOnayi;
    q.xHazneHareket := ((q.uiDurum=60) OR (q.uiDurum=90)) AND a.xKapakKapali;
    q.xKapakAc := (q.uiDurum=70) AND (ABS(a.rHazneKonumu-q.rHazneHedefi)<0.008);
END_IF;
IF NOT q.xCalismaIzni THEN q.xTanimaOnayi := FALSE; END_IF;
q.udiKomutOnayi := replyId[1]; q.uiKomutDurumu := replyStatus[1]; q.uiKomutNedeni := replyReason[1];
q.udiPlcOperatorKomutKimligi := replyId[2]; q.uiPlcOperatorKomutu := cmdCode[2];
q.uiPlcKomutDurumu := replyStatus[2]; q.uiPlcKomutNedeni := replyReason[2];
