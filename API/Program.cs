// using System.Text;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.IdentityModel.Tokens;
// using Microsoft.Net.Http.Headers;
// using Microsoft.OpenApi.Models;
// using Npgsql;
// using StackExchange.Redis;

// using Repository.Interfaces;
// using Repository.Implementations;
// using Repository.Services;
// using Repository;
// using API.Services;
// using System.Security.Claims;

// var builder = WebApplication.CreateBuilder(args);

// // =========================
// // Controllers
// // =========================
// builder.Services.AddControllers();

// // =========================
// // Dependency Injection (Repositories)
// // =========================
// builder.Services.AddScoped<IAdmincategoiresInteface, AdminCategoriesRepository>();
// builder.Services.AddScoped<IAdminUsersInterface, AdminUsersRepository>();
// builder.Services.AddScoped<IAdminArtistInterface, AdminArtistRepository>();
// builder.Services.AddScoped<IAdminInterface, AdminRepository>();
// builder.Services.AddScoped<IAuthInterface, AuthRepository>();
// builder.Services.AddScoped<IArtistInterface, ArtistRepository>();
// builder.Services.AddScoped<IArtworkInterface, ArtworkRepository>();
// builder.Services.AddScoped<IUserProfileInterface, UserProfileRepository>();
// builder.Services.AddScoped<IAdminArtworkInterface, AdminArtworkRepository>();
// builder.Services.AddScoped<IAdminOrderInterface, AdminOrdersRepository>();
// builder.Services.AddScoped<IAdminPayoutInterface, AdminPayoutRepository>();

// //paymet 
// builder.Services.AddScoped<IBuyerUiArtworkInterface, BuyerUiArtworkRepository>();
// builder.Services.AddScoped<IPaymentInterface, PaymentRepository>();
// builder.Services.AddScoped<IWishlistInterface, WishlistRepository>();
// builder.Services.AddScoped<IOrderInterface, OrderRepository>();

// //paypal Service
// builder.Services.AddHttpClient<PaypalService>();
// builder.Services.AddScoped<InvoiceService>();

// builder.Services.AddScoped<EmailServices>();
// builder.Services.AddScoped<IBuyerOrderInterface, BuyerOrderRepository>();


// // =========================
// // Services
// // =========================
// builder.Services.AddScoped<AdminArtworkService>();
// // builder.Services.AddSingleton<RabbitMQProducer>();
// builder.Services.AddScoped<EmailServices>();

// // ✅ FIXED Redis Registration
// builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
// {
//     var redisConnection = builder.Configuration.GetConnectionString("Redis");

//     if (string.IsNullOrEmpty(redisConnection))
//         throw new Exception("Redis connection string missing");

//     return ConnectionMultiplexer.Connect(redisConnection);
// });

// builder.Services.AddScoped<RedisService>();

// // =========================
// // Swagger
// // =========================
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.AddSecurityDefinition("token", new OpenApiSecurityScheme
//     {
//         Type = SecuritySchemeType.Http,
//         Scheme = "Bearer",
//         BearerFormat = "JWT",
//         In = ParameterLocation.Header,
//         Name = HeaderNames.Authorization
//     });

//     c.AddSecurityRequirement(new OpenApiSecurityRequirement {
//         {
//             new OpenApiSecurityScheme {
//                 Reference = new OpenApiReference {
//                     Type = ReferenceType.SecurityScheme,
//                     Id = "token"
//                 }
//             },
//             new string[] {}
//         }
//     });
// });

// // =========================
// // PostgreSQL
// // =========================
// builder.Services.AddScoped<NpgsqlConnection>(sp =>
// {
//     var config = sp.GetRequiredService<IConfiguration>();
//     var connectionString = config.GetConnectionString("pgconn");

//     if (string.IsNullOrEmpty(connectionString))
//         throw new Exception("DefaultConnection is missing in appsettings.json");

//     return new NpgsqlConnection(connectionString);
// });

// // =========================
// // CORS
// // =========================
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("corsapp", policy =>
//     {
//         policy.AllowAnyOrigin()
//               .AllowAnyMethod()
//               .AllowAnyHeader();
//     });
// });

// // =========================
// // JWT Authentication
// // =========================
// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
// })
// .AddJwtBearer(options =>
// {
//     options.RequireHttpsMetadata = false;
//     options.SaveToken = true;

//     options.TokenValidationParameters = new TokenValidationParameters
//     {
//         ValidateIssuer = true,
//         ValidateAudience = true,
//         ValidateLifetime = true,
//         ValidateIssuerSigningKey = true,

//         ValidIssuer = builder.Configuration["Jwt:Issuer"],
//         ValidAudience = builder.Configuration["Jwt:Audience"],
//         IssuerSigningKey = new SymmetricSecurityKey(
//             Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
//         ), 
//         RoleClaimType = ClaimTypes.Role
//     };
// });

// // =========================
// // Redis Cache (Optional)
// // =========================
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     var redis = builder.Configuration.GetConnectionString("Redis");

//     if (string.IsNullOrEmpty(redis))
//         throw new Exception("Redis connection string missing");

//     options.Configuration = redis;
//     options.InstanceName = "Session_";
// });

// // =========================
// // Session
// // =========================
// builder.Services.AddSession(options =>
// {
//     options.IdleTimeout = TimeSpan.FromMinutes(30);
//     options.Cookie.HttpOnly = true;
//     options.Cookie.IsEssential = true;
// });

// // =========================
// // Build App
// // =========================
// var app = builder.Build();

// // =========================
// // Middleware
// // =========================
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

// app.UseHttpsRedirection();

// app.UseStaticFiles();

// app.UseCors("corsapp");

// app.UseSession();

// app.UseAuthentication();
// app.UseAuthorization();

// // ✅ FIXED: Default route to avoid 404 on "/"
// app.MapGet("/", () => "Artify API is running 🚀");

// // Controllers
// app.MapControllers();

// app.Run();



// ============================================================
//  API/Program.cs
//  ROOT CAUSE FIX:
//    AllowAnyOrigin() + withCredentials:true = browser blocks it.
//    Must use WithOrigins(...) + AllowCredentials() together.
// ============================================================

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
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
builder.Services.AddScoped<IBuyerUiArtworkInterface, BuyerUiArtworkRepository>();
builder.Services.AddScoped<IPaymentInterface, PaymentRepository>();
builder.Services.AddScoped<IWishlistInterface, WishlistRepository>();
builder.Services.AddScoped<IOrderInterface, OrderRepository>();
builder.Services.AddHttpClient<PaypalService>();
builder.Services.AddScoped<InvoiceService>();
// builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IBuyerOrderInterface, BuyerOrderRepository>();

// =========================
// Services
// =========================
builder.Services.AddScoped<AdminArtworkService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<ElasticService>();

// =========================
// Redis
// =========================
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(redisConnection))
        throw new Exception("Redis connection string missing");
    return ConnectionMultiplexer.Connect(redisConnection);
});
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<RabbitService>();

// =========================
// Swagger
// =========================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("token", new OpenApiSecurityScheme
    {
        Type           = SecuritySchemeType.Http,
        Scheme         = "Bearer",
        BearerFormat   = "JWT",
        In             = ParameterLocation.Header,
        Name           = HeaderNames.Authorization
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "token"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =========================
// PostgreSQL
// =========================
builder.Services.AddScoped<NpgsqlConnection>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
                             .GetConnectionString("pgconn");
    if (string.IsNullOrEmpty(connectionString))
        throw new Exception("pgconn connection string is missing in appsettings.json");
    return new NpgsqlConnection(connectionString);
});

// =========================
// ✅ CORS — FIXED
// ─────────────────────────────────────────────────────────────
// THE BUG:  AllowAnyOrigin() cannot be combined with
//           AllowCredentials(). When withCredentials:true is
//           set on the browser side, the spec requires the
//           server to echo the exact requesting origin, NOT "*".
//
// THE FIX:  Replace AllowAnyOrigin() with WithOrigins(...)
//           listing every MVC origin that will send credentials,
//           then chain AllowCredentials().
//
// Add more origins here if you deploy to staging/production.
// =========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("corsapp", policy =>
    {
        policy
            // ← list every origin that calls the API with credentials
            .WithOrigins(
                "http://localhost:5092",   // MVC dev server (HTTP)
                "https://localhost:5092",  // MVC dev server (HTTPS)
                "http://localhost:5174",   // Vite / React dev (if used)
                "https://localhost:5174"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()           // ← REQUIRED for withCredentials:true
            .WithExposedHeaders("Set-Cookie"); // ← ensures HttpOnly cookie is readable
    });
});

// =========================
// JWT Authentication
// =========================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing from appsettings.json");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken            = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
                                       Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType            = ClaimTypes.Role,
        ClockSkew                = TimeSpan.Zero   // no grace period on expiry
    };

    // ── Also accept JWT from the HttpOnly cookie ──────────────
    // This lets MVC server-side [Authorize] work alongside the
    // Authorization header used by API clients.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            if (string.IsNullOrEmpty(ctx.Token))
                ctx.Token = ctx.Request.Cookies["ArtistToken"];
            return Task.CompletedTask;
        }
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Artist/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    // Fix: Explicitly qualify the namespace
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// =========================
// Redis Cache
// =========================
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redis = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(redis))
        throw new Exception("Redis connection string missing");
    options.Configuration = redis;
    options.InstanceName  = "Session_";
});

// =========================
// Session
// =========================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Fix: Explicitly qualify the namespace
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization();

// =========================
// Build App
// =========================
var app = builder.Build();
var rabbitService = app.Services.GetRequiredService<RabbitService>();

_ = rabbitService.RunConsumerLoopAsync(app.Lifetime.ApplicationStopping);

using (var scope = app.Services.CreateScope())
{
    var elasticService = scope.ServiceProvider.GetRequiredService<ElasticService>();
    await elasticService.EnsureArtworkIndexAsync();
}

// =========================
// Middleware Pipeline
// ORDER MATTERS — do not rearrange
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// ✅ CORS must come BEFORE UseAuthentication / UseAuthorization
app.UseCors("corsapp");

app.UseSession();         // session before auth
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Artify API is running 🚀");
app.MapControllers();

app.Run();
