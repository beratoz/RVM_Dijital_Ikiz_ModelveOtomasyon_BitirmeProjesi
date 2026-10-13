using UnityEngine;
namespace RVM.Experimental
{
    // Physical world parameters, never a PET/glass/aluminium label.
    public sealed class RvmConductiveBody : MonoBehaviour
    {
        public bool propertiesKnown;
        [Range(0,1)] public float conductivityResponse;
        [Range(0,1)] public float magneticResponse;
    }
}
