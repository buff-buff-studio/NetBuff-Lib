using System;

namespace NetBuff.Misc
{
    /// <summary>
    /// Used to specify the inspector mode of a field or property.
    /// Improves the readability of the inspector.
    /// </summary>
    public enum InspectorMode
    {
        Standard,
        
        Object,
        Prefab,
        
        Behaviour,
        Owner,
        Scene,
        
        Data
    }
    
    /// <summary>
    /// Used to specify the inspector mode of a field or property.
    /// Improves the readability of the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InspectorModeAttribute : Attribute
    {
        public InspectorMode InspectorMode { get; }
        
        public InspectorModeAttribute(InspectorMode inspectorMode)
        {
            InspectorMode = inspectorMode;
        }
    }
    
}