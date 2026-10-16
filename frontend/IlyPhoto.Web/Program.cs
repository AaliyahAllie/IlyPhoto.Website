var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Serve static files (built React SPA assets)
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA Fallback for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
