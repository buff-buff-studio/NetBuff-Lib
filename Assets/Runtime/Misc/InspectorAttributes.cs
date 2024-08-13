using System;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Used to specify the inspector mode of a field or property.
    ///     Improves the readability of the inspector.
    /// </summary>
    public enum InspectorMode
    {
        /// <summary>
        /// Default mode. Represents the standard inspector mode.
        /// </summary>
        Standard,
        
        /// <summary>
        /// Represents the field or property as a reference to an object.
        /// </summary>
        Object,
        
        /// <summary>
        /// Represents the field or property as a reference to a prefab.
        /// </summary>
        Prefab,
        
        /// <summary>
        /// Represents the field or property as a reference to a behaviour.
        /// </summary>
        Behaviour,
        
        /// <summary>
        /// Represents the field or property as a reference to a network end.
        /// </summary>
        Owner,
        
        /// <summary>
        /// Represents the field or property as a reference to a loaded scene.
        /// </summary>
        Scene,
        
        /// <summary>
        /// Represents an array as hex values.
        /// </summary>
        Data
    }
    
    /// <summary>
    ///     Used to specify the inspector mode of a field or property.
    ///     Improves the readability of the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InspectorModeAttribute : Attribute
    {
        /// <summary>
        ///     Returns the inspector mode of the field or property.
        /// </summary>
        public InspectorMode InspectorMode { get; }
        
        public InspectorModeAttribute(InspectorMode inspectorMode)
        {
            InspectorMode = inspectorMode;
        }
    }
}