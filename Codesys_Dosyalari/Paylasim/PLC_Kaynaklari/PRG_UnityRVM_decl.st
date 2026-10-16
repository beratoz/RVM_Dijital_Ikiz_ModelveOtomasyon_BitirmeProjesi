PROGRAM PRG_UnityRVM
VAR
    control : FB_RVMControl106;
    candidate : ST_RVMInput106;
    snapshot : ST_RVMInput106;
    op : ST_RVMOperator106;
    initialized : BOOL;
    epoch : UDINT;
    beginMarker : UDINT;
    lastMarker : UDINT;
    outputMarker : UDINT;
    fresh : BOOL;
END_VAR
