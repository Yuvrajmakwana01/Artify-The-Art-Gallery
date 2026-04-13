using Npgsql;
using Repository.Implementations;
using Repository.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// ── PostgreSQL ─────────────────────────────────────────────────────────
builder.Services.AddScoped<NpgsqlConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("pgconn")));

// ── Repository DI ──────────────────────────────────────────────────────
builder.Services.AddScoped<IArtistInterface, ArtistRepository>();

// ── Session ────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});



builder.Services.AddScoped<IArtistInterface, ArtistRepository>();


// Register the Artist Repository for Dependency Injection
builder.Services.AddScoped<IArtistInterface, ArtistRepository>();
builder.Services.AddScoped<IArtworkInterface, ArtworkRepository>();


builder.Services.AddSingleton<NpgsqlConnection>((asas) =>
{
    var connectionString = asas.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    return new NpgsqlConnection(connectionString);
});

builder.Services.AddAntiforgery(options => {
    options.Cookie.Expiration = TimeSpan.Zero; // Cookies ko expire hone par turant delete kare
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Google";
})
.AddCookie("Cookies", options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTP allow karne ke liye
    options.LoginPath = "/Auth/UserLogin";
    options.ExpireTimeSpan = TimeSpan.FromDays(20);
})
.AddGoogle("Google", options =>
{
    options.SignInScheme = "Cookies";
    
    options.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

    // Correlation cookie ki settings ko relax karein
    options.CorrelationCookie.Name = "Artify.Correlation"; 
    options.CorrelationCookie.HttpOnly = true;
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    
    // 2. 🔥 Phone Verification Fix:
    // Phone pe code aane mein time lagta hai, isliye timeout badhana zaroori hai
    options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(15);
    
    // Google se yeh claims lenge
    options.Scope.Add("email");
    options.Scope.Add("profile");

    // Login ke baad yahan aayega
    options.CallbackPath = "/signin-google";

    // 3. Prompt selection (Optional but recommended for multiple accounts)
    // Isse har baar account chunne ka option milega
    options.Events.OnRedirectToAuthorizationEndpoint = context =>
    {
        context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
        return Task.CompletedTask;
    };
});

// (Optional) Session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


// (Optional)
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();