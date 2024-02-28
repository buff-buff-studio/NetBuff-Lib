namespace BuffBuffNetcode.Misc
{
    /// <summary>
    /// Marks that a method, field, or property should only be used on the server.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class ServerOnlyAttribute : System.Attribute {}
}