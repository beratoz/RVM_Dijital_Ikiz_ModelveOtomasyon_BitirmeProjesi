FUNCTION_BLOCK FB_RVMControl106
VAR_INPUT
    inp : ST_RVMInput106;
    op : ST_RVMOperator106;
    fresh : BOOL;
    epoch : UDINT;
END_VAR
VAR_OUTPUT
    q : ST_RVMOutput106;
END_VAR
VAR
    a : ST_RVMInput106;
    initialized : BOOL;
    session : UDINT;
    packet : UDINT;
    heartbeat : BOOL;
    synchronized : BOOL;
    sessionChanged : BOOL;
    dataOK : BOOL;
    linked : BOOL;
    home : BOOL;
    emptyHome : BOOL;
    emergency : BOOL;
    stopLevel : BOOL;
    targetFull : BOOL;
    faultNow : UINT;
    previousState : UINT;
    previousButtons : ARRAY[1..5] OF BOOL;
    armed : ARRAY[1..5] OF BOOL;
    buttons : ARRAY[1..5] OF BOOL;
    edges : ARRAY[1..5] OF BOOL;
    newRequest : ARRAY[1..2] OF BOOL;
    cmdId : ARRAY[1..2] OF UDINT;
    cmdCode : ARRAY[1..2] OF UINT;
    cmdParam : ARRAY[1..2] OF UINT;
    cachedStatus : ARRAY[1..2] OF UINT;
    cachedReason : ARRAY[1..2] OF UINT;
    replyId : ARRAY[1..2] OF UDINT;
    replyStatus : ARRAY[1..2] OF UINT;
    replyReason : ARRAY[1..2] OF UINT;
    source : INT;
    chosen : INT;
    other : INT;
    i : INT;
    hmiCode : UINT;
    hmiParam : UINT;
    resultStatus : UINT;
    resultReason : UINT;
    pendingSource : INT;
    pendingId : UDINT;
    insertConfirmed : BOOL;
    confirmedProduct : UDINT;
    lastProduct : UDINT;
    waitLink : TON;
    waitStage : TON;
    waitRecognition : TON;
    waitVision : TON;
    visionMatch : BOOL;
    visionShape : BOOL;
    waitCycle : TON;
    waitInsert : TON;
    fbMalzeme : FB_MalzemeSiniflandirma106;
    fbUnityOncelik : FB_KomutOnceligi106;
    fbCodesysOncelik : FB_KomutOnceligi106;
    fbCikisLD : FB_CikisKontrolLD;
    fbKonveyorIzin : FB_KonveyorIzin;
END_VAR
