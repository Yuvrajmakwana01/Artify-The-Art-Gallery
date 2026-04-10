using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Npgsql;
using Repository.Implementations;
using Repository.Interfaces;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddScoped<IAdmincategoiresInteface, AdminCategoriesRepository>();
builder.Services.AddScoped<IAdminUsersInterface, AdminUsersRepository>();
builder.Services.AddScoped<IAdminArtistInterface, AdminArtistRepository>();
builder.Services.AddEndpointsApiExplorer();

// Swagger (ONLY ONCE)
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("token", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer",
        In = ParameterLocation.Header,
        Name = HeaderNames.Authorization
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "token"
                }
            },
            Array.Empty<string>()
        }
    });
});

// PostgreSQL
builder.Services.AddScoped<NpgsqlConnection>(conn =>
{
    var configuration = conn.GetRequiredService<IConfiguration>();
    var raw = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")?.Trim();
    if (string.IsNullOrWhiteSpace(raw))
    {
        raw = configuration.GetConnectionString("pgconn")?.Trim();
    }
    if (string.IsNullOrWhiteSpace(raw))
    {
        raw = configuration.GetConnectionString("DefaultConnection")?.Trim();
    }

    if (string.IsNullOrWhiteSpace(raw) ||
        raw.Equals("your-postgres-connection", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("YOUR_DB_USER", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("REPLACE_WITH_YOUR_NEON_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("YOUR_REAL_NEON_PASSWORD", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Set a valid PostgreSQL connection string. Use env var POSTGRES_CONNECTION_STRING or API/appsettings.json -> ConnectionStrings:pgconn");
    }

    string connectionString;
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length < 2)
            throw new InvalidOperationException("Invalid PostgreSQL URL format. Expected: postgresql://user:password@host:port/database");

        var database = uri.AbsolutePath.Trim('/');
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={database};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])};SSL Mode=Require;Trust Server Certificate=true";
    }
    else
    {
        connectionString = raw;
    }

    return new NpgsqlConnection(connectionString);
});

// CORS (FIXED)
builder.Services.AddCors(options =>
{
    options.AddPolicy("corsapp", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// JWT Authentication (FIXED SECURITY)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true, // ✅ FIXED
        ValidateIssuerSigningKey = true,

        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
        )
    };
});

// Redis
builder.Services.AddScoped<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisConn = config.GetConnectionString("Redis");

    if (string.IsNullOrEmpty(redisConn))
        throw new InvalidOperationException("Redis connection string missing");

    return ConnectionMultiplexer.Connect(redisConn);
});

// Redis DB
builder.Services.AddScoped<IDatabase>(sp =>
{
    var mux = sp.GetRequiredService<IConnectionMultiplexer>();
    return mux.GetDatabase();
});

// Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Session_";
});

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// =========================
// Middleware (ORDER FIXED)
// =========================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("corsapp");

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllers();

app.Run();
