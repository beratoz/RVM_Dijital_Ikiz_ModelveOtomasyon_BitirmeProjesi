using System;
using UnityEngine;

namespace RVM.Experimental
{
    // One condition per Play session. Original assets are never edited.
    public sealed class RvmOptikAyar : MonoBehaviour
    {
        public string kosul="B_yari_isik";
        public float isikCarpani=.5f, arkaPlan=1;
        Light[] lights;
        float[] original;
        [Serializable] sealed class Ayar { public string kosul; public float isikCarpani,arkaPlan; public string kapsam="Sahnedeki tum Light siddetleri; ortam aydinlatmasi sabit"; }
        public void Hazirla()
        {
            // Frozen delivery condition: tested B, half light, white background.
            isikCarpani=.5f; arkaPlan=1; kosul="B_yari_isik";
            lights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            original=new float[lights.Length];
            for(int i=0;i<lights.Length;i++){original[i]=lights[i].intensity;lights[i].intensity=original[i]*isikCarpani;}
        }
        public void Kaydet()
        {
            RvmTezKaydi.Ekle("optik_deney_kosulu",JsonUtility.ToJson(new Ayar{kosul=kosul,isikCarpani=isikCarpani,arkaPlan=arkaPlan}));
        }
        void OnGUI()
        {
            GUI.Label(new Rect(10,Screen.height-32,800,28),"FINAL OPTIK: "+kosul+" | Isik x"+isikCarpani+" | Arka plan "+arkaPlan);
        }
        void OnDestroy()
        {
            if(lights==null)return;
            for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].intensity=original[i];
        }
    }
}
