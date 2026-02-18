using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using skpd_multi_tenant_api.Endpoints;
using skpd_multi_tenant_api.Middleware;
using skpd_multi_tenant_api.Options;
using skpd_multi_tenant_api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? throw new InvalidOperationException("Konfigurasi Jwt belum benar.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            RoleClaimType = "role",
            NameClaimType = JwtRegisteredClaimNames.UniqueName
        };

        // Read JWT from HttpOnly cookie if no Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    context.Token = context.Request.Cookies["accessToken"];
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("PublicPolicy", context =>
    {
        // Authenticated users get a generous limit
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst("sub")?.Value ?? "authenticated";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"user_{userId}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        // Anonymous users get a stricter limit per IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"anon_{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanCreateBerita", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "create_berita" || c.Value == "manage_all")));

    options.AddPolicy("CanEditBerita", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "edit_berita" || c.Value == "manage_all")));

    options.AddPolicy("CanDeleteBerita", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "delete_berita" || c.Value == "manage_all")));

    options.AddPolicy("CanPublishBerita", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "publish_berita" || c.Value == "manage_all")));

    options.AddPolicy("CanCreateCategory", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "create_category" || c.Value == "manage_all")));

    options.AddPolicy("CanEditCategory", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "edit_category" || c.Value == "manage_all")));

    options.AddPolicy("CanDeleteCategory", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "delete_category" || c.Value == "manage_all")));

    options.AddPolicy("ManageAll", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "manage_all")));

    options.AddPolicy("CanViewBerita", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "view_berita" || c.Value == "manage_all")));

    options.AddPolicy("CanViewCategory", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "view_category" || c.Value == "manage_all")));

    options.AddPolicy("CanViewSkpd", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "view_skpd" || c.Value == "manage_all")));

    options.AddPolicy("CanCreateSkpd", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "create_skpd" || c.Value == "manage_all")));

    options.AddPolicy("CanEditSkpd", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "edit_skpd" || c.Value == "manage_all")));

    options.AddPolicy("CanDeleteSkpd", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "delete_skpd" || c.Value == "manage_all")));

    options.AddPolicy("CanViewUsers", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "view_users" || c.Value == "manage_all")));

    options.AddPolicy("CanCreateUser", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "create_user" || c.Value == "manage_all")));

    options.AddPolicy("CanEditUser", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "edit_user" || c.Value == "manage_all")));

    options.AddPolicy("CanDeleteUser", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "delete_user" || c.Value == "manage_all")));

    options.AddPolicy("CanViewRoles", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "assign_role" || c.Value == "manage_all")));

    options.AddPolicy("CanManageRoles", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "assign_role" || c.Value == "manage_all")));

    options.AddPolicy("CanViewAuditLogs", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.FindAll("permission").Any(c => c.Value == "assign_role" || c.Value == "manage_all")));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<ISkpdService, SkpdService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<IBeritaService, BeritaService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IPenggunaService, PenggunaService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseSkpdValidation();

app.MapGet("/", () => Results.Ok(new
{
    name = "Website Terintegrasi SKPD Kabupaten Merauke API",
    mode = "Multi-Tenancy + Dynamic Subdomain + API Gateway Ready"
}));

app.MapAuthEndpoints();
app.MapSkpdEndpoints();
app.MapBeritaEndpoints();
app.MapCategoryEndpoints();
app.MapTagEndpoints();
app.MapUploadEndpoints();
app.MapPenggunaEndpoints();
app.MapRoleEndpoints();
app.MapStatsEndpoints();
app.MapAuditLogEndpoints();
app.MapSettingsEndpoints();
app.MapNotificationEndpoints();

app.Run();
