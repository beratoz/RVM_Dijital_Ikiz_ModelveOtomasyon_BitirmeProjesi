using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVM.Experimental
{
    // Experimental rank-trimmed estimator. Quantiles do not identify bare material.
    public static class RvmKantilOptik
    {
        public const string Surum="tum-maske-sira60-90-v1";
        [Serializable] public sealed class Sonuc
        {
            public string surum=Surum, neden;
            public bool gecerli;
            public int piksel, baslangic, bitisHaric, seciliPiksel;
            public float gecirgenlik,yansima,p20,p85,optikYayilim;
        }
        static float L(Color32 c)=>(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
        public static Sonuc Hesapla(Color32[] eb,Color32[] ew,Color32[] b,Color32[] w,Color32[] mask,out Color32[] secim)
        {
            var s=new Sonuc();secim=new Color32[mask.Length];
            if(eb.Length!=mask.Length||ew.Length!=mask.Length||b.Length!=mask.Length||w.Length!=mask.Length)
                throw new ArgumentException("Image sizes differ");
            var indices=new List<int>();var t=new float[mask.Length];var y=new float[mask.Length];
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24){
                float referans=L(ew[i])-L(eb[i]);
                if(referans<=.2f){s.neden="Invalid reference inside mask";return s;}
                t[i]=Mathf.Clamp01((L(w[i])-L(b[i]))/referans);
                y[i]=Mathf.Clamp01((L(b[i])-L(eb[i]))/referans);indices.Add(i);
            }
            s.piksel=indices.Count;
            if(s.piksel<32){s.neden="Insufficient mask pixels";return s;}
            // Stable rank ties across C#/Python float arithmetic; average original values.
            indices.Sort((i,j)=>{int c=Math.Floor((double)t[i]*10000+.5).CompareTo(Math.Floor((double)t[j]*10000+.5));return c!=0?c:i.CompareTo(j);});
            s.baslangic=(int)Math.Floor(.6*s.piksel);s.bitisHaric=(int)Math.Floor(.9*s.piksel);
            double sumT=0,sumY=0;
            for(int k=s.baslangic;k<s.bitisHaric;k++){
                int i=indices[k];sumT+=t[i];sumY+=y[i];secim[i]=new Color32(255,255,255,255);
            }
            s.seciliPiksel=s.bitisHaric-s.baslangic;
            s.gecirgenlik=(float)(sumT/s.seciliPiksel);s.yansima=(float)(sumY/s.seciliPiksel);
            s.p20=t[indices[(int)Math.Floor(.2*s.piksel)]];
            s.p85=t[indices[(int)Math.Floor(.85*s.piksel)]];
            s.optikYayilim=s.p85-s.p20;s.gecerli=true;
            s.neden="Full-mask sorted ranks [floor(.60N),floor(.90N)); spread observed only";
            return s;
        }
    }
}
