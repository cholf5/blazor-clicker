using Microsoft.JSInterop;

namespace Game.Web.Services;

/// <summary>
/// Thin wrapper over browser <c>localStorage</c> via IJSRuntime. Kept behind
/// an interface-y facade so tests could stub it later if we ever move
/// autosave logic into Game.Core.
/// </summary>
public sealed class LocalStorageService
{
    private readonly IJSRuntime _js;

    public LocalStorageService(IJSRuntime js) => _js = js;

    public ValueTask<string?> GetItemAsync(string key) =>
        _js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetItemAsync(string key, string value) =>
        _js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveItemAsync(string key) =>
        _js.InvokeVoidAsync("localStorage.removeItem", key);
}
