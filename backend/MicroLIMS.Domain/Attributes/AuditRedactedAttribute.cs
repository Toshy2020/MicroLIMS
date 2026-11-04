namespace MicroLIMS.Domain.Attributes;

// Marks a property whose value is a credential (password hash, token hash,
// recovery-code hash, API-key hash). The automatic audit capture still
// records THAT the property changed, but writes "[REDACTED]" in place of
// the value, so the immutable AuditLogs table never holds secret material
// that Audit.View holders can read back.
//
// Do NOT use it for content or file checksums: those are evidence and
// must stay visible in the trail.
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AuditRedactedAttribute : Attribute
{
    public const string RedactedValue = "[REDACTED]";
}
