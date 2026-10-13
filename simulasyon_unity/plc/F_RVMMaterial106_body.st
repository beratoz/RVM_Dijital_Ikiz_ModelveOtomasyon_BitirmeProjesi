// Experimental calibrated rules, not a universal chemical identification.
F_RVMMaterial106 := 0;
IF NOT ((metal>=0) AND (metal<=1) AND (magnetic>=0) AND (magnetic<=0.1)
    AND (grams>=5) AND (grams<=1000) AND (transmission>=0) AND (transmission<=1)
    AND (reflection>=0) AND (reflection<=1) AND (length>=0.10) AND (length<=0.30)
    AND (width>=0.02) AND (width<=0.09)) THEN RETURN; END_IF;
IF metal>=0.5 THEN
    IF (transmission<=0.08) AND (grams>=8) AND (grams<=45) AND (length<=0.20) THEN
        F_RVMMaterial106 := 2;
    END_IF;
ELSIF metal<=0.1 THEN
    IF (grams>=10) AND (grams<=65) AND (transmission>=0.35) THEN F_RVMMaterial106 := 1;
    ELSIF (grams>=100) AND (grams<=800) AND (transmission>=0.15) AND (transmission<=0.85)
        AND (reflection>=0.08) THEN F_RVMMaterial106 := 3;
    END_IF;
END_IF;
