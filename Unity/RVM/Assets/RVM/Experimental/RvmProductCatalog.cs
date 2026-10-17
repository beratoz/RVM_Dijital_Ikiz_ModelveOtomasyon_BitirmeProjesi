using System;
using UnityEngine;

namespace RVM.Experimental
{
    // Operator asset selection only. No expected class, bin or acceptance data.
    public sealed class RvmProductCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string id, title;
            public GameObject prefab;
        }
        public Entry[] entries = Array.Empty<Entry>();
        public int ProfileIndex(int profile, int selected)
        {
            string id = profile==1 ? "P01" : profile==2 ? "A01" : profile==3 ? "G01" : null;
            if(id==null)return selected;
            return Array.FindIndex(entries, entry=>entry.id==id);
        }
    }
}
