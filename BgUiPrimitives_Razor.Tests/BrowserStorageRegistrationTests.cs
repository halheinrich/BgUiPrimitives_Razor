using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace BgUiPrimitives_Razor.Tests;

// Pins for the one registration a host makes. Its lifetime is the library's
// decision, not each host's: scoped, beside the IJSRuntime it calls, which a
// server scopes per circuit. Read from the service collection bUnit gives a
// test, the same IServiceCollection a host's builder exposes.
public class BrowserStorageRegistrationTests : BunitContext
{
    [Fact]
    public void AddBrowserStorage_RegistersTheAccess_Scoped()
    {
        Services.AddBrowserStorage();

        var registration = Assert.Single(Services, d => d.ServiceType == typeof(BrowserStorage));
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.Equal(typeof(BrowserStorage), registration.ImplementationType);
    }

    // A host may call it, and so may a later layer of the same host's setup;
    // the access is registered once either way.
    [Fact]
    public void AddBrowserStorage_Twice_RegistersOnce()
    {
        Services.AddBrowserStorage();
        Services.AddBrowserStorage();

        Assert.Single(Services, d => d.ServiceType == typeof(BrowserStorage));
    }

    [Fact]
    public void AddBrowserStorage_ReturnsTheCollection_ForChaining()
    {
        Assert.Same(Services, Services.AddBrowserStorage());
    }

    [Fact]
    public void AddBrowserStorage_OnNull_Throws()
    {
        var thrown = Assert.Throws<ArgumentNullException>(
            () => BrowserStorageServiceCollectionExtensions.AddBrowserStorage(null!));

        Assert.Equal("services", thrown.ParamName);
    }

    // What the registration resolves to calls the runtime of its own scope —
    // here bUnit's — which is all a component injecting it relies on.
    [Fact]
    public async Task TheRegisteredAccess_CallsTheScopesRuntime()
    {
        Services.AddBrowserStorage();
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.Setup<string?>("localStorage.getItem", "k").SetResult("v");

        var read = await Services.GetRequiredService<BrowserStorage>().ReadAsync(BrowserStorageArea.Local, "k");

        Assert.Equal("v", read.Value);
        Assert.Equal("localStorage.getItem", Assert.Single(JSInterop.Invocations).Identifier);
        Assert.Same(JSInterop.JSRuntime, Services.GetRequiredService<IJSRuntime>());
    }
}
