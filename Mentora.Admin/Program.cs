using Mentora.Admin;
using Mentora.Admin.Api;
using Mentora.Admin.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var config = new AppConfig(
    builder.Configuration["ApiBaseUrl"],
    builder.Configuration["EnvironmentLabel"]);
builder.Services.AddSingleton(config);

// Sans ApiBaseUrl valide, l'écran d'erreur remplace l'application : le client n'est jamais appelé.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = config.ApiBaseUri ?? new Uri("http://invalid.local/")
});
builder.Services.AddScoped<ApiTransport>();
builder.Services.AddScoped<AdminSession>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();
