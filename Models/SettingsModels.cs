namespace skpd_multi_tenant_api.Models;

public sealed class AppSettings
{
    public int Id { get; set; }

    // Identitas
    public string NamaAplikasi { get; set; } = string.Empty;
    public string AliasAplikasi { get; set; } = string.Empty;
    public string? Deskripsi { get; set; }
    public string Versi { get; set; } = string.Empty;
    public string? Copyright { get; set; }
    public int Tahun { get; set; }
    public string? Logo { get; set; }
    public string? Favicon { get; set; }
    public string? InstansiNama { get; set; }
    public string? KepalaDinas { get; set; }
    public string? NipKepalaDinas { get; set; }
    public string? PimpinanWilayah { get; set; }
    public string? LogoPemda { get; set; }

    // Kontak
    public string Email { get; set; } = string.Empty;
    public string NoTelepon { get; set; } = string.Empty;
    public string? Whatsapp { get; set; }
    public string Alamat { get; set; } = string.Empty;
    public string DomainUrl { get; set; } = string.Empty;

    // Media Sosial
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? TiktokUrl { get; set; }

    // SEO & Meta
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImage { get; set; }

    // Sistem
    public string Mode { get; set; } = "online";
    public string? MaintenanceMessage { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string BahasaDefault { get; set; } = string.Empty;
    public string? DatabaseVersion { get; set; }
    public int MaxUploadSize { get; set; }
    public string? AllowedExtensions { get; set; }
    public string ThemeColor { get; set; } = "#3b82f6";
    public string DateFormat { get; set; } = "d-m-Y";
    public string TimeFormat { get; set; } = "24h";

    // Keamanan
    public int SessionTimeout { get; set; }
    public int PasswordMinLength { get; set; }
    public int MaxLoginAttempts { get; set; }
    public int LockoutDuration { get; set; }
    public bool Enable2fa { get; set; }

    // SMTP
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpFromName { get; set; }

    // Backup & Log
    public bool BackupAuto { get; set; }
    public int BackupInterval { get; set; }
    public DateTime? LastBackup { get; set; }
    public bool LogActivity { get; set; }
    public int LogRetentionDays { get; set; }

    // Meta
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class UpdateSettingsRequest
{
    // Identitas
    public string NamaAplikasi { get; set; } = string.Empty;
    public string AliasAplikasi { get; set; } = string.Empty;
    public string? Deskripsi { get; set; }
    public string Versi { get; set; } = string.Empty;
    public string? Copyright { get; set; }
    public int Tahun { get; set; }
    public string? Logo { get; set; }
    public string? Favicon { get; set; }
    public string? InstansiNama { get; set; }
    public string? KepalaDinas { get; set; }
    public string? NipKepalaDinas { get; set; }
    public string? PimpinanWilayah { get; set; }
    public string? LogoPemda { get; set; }

    // Kontak
    public string Email { get; set; } = string.Empty;
    public string NoTelepon { get; set; } = string.Empty;
    public string? Whatsapp { get; set; }
    public string Alamat { get; set; } = string.Empty;
    public string DomainUrl { get; set; } = string.Empty;

    // Media Sosial
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TwitterUrl { get; set; }
    public string? YoutubeUrl { get; set; }
    public string? TiktokUrl { get; set; }

    // SEO & Meta
    public string? MetaKeywords { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImage { get; set; }

    // Sistem
    public string Mode { get; set; } = "online";
    public string? MaintenanceMessage { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string BahasaDefault { get; set; } = string.Empty;
    public int MaxUploadSize { get; set; }
    public string? AllowedExtensions { get; set; }
    public string ThemeColor { get; set; } = "#3b82f6";
    public string DateFormat { get; set; } = "d-m-Y";
    public string TimeFormat { get; set; } = "24h";

    // Keamanan
    public int SessionTimeout { get; set; }
    public int PasswordMinLength { get; set; }
    public int MaxLoginAttempts { get; set; }
    public int LockoutDuration { get; set; }
    public bool Enable2fa { get; set; }

    // SMTP
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUser { get; set; }
    public string? SmtpFromName { get; set; }

    // Backup & Log
    public bool BackupAuto { get; set; }
    public int BackupInterval { get; set; }
    public bool LogActivity { get; set; }
    public int LogRetentionDays { get; set; }
}
