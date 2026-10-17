using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVM.Experimental
{
    // Only pixels and a silhouette enter this estimator. No catalogue or material labels.
    public static class RvmBolgeselOptik
    {
        public const string Surum = "govde-pca-q60q90-v1";
        [Serializable] public sealed class Sonuc
        {
            public string surum=Surum, neden;
            public bool gecerli;
            public int piksel, roiPiksel, seciliPiksel;
            public double merkezX, merkezY, eksenRadyan;
            public float hamT, hamY, roiT, roiY, altSinir, ustSinir, gecirgenlik, yansima;
            public float ucPayi=.1f, genislikOrani=.75f, kenarPayi=.15f, altYuzdelik=.6f, ustYuzdelik=.9f;
            public float yuzdelikCozunurlugu=.0001f;
        }
        static float L(Color32 c)=>(.2126f*c.r+.7152f*c.g+.0722f*c.b)/255f;
        static float Yuzdelik(List<float> a, double q)
        {
            double p=(a.Count-1)*q;int i=(int)p;
            return (float)(a[i]+(a[Math.Min(i+1,a.Count-1)]-a[i])*(p-i));
        }
        public static Sonuc Hesapla(Color32[] eb, Color32[] ew, Color32[] b, Color32[] w,
            Color32[] mask, int en, out Color32[] roiMask, out Color32[] secimMask)
        {
            var s=new Sonuc();roiMask=new Color32[mask.Length];secimMask=new Color32[mask.Length];
            double xx=0,yy=0,xy=0,ht=0,hy=0;
            var t=new float[mask.Length];var y=new float[mask.Length];
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24) {
                double x=i%en,z=i/en;s.piksel++;s.merkezX+=x;s.merkezY+=z;xx+=x*x;yy+=z*z;xy+=x*z;
                float baseline=L(ew[i])-L(eb[i]);
                if(baseline<=.2f){s.neden="Invalid reference inside silhouette";return s;}
                t[i]=Mathf.Clamp01((L(w[i])-L(b[i]))/baseline);y[i]=Mathf.Clamp01((L(b[i])-L(eb[i]))/baseline);
                ht+=t[i];hy+=y[i];
            }
            if(s.piksel<32){s.neden="Insufficient silhouette pixels";return s;}
            s.merkezX/=s.piksel;s.merkezY/=s.piksel;s.hamT=(float)(ht/s.piksel);s.hamY=(float)(hy/s.piksel);
            xx-=s.piksel*s.merkezX*s.merkezX;yy-=s.piksel*s.merkezY*s.merkezY;xy-=s.piksel*s.merkezX*s.merkezY;
            s.eksenRadyan=.5*Math.Atan2(2*xy,xx-yy);
            double c=Math.Cos(s.eksenRadyan),sn=Math.Sin(s.eksenRadyan),min=double.PositiveInfinity,max=double.NegativeInfinity;
            var u=new double[mask.Length];var v=new double[mask.Length];
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24) {
                double x=i%en-s.merkezX,z=i/en-s.merkezY;u[i]=c*x+sn*z;v[i]=-sn*x+c*z;
                min=Math.Min(min,u[i]);max=Math.Max(max,u[i]);
            }
            int bins=(int)Math.Floor(max-min)+1;var lo=new double[bins];var hi=new double[bins];
            for(int j=0;j<bins;j++){lo[j]=double.PositiveInfinity;hi[j]=double.NegativeInfinity;}
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24) {
                int j=(int)Math.Floor(u[i]-min);lo[j]=Math.Min(lo[j],v[i]);hi[j]=Math.Max(hi[j],v[i]);
            }
            double widest=0;for(int j=0;j<bins;j++)widest=Math.Max(widest,hi[j]-lo[j]+1);
            var values=new List<float>();double rt=0,ry=0;
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24) {
                int j=(int)Math.Floor(u[i]-min);double width=hi[j]-lo[j]+1;
                if(u[i]<min+.1*(max-min)||u[i]>max-.1*(max-min)||width<.75*widest||
                    v[i]<lo[j]+.15*width||v[i]>hi[j]-.15*width)continue;
                roiMask[i]=new Color32(255,255,255,255);
                // Stable ties across float implementations; means retain original pixel values.
                values.Add((float)Math.Floor((double)t[i]*10000+.5));rt+=t[i];ry+=y[i];
            }
            s.roiPiksel=values.Count;
            if(values.Count<32||values.Count<s.piksel*.01){s.neden="Insufficient body ROI";return s;}
            s.roiT=(float)(rt/values.Count);s.roiY=(float)(ry/values.Count);values.Sort();
            float lower=Yuzdelik(values,.6),upper=Yuzdelik(values,.9);
            s.altSinir=lower/10000;s.ustSinir=upper/10000;double st=0,sy=0;
            for(int i=0;i<mask.Length;i++)if(roiMask[i].r>0) {
                double key=Math.Floor((double)t[i]*10000+.5);if(key<lower||key>upper)continue;
                secimMask[i]=new Color32(255,255,255,255);s.seciliPiksel++;st+=t[i];sy+=y[i];
            }
            s.gecerli=s.seciliPiksel>0;
            if(s.gecerli){s.gecirgenlik=(float)(st/s.seciliPiksel);s.yansima=(float)(sy/s.seciliPiksel);}
            s.neden=s.gecerli?"Geometric body ROI, T quantile 60-90; Y on same pixels":"Empty quantile selection";
            return s;
        }
    }
}
