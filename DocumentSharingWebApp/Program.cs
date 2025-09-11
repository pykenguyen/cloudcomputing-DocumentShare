using System;
using System.IO;
using DocumentSharingWebApp.Models;
using ImageMagick; // for MagickNET.SetGhostscriptDirectory
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== EF Core (SQL Server) =====
builder.Services.AddDbContext<DocumentSharingDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== MVC + (optional) runtime compilation =====
builder.Services
    .AddControllersWithViews()
#if DEBUG
    .AddRazorRuntimeCompilation()
#endif
    ;

// ===== Auth (Cookies) =====
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);

        options.Cookie.Name = "DocShare.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax; // good default for forms + redirects
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // set Always if forcing HTTPS
    });

// ===== Session =====
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "DocShare.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromHours(12);
});

builder.Services.AddHttpContextAccessor();

// ===== Form / Upload limits (default 300MB) =====
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 300L * 1024 * 1024;
    o.ValueLengthLimit = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
});

// ===== Cookie policy (for SameSite warnings) =====
builder.Services.Configure<CookiePolicyOptions>(o =>
{
    o.MinimumSameSitePolicy = SameSiteMode.Lax;
});

var app = builder.Build();

// ===== Magick.NET: point to Ghostscript (gsdll64.dll) directory =====
// Put this in appsettings.json:  "Ghostscript": { "BinDir": "C:\\Program Files\\gs\\gs10.03.0\\bin" }
var gsDir = builder.Configuration["Ghostscript:BinDir"];
if (!string.IsNullOrWhiteSpace(gsDir) && Directory.Exists(gsDir))
{
    MagickNET.SetGhostscriptDirectory(gsDir);
}

// ===== Ensure folders exist =====
var webroot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(webroot, "uploads"));
Directory.CreateDirectory(Path.Combine(webroot, "thumbs"));

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCookiePolicy();   // before auth/session is fine
app.UseAuthentication();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
