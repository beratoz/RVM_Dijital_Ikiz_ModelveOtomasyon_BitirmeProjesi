using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RVM
{
    // Pixel-only measurement. No specimen profile, name, mesh bounds or material code enters Analyze.
    public static class RvmImageAnalysis
    {
        public struct Measurement { public bool valid, present; public float length, width, fill; public string reason; }
        public static Measurement Analyze(Color32[] pixels, int width, int height, float horizontalMetres)
        {
            var m=new Measurement {reason="Invalid frame"};
            if(pixels==null||pixels.Length!=width*height||width<8||height<8||horizontalMetres<=0)return m;
            var mask=new bool[pixels.Length];var seen=new bool[pixels.Length];var queue=new int[pixels.Length];
            for(int i=0;i<pixels.Length;i++) mask[i]=Math.Max(pixels[i].r,Math.Max(pixels[i].g,pixels[i].b))>24;
            int largest=0,second=0,minX=0,maxX=0,minY=0,maxY=0;
            for(int i=0;i<mask.Length;i++)
            {
                if(!mask[i]||seen[i])continue;
                int head=0,tail=1,count=0,l=width,r=0,b=height,t=0;queue[0]=i;seen[i]=true;
                while(head<tail)
                {
                    int p=queue[head++],x=p%width,y=p/width;count++;
                    l=Math.Min(l,x);r=Math.Max(r,x);b=Math.Min(b,y);t=Math.Max(t,y);
                    for(int d=0;d<4;d++)
                    {
                        int nx=x+(d==0?-1:d==1?1:0),ny=y+(d==2?-1:d==3?1:0);
                        if(nx<0||nx>=width||ny<0||ny>=height)continue;
                        int n=ny*width+nx;if(mask[n]&&!seen[n]){seen[n]=true;queue[tail++]=n;}
                    }
                }
                if(count>largest){second=largest;largest=count;minX=l;maxX=r;minY=b;maxY=t;}
                else second=Math.Max(second,count);
            }
            m.valid=true;m.present=largest>=20;
            if(!m.present){m.reason="No object in image";return m;}
            float metresPerPixel=horizontalMetres/width;
            m.length=(maxX-minX+1)*metresPerPixel;m.width=(maxY-minY+1)*metresPerPixel;
            m.fill=(float)largest/((maxX-minX+1)*(maxY-minY+1));
            m.valid=minX>1&&maxX<width-2&&minY>1&&maxY<height-2&&second<largest*.10f;
            m.reason=m.valid?"Pixel silhouette measured":"Clipped frame or multiple objects";
            return m;
        }
    }
}
