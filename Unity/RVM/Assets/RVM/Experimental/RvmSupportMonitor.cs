using System.Collections.Generic;
using UnityEngine;
namespace RVM.Experimental
{
    public sealed class RvmSupportMonitor : MonoBehaviour
    {
        public Collider allowedPlatform;
        readonly Dictionary<Collider,float> otherContacts=new Dictionary<Collider,float>();
        public bool ForeignContact
        {
            get {foreach(var pair in otherContacts)if(pair.Key&&Time.fixedTime-pair.Value<Time.fixedDeltaTime*2.5f)return true;return false;}
        }
        void OnCollisionEnter(Collision c){Record(c);}
        void OnCollisionStay(Collision c){Record(c);}
        void OnCollisionExit(Collision c){otherContacts.Remove(c.collider);}
        void Record(Collision c){if(c.collider!=allowedPlatform)otherContacts[c.collider]=Time.fixedTime;}
    }
}
