using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();

// ---- Configuration Sections ----
builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.Configure<AzureCloudApiOptions>(builder.Configuration.GetSection("AzureCloudApi"));
builder.Services.Configure<PhotoStorageOptions>(builder.Configuration.GetSection("PhotoStorage"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<BusinessOptions>(builder.Configuration.GetSection("Business"));

// ---- Azure Cloud Database, Blob Storage, Shared Cloud Sync & Stores ----
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AzureTableContext>();
builder.Services.AddSingleton<AzureCloudSyncService>();
builder.Services.AddSingleton<LocalDiskPhotoStorage>();
builder.Services.AddSingleton<AzureBlobPhotoStorage>();
builder.Services.AddSingleton<IPhotoStorage>(sp => sp.GetRequiredService<AzureBlobPhotoStorage>());
builder.Services.AddSingleton<CounterStore>();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<BookingStore>();
builder.Services.AddSingleton<InvoiceStore>();
builder.Services.AddSingleton<ServiceStore>();
builder.Services.AddSingleton<ReviewStore>();
builder.Services.AddSingleton<EnquiryStore>();
builder.Services.AddSingleton<GalleryStore>();
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<DemoSeederService>();
builder.Services.AddHealthChecks();

// ---- Authentication via Secure Cookie ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "ily.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Pre-initialize Azure Table Storage, Azure Blob Storage & Shared Cloud Sync at startup
try
{
    app.Services.GetRequiredService<AzureTableContext>();
    app.Services.GetRequiredService<AzureBlobPhotoStorage>();
    var cloudSync = app.Services.GetRequiredService<AzureCloudSyncService>();
    await cloudSync.EnsureSyncedAsync(force: true);

    var bookingStore = app.Services.GetRequiredService<BookingStore>();
    if ((await bookingStore.ListAllAsync()).Count == 0)
    {
        var seeder = app.Services.GetRequiredService<DemoSeederService>();
        await seeder.SeedShowcaseAsync();
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Azure storage pre-initialization warning: {Message}", ex.Message);
}

// Enforce security headers across all responses
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
