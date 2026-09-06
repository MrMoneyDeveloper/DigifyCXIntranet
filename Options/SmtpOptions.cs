using System.ComponentModel.DataAnnotations;

namespace DigifyCXIntranet.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    [Range(1, 65535)]
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    [Required, EmailAddress]
    public string FromAddress { get; set; } = "no-reply@digifycx.local";

    [Required, MaxLength(120)]
    public string FromName { get; set; } = "DigifyCX Intranet";
}
