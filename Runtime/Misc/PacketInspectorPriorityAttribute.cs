using System;

namespace NetBuff.Misc
{
    public enum PacketInspectorPriority
    {
        VeryLow = -2,
        Low = -1,
        Normal = 0,
        High = 1,
        VeryHigh = 2
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class PacketInspectorPriorityAttribute : Attribute
    {
        public PacketInspectorPriority Priority { get; }
        
        public PacketInspectorPriorityAttribute(PacketInspectorPriority priority)
        {
            Priority = priority;
        }
    }
}