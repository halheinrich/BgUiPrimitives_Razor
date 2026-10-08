using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BgUiPrimitives_Razor;

/// <summary>
/// Registers <see cref="BrowserStorage"/> with a host's services.
/// </summary>
public static class BrowserStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BrowserStorage"/> as a scoped service, so that a
    /// component or service in the host can inject it. Registering it more
    /// than once registers it once.
    /// </summary>
    /// <remarks>
    /// Scoped because the <see cref="Microsoft.JSInterop.IJSRuntime"/> it calls
    /// is scoped where the platform scopes it — one per circuit on a server —
    /// and an access must never outlive the runtime of its own scope. The type
    /// holds no other state, so nothing else depends on the lifetime.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddBrowserStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<BrowserStorage>();
        return services;
    }
}
