using System;
namespace RVM.Experimental
{
    // Bench hypothesis only: thresholds need validation across a larger packaging dataset.
    // No GameObject, prefab, name, shader properties or physical mass references allowed here.
    public static class RvmMaterialDecision
    {
        [Serializable] public struct Reading
        {
            public bool imageValid, scaleValid, probeValid;
            public float length,width,grams,transmission,reflection,metal,magnetic,ageSeconds;
        }
        [Serializable] public struct Decision
        {
            public int materialCode;
            public string reason;
        }
        static Decision Reject(string text)=>new Decision{materialCode=0,reason=text};
        public static Decision Evaluate(Reading m)
        {
            foreach(float x in new[]{m.length,m.width,m.grams,m.transmission,m.reflection,m.metal,m.magnetic,m.ageSeconds})
                if(float.IsNaN(x)||float.IsInfinity(x))return Reject("Non-finite measurement");
            if(!m.imageValid||!m.scaleValid||!m.probeValid)return Reject("Incomplete or invalid measurements");
            if(m.ageSeconds<0||m.ageSeconds>2)return Reject("Stale measurement");
            if(m.length<.10f||m.length>.30f||m.width<.02f||m.width>.09f||m.grams<5||m.grams>1000)return Reject("Measurement range");
            if(m.transmission<0||m.transmission>1||m.reflection<0||m.reflection>1||m.metal<0||m.metal>1||m.magnetic<0||m.magnetic>1)return Reject("Signal range");
            if(m.magnetic>.1f)return Reject("Magnetic metal: not accepted as aluminium");
            if(m.metal>=.5f)
            {
                if(m.transmission<=.08f&&m.grams>=8&&m.grams<=45&&m.length<=.20f)
                    return new Decision{materialCode=2,reason="Aluminium candidate: conductive, opaque, light"};
                return Reject("Metal / optical / mass disagreement");
            }
            if(m.metal>.1f)return Reject("Uncertain metal response");
            if(m.grams>=10&&m.grams<=65&&m.transmission>=.35f)
                return new Decision{materialCode=1,reason="PET candidate: nonconductive, light, transmitting"};
            if(m.grams>=100&&m.grams<=800&&m.transmission>=.15f&&m.transmission<=.85f&&m.reflection>=.08f)
                return new Decision{materialCode=3,reason="Glass candidate: nonconductive, heavy, optical response"};
            return Reject("No unambiguous calibrated rule");
        }
    }
}
