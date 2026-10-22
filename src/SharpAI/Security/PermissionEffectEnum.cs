namespace SharpAI.Security
{
    /// <summary>
    /// Whether a permission grants or explicitly denies access. Explicit <see cref="Deny"/> always wins over
    /// any matching <see cref="Permit"/> within the same tenant and resource scope.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum PermissionEffectEnum
    {
        /// <summary>Grant access when this permission matches.</summary>
        Permit,

        /// <summary>Explicitly deny access when this permission matches; overrides any permit.</summary>
        Deny
    }
}
