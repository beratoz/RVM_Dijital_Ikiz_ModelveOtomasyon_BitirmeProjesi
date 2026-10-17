using System;
namespace RVM.Experimental
{
    // Only measured data; material decisions belong to CODESYS v106.
    [Serializable] public struct RvmOlcumVerisi
    {
        public bool imageValid, scaleValid, probeValid;
        public float length,width,grams,transmission,reflection,metal,magnetic,ageSeconds;
    }
}
