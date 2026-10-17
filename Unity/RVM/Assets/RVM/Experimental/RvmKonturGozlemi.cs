using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVM.Experimental
{
    // Observation only. No acceptance threshold, neck requirement or material input.
    public static class RvmKonturGozlemi
    {
        public const int Bolum=64;
        public const string Surum="kontur-gozlem-pca64-v1";
        [Serializable] public sealed class Sonuc
        {
            public string surum=Surum,neden;
            public bool gozlemGecerli,kenaraDegiyor,kararaBagli=false;
            public int toplamPiksel,anaPiksel,bilesenSayisi,profilBolum=Bolum,yumusatmaPenceresi=5;
            public double merkezX,merkezY,eksenRadyan,uzunlukPiksel,genislikPiksel;
            public double ayrikAlanOrani,disbukeyDoluluk,aynaUyumu,profilPuruzu,enBuyukProfilAdimi;
            public bool[] profilVar=new bool[Bolum];
            public double[] profilAlt=new double[Bolum],profilUst=new double[Bolum],profilGenislik=new double[Bolum],profilYumusak=new double[Bolum];
        }
        struct Nokta {public double x,y;public Nokta(double x,double y){this.x=x;this.y=y;}}
        static double Capraz(Nokta a,Nokta b,Nokta c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static double ZarfAlani(List<Nokta> p)
        {
            p.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));var h=new List<Nokta>();
            foreach(var a in p){while(h.Count>=2&&Capraz(h[h.Count-2],h[h.Count-1],a)<=0)h.RemoveAt(h.Count-1);h.Add(a);}
            int lower=h.Count;
            for(int i=p.Count-2;i>=0;i--){while(h.Count>lower&&Capraz(h[h.Count-2],h[h.Count-1],p[i])<=0)h.RemoveAt(h.Count-1);h.Add(p[i]);}
            double area=0;for(int i=1;i<h.Count;i++)area+=h[i-1].x*h[i].y-h[i].x*h[i-1].y;return Math.Abs(area)*.5;
        }
        public static Sonuc Hesapla(Color32[] mask,int en,int boy,out Color32[] anaMaske)
        {
            var s=new Sonuc();anaMaske=new Color32[mask.Length];var seen=new bool[mask.Length];var queue=new int[mask.Length];var largest=new List<int>();
            for(int i=0;i<mask.Length;i++)if(mask[i].r>24)s.toplamPiksel++;
            for(int i=0;i<mask.Length;i++) {
                if(seen[i]||mask[i].r<=24)continue;
                s.bilesenSayisi++;int head=0,tail=1;queue[0]=i;seen[i]=true;
                while(head<tail) {
                    int j=queue[head++],x=j%en,y=j/en;
                    for(int d=0;d<4;d++) {
                        int nx=x+(d==0?-1:d==1?1:0),ny=y+(d==2?-1:d==3?1:0);
                        if(nx<0||nx>=en||ny<0||ny>=boy)continue;
                        int n=ny*en+nx;if(!seen[n]&&mask[n].r>24){seen[n]=true;queue[tail++]=n;}
                    }
                }
                if(tail>largest.Count){largest.Clear();for(int j=0;j<tail;j++)largest.Add(queue[j]);}
            }
            s.anaPiksel=largest.Count;
            if(largest.Count<32){s.neden="Insufficient connected silhouette; no verdict";return s;}
            s.ayrikAlanOrani=1-(double)largest.Count/s.toplamPiksel;
            double xx=0,yy=0,xy=0;var bottom=new int[en];var top=new int[en];
            for(int x=0;x<en;x++){bottom[x]=boy;top[x]=-1;}
            foreach(int i in largest) {
                int x=i%en,y=i/en;anaMaske[i]=new Color32(255,255,255,255);
                s.merkezX+=x;s.merkezY+=y;xx+=x*x;yy+=y*y;xy+=x*y;
                bottom[x]=Math.Min(bottom[x],y);top[x]=Math.Max(top[x],y);
                if(x<=1||x>=en-2||y<=1||y>=boy-2)s.kenaraDegiyor=true;
            }
            s.merkezX/=largest.Count;s.merkezY/=largest.Count;
            xx-=largest.Count*s.merkezX*s.merkezX;yy-=largest.Count*s.merkezY*s.merkezY;xy-=largest.Count*s.merkezX*s.merkezY;
            s.eksenRadyan=.5*Math.Atan2(2*xy,xx-yy);double c=Math.Cos(s.eksenRadyan),sn=Math.Sin(s.eksenRadyan);
            double min=double.PositiveInfinity,max=double.NegativeInfinity;
            foreach(int i in largest){double u=c*(i%en-s.merkezX)+sn*(i/en-s.merkezY);min=Math.Min(min,u);max=Math.Max(max,u);}
            s.uzunlukPiksel=max-min+1;
            var lo=new double[Bolum];var hi=new double[Bolum];
            for(int j=0;j<Bolum;j++){lo[j]=double.PositiveInfinity;hi[j]=double.NegativeInfinity;}
            foreach(int i in largest) {
                double x=i%en-s.merkezX,y=i/en-s.merkezY,u=c*x+sn*y,v=-sn*x+c*y;
                int j=Math.Min(Bolum-1,(int)((u-min)/s.uzunlukPiksel*Bolum));lo[j]=Math.Min(lo[j],v);hi[j]=Math.Max(hi[j],v);s.profilVar[j]=true;
            }
            double sum=0,error=0;
            for(int j=0;j<Bolum;j++)if(s.profilVar[j]) {
                double width=hi[j]-lo[j]+1;s.genislikPiksel=Math.Max(s.genislikPiksel,width);sum+=width;error+=Math.Abs(hi[j]+lo[j]);
            }
            s.aynaUyumu=Math.Max(0,Math.Min(1,1-error/sum));
            for(int j=0;j<Bolum;j++)if(s.profilVar[j]) {
                s.profilAlt[j]=lo[j]/s.genislikPiksel;s.profilUst[j]=hi[j]/s.genislikPiksel;
                s.profilGenislik[j]=(hi[j]-lo[j]+1)/s.genislikPiksel;
            }
            for(int j=0;j<Bolum;j++)if(s.profilVar[j]) {
                var a=new List<double>();for(int k=Math.Max(0,j-2);k<=Math.Min(Bolum-1,j+2);k++)if(s.profilVar[k])a.Add(s.profilGenislik[k]);
                a.Sort();s.profilYumusak[j]=a.Count%2==1?a[a.Count/2]:(a[a.Count/2-1]+a[a.Count/2])*.5;
            }
            int count=0;
            for(int j=1;j<Bolum;j++)if(s.profilVar[j]&&s.profilVar[j-1])s.enBuyukProfilAdimi=Math.Max(s.enBuyukProfilAdimi,Math.Abs(s.profilYumusak[j]-s.profilYumusak[j-1]));
            for(int j=1;j<Bolum-1;j++)if(s.profilVar[j-1]&&s.profilVar[j]&&s.profilVar[j+1]) {
                s.profilPuruzu+=Math.Abs(s.profilYumusak[j+1]-2*s.profilYumusak[j]+s.profilYumusak[j-1]);count++;
            }
            if(count>0)s.profilPuruzu/=count;
            var points=new List<Nokta>();for(int x=0;x<en;x++)if(top[x]>=0) {
                points.Add(new Nokta(x,bottom[x]));points.Add(new Nokta(x+1,bottom[x]));
                points.Add(new Nokta(x,top[x]+1));points.Add(new Nokta(x+1,top[x]+1));
            }
            double hull=ZarfAlani(points);s.disbukeyDoluluk=hull>0?largest.Count/hull:0;
            s.gozlemGecerli=!s.kenaraDegiyor;s.neden=s.kenaraDegiyor?"Clipped silhouette; observation only":"Contour metrics only; no acceptance threshold";
            return s;
        }
    }
}
