using skpd_multi_tenant_api.Models;

namespace skpd_multi_tenant_api.Services;

public interface ISettingsService
{
    Task<AppSettings?> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(UpdateSettingsRequest request, string updatedBy, CancellationToken ct = default);
}

public sealed class SettingsService(IMySqlConnectionFactory connectionFactory) : ISettingsService
{
    public async Task<AppSettings?> GetAsync(CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, nama_aplikasi, alias_aplikasi, deskripsi, versi, copyright, tahun,
                   logo, favicon, instansi_nama, kepala_dinas, nip_kepala_dinas, pimpinan_wilayah, logo_pemda,
                   email, no_telepon, whatsapp, alamat, domain_url,
                   facebook_url, instagram_url, twitter_url, youtube_url, tiktok_url,
                   meta_keywords, meta_description, og_image,
                   mode, maintenance_message, timezone, bahasa_default, database_version,
                   max_upload_size, allowed_extensions, theme_color, date_format, time_format,
                   session_timeout, password_min_length, max_login_attempts, lockout_duration, enable_2fa,
                   smtp_host, smtp_port, smtp_user, smtp_from_name,
                   backup_auto, backup_interval, last_backup, log_activity, log_retention_days,
                   updated_at, updated_by
            FROM settings WHERE id = 1 LIMIT 1";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new AppSettings
        {
            Id = reader.GetInt32("id"),
            NamaAplikasi = reader.GetString("nama_aplikasi"),
            AliasAplikasi = reader.GetString("alias_aplikasi"),
            Deskripsi = reader.IsDBNull(reader.GetOrdinal("deskripsi")) ? null : reader.GetString("deskripsi"),
            Versi = reader.GetString("versi"),
            Copyright = reader.IsDBNull(reader.GetOrdinal("copyright")) ? null : reader.GetString("copyright"),
            Tahun = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("tahun"))),
            Logo = reader.IsDBNull(reader.GetOrdinal("logo")) ? null : reader.GetString("logo"),
            Favicon = reader.IsDBNull(reader.GetOrdinal("favicon")) ? null : reader.GetString("favicon"),
            InstansiNama = reader.IsDBNull(reader.GetOrdinal("instansi_nama")) ? null : reader.GetString("instansi_nama"),
            KepalaDinas = reader.IsDBNull(reader.GetOrdinal("kepala_dinas")) ? null : reader.GetString("kepala_dinas"),
            NipKepalaDinas = reader.IsDBNull(reader.GetOrdinal("nip_kepala_dinas")) ? null : reader.GetString("nip_kepala_dinas"),
            PimpinanWilayah = reader.IsDBNull(reader.GetOrdinal("pimpinan_wilayah")) ? null : reader.GetString("pimpinan_wilayah"),
            LogoPemda = reader.IsDBNull(reader.GetOrdinal("logo_pemda")) ? null : reader.GetString("logo_pemda"),
            Email = reader.GetString("email"),
            NoTelepon = reader.GetString("no_telepon"),
            Whatsapp = reader.IsDBNull(reader.GetOrdinal("whatsapp")) ? null : reader.GetString("whatsapp"),
            Alamat = reader.GetString("alamat"),
            DomainUrl = reader.GetString("domain_url"),
            FacebookUrl = reader.IsDBNull(reader.GetOrdinal("facebook_url")) ? null : reader.GetString("facebook_url"),
            InstagramUrl = reader.IsDBNull(reader.GetOrdinal("instagram_url")) ? null : reader.GetString("instagram_url"),
            TwitterUrl = reader.IsDBNull(reader.GetOrdinal("twitter_url")) ? null : reader.GetString("twitter_url"),
            YoutubeUrl = reader.IsDBNull(reader.GetOrdinal("youtube_url")) ? null : reader.GetString("youtube_url"),
            TiktokUrl = reader.IsDBNull(reader.GetOrdinal("tiktok_url")) ? null : reader.GetString("tiktok_url"),
            MetaKeywords = reader.IsDBNull(reader.GetOrdinal("meta_keywords")) ? null : reader.GetString("meta_keywords"),
            MetaDescription = reader.IsDBNull(reader.GetOrdinal("meta_description")) ? null : reader.GetString("meta_description"),
            OgImage = reader.IsDBNull(reader.GetOrdinal("og_image")) ? null : reader.GetString("og_image"),
            Mode = reader.GetString("mode"),
            MaintenanceMessage = reader.IsDBNull(reader.GetOrdinal("maintenance_message")) ? null : reader.GetString("maintenance_message"),
            Timezone = reader.GetString("timezone"),
            BahasaDefault = reader.GetString("bahasa_default"),
            DatabaseVersion = reader.IsDBNull(reader.GetOrdinal("database_version")) ? null : reader.GetString("database_version"),
            MaxUploadSize = reader.GetInt32("max_upload_size"),
            AllowedExtensions = reader.IsDBNull(reader.GetOrdinal("allowed_extensions")) ? null : reader.GetString("allowed_extensions"),
            ThemeColor = reader.GetString("theme_color"),
            DateFormat = reader.GetString("date_format"),
            TimeFormat = reader.GetString("time_format"),
            SessionTimeout = reader.GetInt32("session_timeout"),
            PasswordMinLength = reader.GetInt32("password_min_length"),
            MaxLoginAttempts = reader.GetInt32("max_login_attempts"),
            LockoutDuration = reader.GetInt32("lockout_duration"),
            Enable2fa = reader.GetBoolean("enable_2fa"),
            SmtpHost = reader.IsDBNull(reader.GetOrdinal("smtp_host")) ? null : reader.GetString("smtp_host"),
            SmtpPort = reader.IsDBNull(reader.GetOrdinal("smtp_port")) ? null : reader.GetInt32("smtp_port"),
            SmtpUser = reader.IsDBNull(reader.GetOrdinal("smtp_user")) ? null : reader.GetString("smtp_user"),
            SmtpFromName = reader.IsDBNull(reader.GetOrdinal("smtp_from_name")) ? null : reader.GetString("smtp_from_name"),
            BackupAuto = reader.GetBoolean("backup_auto"),
            BackupInterval = reader.GetInt32("backup_interval"),
            LastBackup = reader.IsDBNull(reader.GetOrdinal("last_backup")) ? null : reader.GetDateTime("last_backup"),
            LogActivity = reader.GetBoolean("log_activity"),
            LogRetentionDays = reader.GetInt32("log_retention_days"),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at"),
            UpdatedBy = reader.IsDBNull(reader.GetOrdinal("updated_by")) ? null : reader.GetString("updated_by"),
        };
    }

    public async Task UpdateAsync(UpdateSettingsRequest r, string updatedBy, CancellationToken ct = default)
    {
        await using var conn = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE settings SET
                nama_aplikasi      = @namaAplikasi,
                alias_aplikasi     = @aliasAplikasi,
                deskripsi          = @deskripsi,
                versi              = @versi,
                copyright          = @copyright,
                tahun              = @tahun,
                logo               = @logo,
                favicon            = @favicon,
                instansi_nama      = @instansiNama,
                kepala_dinas       = @kepalaDinas,
                nip_kepala_dinas   = @nipKepalaDinas,
                pimpinan_wilayah   = @pimpinanWilayah,
                logo_pemda         = @logoPemda,
                email              = @email,
                no_telepon         = @noTelepon,
                whatsapp           = @whatsapp,
                alamat             = @alamat,
                domain_url         = @domainUrl,
                facebook_url       = @facebookUrl,
                instagram_url      = @instagramUrl,
                twitter_url        = @twitterUrl,
                youtube_url        = @youtubeUrl,
                tiktok_url         = @tiktokUrl,
                meta_keywords      = @metaKeywords,
                meta_description   = @metaDescription,
                og_image           = @ogImage,
                mode               = @mode,
                maintenance_message= @maintenanceMessage,
                timezone           = @timezone,
                bahasa_default     = @bahasaDefault,
                max_upload_size    = @maxUploadSize,
                allowed_extensions = @allowedExtensions,
                theme_color        = @themeColor,
                date_format        = @dateFormat,
                time_format        = @timeFormat,
                session_timeout    = @sessionTimeout,
                password_min_length= @passwordMinLength,
                max_login_attempts = @maxLoginAttempts,
                lockout_duration   = @lockoutDuration,
                enable_2fa         = @enable2fa,
                smtp_host          = @smtpHost,
                smtp_port          = @smtpPort,
                smtp_user          = @smtpUser,
                smtp_from_name     = @smtpFromName,
                backup_auto        = @backupAuto,
                backup_interval    = @backupInterval,
                log_activity       = @logActivity,
                log_retention_days = @logRetentionDays,
                updated_by         = @updatedBy
            WHERE id = 1";

        object N(string? v) => (object?)v ?? DBNull.Value;
        object NI(int? v) => (object?)v ?? DBNull.Value;

        cmd.Parameters.AddWithValue("@namaAplikasi",       r.NamaAplikasi);
        cmd.Parameters.AddWithValue("@aliasAplikasi",      r.AliasAplikasi);
        cmd.Parameters.AddWithValue("@deskripsi",          N(r.Deskripsi));
        cmd.Parameters.AddWithValue("@versi",              r.Versi);
        cmd.Parameters.AddWithValue("@copyright",          N(r.Copyright));
        cmd.Parameters.AddWithValue("@tahun",              r.Tahun);
        cmd.Parameters.AddWithValue("@logo",               N(r.Logo));
        cmd.Parameters.AddWithValue("@favicon",            N(r.Favicon));
        cmd.Parameters.AddWithValue("@instansiNama",       N(r.InstansiNama));
        cmd.Parameters.AddWithValue("@kepalaDinas",        N(r.KepalaDinas));
        cmd.Parameters.AddWithValue("@nipKepalaDinas",     N(r.NipKepalaDinas));
        cmd.Parameters.AddWithValue("@pimpinanWilayah",    N(r.PimpinanWilayah));
        cmd.Parameters.AddWithValue("@logoPemda",          N(r.LogoPemda));
        cmd.Parameters.AddWithValue("@email",              r.Email);
        cmd.Parameters.AddWithValue("@noTelepon",          r.NoTelepon);
        cmd.Parameters.AddWithValue("@whatsapp",           N(r.Whatsapp));
        cmd.Parameters.AddWithValue("@alamat",             r.Alamat);
        cmd.Parameters.AddWithValue("@domainUrl",          r.DomainUrl);
        cmd.Parameters.AddWithValue("@facebookUrl",        N(r.FacebookUrl));
        cmd.Parameters.AddWithValue("@instagramUrl",       N(r.InstagramUrl));
        cmd.Parameters.AddWithValue("@twitterUrl",         N(r.TwitterUrl));
        cmd.Parameters.AddWithValue("@youtubeUrl",         N(r.YoutubeUrl));
        cmd.Parameters.AddWithValue("@tiktokUrl",          N(r.TiktokUrl));
        cmd.Parameters.AddWithValue("@metaKeywords",       N(r.MetaKeywords));
        cmd.Parameters.AddWithValue("@metaDescription",    N(r.MetaDescription));
        cmd.Parameters.AddWithValue("@ogImage",            N(r.OgImage));
        cmd.Parameters.AddWithValue("@mode",               r.Mode);
        cmd.Parameters.AddWithValue("@maintenanceMessage", N(r.MaintenanceMessage));
        cmd.Parameters.AddWithValue("@timezone",           r.Timezone);
        cmd.Parameters.AddWithValue("@bahasaDefault",      r.BahasaDefault);
        cmd.Parameters.AddWithValue("@maxUploadSize",      r.MaxUploadSize);
        cmd.Parameters.AddWithValue("@allowedExtensions",  N(r.AllowedExtensions));
        cmd.Parameters.AddWithValue("@themeColor",         r.ThemeColor);
        cmd.Parameters.AddWithValue("@dateFormat",         r.DateFormat);
        cmd.Parameters.AddWithValue("@timeFormat",         r.TimeFormat);
        cmd.Parameters.AddWithValue("@sessionTimeout",     r.SessionTimeout);
        cmd.Parameters.AddWithValue("@passwordMinLength",  r.PasswordMinLength);
        cmd.Parameters.AddWithValue("@maxLoginAttempts",   r.MaxLoginAttempts);
        cmd.Parameters.AddWithValue("@lockoutDuration",    r.LockoutDuration);
        cmd.Parameters.AddWithValue("@enable2fa",          r.Enable2fa ? 1 : 0);
        cmd.Parameters.AddWithValue("@smtpHost",           N(r.SmtpHost));
        cmd.Parameters.AddWithValue("@smtpPort",           NI(r.SmtpPort));
        cmd.Parameters.AddWithValue("@smtpUser",           N(r.SmtpUser));
        cmd.Parameters.AddWithValue("@smtpFromName",       N(r.SmtpFromName));
        cmd.Parameters.AddWithValue("@backupAuto",         r.BackupAuto ? 1 : 0);
        cmd.Parameters.AddWithValue("@backupInterval",     r.BackupInterval);
        cmd.Parameters.AddWithValue("@logActivity",        r.LogActivity ? 1 : 0);
        cmd.Parameters.AddWithValue("@logRetentionDays",   r.LogRetentionDays);
        cmd.Parameters.AddWithValue("@updatedBy",          updatedBy);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
