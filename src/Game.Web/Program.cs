using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Game.Core.Localization;
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
builder.Services.AddSingleton<ILocalizer>(_ => new Localizer());
builder.Services.AddSingleton<SaveCoordinator>();
builder.Services.AddSingleton<LanguageService>();
builder.Services.AddSingleton<GameLoop>();
builder.Services.AddSingleton<AudioService>();
// A singleton, so it can't take the scoped HttpClient above; give it its own
// bound to the same base address.
builder.Services.AddSingleton(sp => new UpdateChecker(
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));
builder.Services.AddScoped<TooltipService>();

await builder.Build().RunAsync();
