using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Game.Web;
using Game.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Wire up game services. SaveCoordinator owns the current GameState;
// GameLoop reads from it every tick so state replacement (import / wipe)
// takes effect immediately.
builder.Services.AddSingleton<LocalStorageService>();
builder.Services.AddSingleton<SaveCoordinator>();
builder.Services.AddSingleton<GameLoop>();

await builder.Build().RunAsync();
