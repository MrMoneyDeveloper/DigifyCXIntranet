namespace DigifyCXIntranet.Options;

public sealed class SqlServerSecurityOptions
{
    public const string SectionName = "SqlServerSecurity";

    public bool AllowTrustServerCertificateForInternalTest { get; set; }
}
