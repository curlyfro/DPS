using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Web.Hubs;
using DocumentProcessor.Web.Services;
using DocumentProcessor.Application;
using DocumentProcessor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR
builder.Services.AddSignalR();

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add infrastructure services (Database, Repositories, Document Sources)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add application services (Document processing, Background services)
builder.Services.AddApplicationServices();

// TEST: Add a simple background service to verify hosting is working
builder.Services.AddHostedService<DocumentProcessor.Web.TestBackgroundService>();

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false; // Set to true in production
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure authentication cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Add authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdminRole", policy => policy.RequireRole("Administrator"))
    .AddPolicy("RequireManagerRole", policy => policy.RequireRole("Administrator", "Manager"))
    .AddPolicy("RequireUserRole", policy => policy.RequireRole("Administrator", "Manager", "User"));

// Add health checks
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);

// Add additional logging if needed
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Add rate limiting (simplified for now - can be enhanced later)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Ensure database is created and migrations are applied
await app.Services.EnsureDatabaseAsync();

// Seed initial roles and admin user
await SeedIdentityData(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Development environment configuration
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add response compression
app.UseResponseCompression();

// Add rate limiting
app.UseRateLimiter();

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Serve static files from wwwroot
app.UseStaticFiles();

// Serve files from the uploads directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub
app.MapHub<DocumentProcessingHub>("/documentHub");

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.Run();

// Helper method to seed initial data
async Task SeedIdentityData(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Seed roles
    string[] roleNames = { "Administrator", "Manager", "User" };
    foreach (var roleName in roleNames)
    {
        var roleExists = await roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            var role = new ApplicationRole
            {
                Name = roleName,
                Description = $"{roleName} role for document processing system",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            await roleManager.CreateAsync(role);
        }
    }

    // Seed admin user
    var adminEmail = "admin@documentprocessor.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            Department = "IT",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@123456");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrator");
        }
    }
}