using UnityEngine;
namespace RVM.Experimental
{
    public sealed class RvmInductiveProbe : MonoBehaviour
    {
        public float rangeMetres=.04f;
        public bool valid;
        public float signal, magneticSignal;
        public string reason="No target";
        // Idealized proximity response, not an electromagnetic field solver.
        public void Sample(Rigidbody target)
        {
            valid=false;signal=magneticSignal=0;
            if(!target){reason="No target";return;}
            var physical=target.GetComponent<RvmConductiveBody>();
            if(!physical||!physical.propertiesKnown){reason="Electrical properties unspecified";return;}
            float distance=float.PositiveInfinity;
            foreach(var c in target.GetComponentsInChildren<Collider>())
                if(c.enabled&&!c.isTrigger)distance=Mathf.Min(distance,Vector3.Distance(transform.position,c.ClosestPoint(transform.position)));
            if(distance>rangeMetres){reason="Outside probe range";return;}
            float response=1/(1+Mathf.Pow(distance/.02f,2));
            signal=physical.conductivityResponse*response;
            magneticSignal=physical.magneticResponse*response;
            valid=true;reason="Simulated inductive / magnetic response";
        }
    }
}
