using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using skpd_multi_tenant.Endpoints;
using skpd_multi_tenant.Middleware;
using skpd_multi_tenant.Options;
using skpd_multi_tenant.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
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
    });

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("BeritaPolicy", context =>
    {
        // Ambil IP pengguna
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // TokenBucketLimiter per IP
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,            // max 10 request
                Window = TimeSpan.FromMinutes(1), // per 1 menit
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0               // tidak ada antrian, langsung reject jika over limit
            });
    });
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IMySqlConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<ISkpdService, SkpdService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<IBeritaService, BeritaService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

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

app.Run();
