using System;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Used to mark a method, field or property as requiring authority.
    ///     If a method, field or property is marked with this attribute, it will only be used if the local environment has
    ///     authority over the object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public class RequiresAuthorityAttribute : Attribute
    {
    }
}