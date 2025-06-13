using Draugesac.UI;
using Draugesac.UI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to communicate with the backend API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:8080/api/") });

// Register DocumentStateService as a scoped service since it now depends on HttpClient
builder.Services.AddScoped<DocumentStateService>();

// Register NotificationService for toast notifications
builder.Services.AddScoped<NotificationService>();

await builder.Build().RunAsync();
