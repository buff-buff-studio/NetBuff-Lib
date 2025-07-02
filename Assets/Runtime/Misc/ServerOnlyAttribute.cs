using System;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Used to mark a method, field or property as server only.
    ///     This means that the method, field or property will only be used on the server side.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event)]
    public class ServerOnlyAttribute : Attribute
    {
    }
}