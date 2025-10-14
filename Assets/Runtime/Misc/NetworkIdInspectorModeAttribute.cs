using System;

namespace NetBuff.Misc
{
    public enum NetworkIdInspectorMode
    {
        Standard,
        
        Object,
        
        Prefab,
        
        Behaviour,
        
        Owner,
        
        Scene,
        
        Data
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NetworkIdInspectorModeAttribute : Attribute
    {
        public NetworkIdInspectorMode NetworkIdInspectorMode { get; }
        
        public NetworkIdInspectorModeAttribute(NetworkIdInspectorMode networkIdInspectorMode)
        {
            NetworkIdInspectorMode = networkIdInspectorMode;
        }
    }
}