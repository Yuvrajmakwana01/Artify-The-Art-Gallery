using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using Npgsql;
using StackExchange.Redis;

using Repository.Interfaces;
using Repository.Implementations;
using Repository.Services;
using Repository;
using API.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Controllers
// =========================
builder.Services.AddControllers();

// =========================
// Dependency Injection (Repositories)
// =========================
builder.Services.AddScoped<IAdmincategoiresInteface, AdminCategoriesRepository>();
builder.Services.AddScoped<IAdminUsersInterface, AdminUsersRepository>();
builder.Services.AddScoped<IAdminArtistInterface, AdminArtistRepository>();
builder.Services.AddScoped<IAdminInterface, AdminRepository>();
builder.Services.AddScoped<IAuthInterface, AuthRepository>();
builder.Services.AddScoped<IArtistInterface, ArtistRepository>();
builder.Services.AddScoped<IArtworkInterface, ArtworkRepository>();
builder.Services.AddScoped<IUserProfileInterface, UserProfileRepository>();
builder.Services.AddScoped<IAdminArtworkInterface, AdminArtworkRepository>();
builder.Services.AddScoped<IAdminOrderInterface, AdminOrdersRepository>();
builder.Services.AddScoped<IAdminPayoutInterface, AdminPayoutRepository>();

//paymet 
builder.Services.AddScoped<IBuyerUiArtworkInterface, BuyerUiArtworkRepository>();
builder.Services.AddScoped<IPaymentInterface, PaymentRepository>();
builder.Services.AddScoped<IWishlistInterface, WishlistRepository>();
builder.Services.AddScoped<IOrderInterface, OrderRepository>();

//paypal Service
builder.Services.AddHttpClient<PaypalService>();
builder.Services.AddScoped<InvoiceService>();

builder.Services.AddScoped<EmailServices>();
builder.Services.AddScoped<IBuyerOrderInterface, BuyerOrderRepository>();


// =========================
// Services
// =========================
builder.Services.AddScoped<AdminArtworkService>();
builder.Services.AddSingleton<RabbitMQProducer>();
builder.Services.AddScoped<EmailServices>();

// ✅ FIXED Redis Registration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");

    if (string.IsNullOrEmpty(redisConnection))
        throw new Exception("Redis connection string missing");

    return ConnectionMultiplexer.Connect(redisConnection);
});

builder.Services.AddScoped<RedisService>();

// =========================
// Swagger
// =========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("token", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
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
            new string[] {}
        }
    });
});

// =========================
// PostgreSQL
// =========================
builder.Services.AddScoped<NpgsqlConnection>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("pgconn");

    if (string.IsNullOrEmpty(connectionString))
        throw new Exception("DefaultConnection is missing in appsettings.json");

    return new NpgsqlConnection(connectionString);
});

// =========================
// CORS
// =========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("corsapp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// =========================
// JWT Authentication
// =========================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
        ), 
        RoleClaimType = ClaimTypes.Role
    };
});

// =========================
// Redis Cache (Optional)
// =========================
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redis = builder.Configuration.GetConnectionString("Redis");

    if (string.IsNullOrEmpty(redis))
        throw new Exception("Redis connection string missing");

    options.Configuration = redis;
    options.InstanceName = "Session_";
});

// =========================
// Session
// =========================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =========================
// Build App
// =========================
var app = builder.Build();

// =========================
// Middleware
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("corsapp");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// ✅ FIXED: Default route to avoid 404 on "/"
app.MapGet("/", () => "Artify API is running 🚀");

// Controllers
app.MapControllers();

app.Run();

