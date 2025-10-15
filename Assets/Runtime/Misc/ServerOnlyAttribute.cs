using System;

namespace NetBuff.Misc
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event)]
    public class ServerOnlyAttribute : Attribute
    {
    }
}