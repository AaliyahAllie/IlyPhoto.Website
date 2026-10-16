using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHttpContextAccessor();

// ---- Configuration Sections ----
builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.Configure<PhotoStorageOptions>(builder.Configuration.GetSection("PhotoStorage"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<BusinessOptions>(builder.Configuration.GetSection("Business"));

// ---- Azure Cloud Database, Blob Storage & Stores ----
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AzureTableContext>();
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
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<IEmailService, EmailService>();

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

// Pre-initialize Azure Table Storage & Azure Blob Storage at startup
try
{
    app.Services.GetRequiredService<AzureTableContext>();
    app.Services.GetRequiredService<AzureBlobPhotoStorage>();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Azure storage pre-initialization warning: {Message}", ex.Message);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
