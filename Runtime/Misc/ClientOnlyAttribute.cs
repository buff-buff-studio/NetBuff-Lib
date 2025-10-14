using System;

namespace NetBuff.Misc
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class ClientOnlyAttribute : Attribute
    {
    }
}