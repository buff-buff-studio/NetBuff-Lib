using System;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Used to mark a method, field or property as client only.
    ///     This means that the method, field or property will only be used on the client side.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class ClientOnlyAttribute : Attribute
    {
    }
}