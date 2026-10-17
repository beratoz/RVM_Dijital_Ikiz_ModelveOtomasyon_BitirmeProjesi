using System;
using UnityEngine;
namespace RVM.Experimental
{
    // Logged diagnostics only; the PLC remains the decision authority.
    public static class RvmKabukGozlemi
    {
        [Serializable] public sealed class Sonuc
        {
            public string surum="kabuk-kantil-deney-v1", neden;
            public bool gecerli;
            public double yuzeyCm2,hacimCm3,kabukGramCm2,hacimGramCm3;
            public float kutleGram,boyMetre,enMetre,doluluk;
            public float petAlt=.040f,petUst=.095f,camAlt=.450f,camUst=.680f;
        }
        public static Sonuc Hesapla(float m,float h,float w,float d)
        {
            var s=new Sonuc{kutleGram=m,boyMetre=h,enMetre=w,doluluk=d};
            foreach(float v in new[]{m,h,w,d})if(float.IsNaN(v)||float.IsInfinity(v)||v<=0){s.neden="Invalid measurement";return s;}
            if(d>1){s.neden="Invalid fill";return s;}
            s.yuzeyCm2=Math.PI*(w*100.0)*(h*100.0)*d;
            s.hacimCm3=s.yuzeyCm2*(w*100.0)/4;
            if(s.yuzeyCm2<=1){s.neden="Insufficient area";return s;}
            s.kabukGramCm2=m/s.yuzeyCm2;s.hacimGramCm3=m/s.hacimCm3;s.gecerli=true;
            s.neden="Experimental dimensional indices; not true material density";return s;
        }
    }
}
