using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Web.Hubs;
using DocumentProcessor.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add SignalR
builder.Services.AddSignalR();

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add infrastructure services (Database, Repositories, Document Sources)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add health checks
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);

// Add additional logging if needed
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Ensure database is created and migrations are applied
await app.Services.EnsureDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DocumentProcessor.Web.Client._Imports).Assembly);

// Map SignalR hub
app.MapHub<DocumentProcessingHub>("/documentHub");

app.Run();