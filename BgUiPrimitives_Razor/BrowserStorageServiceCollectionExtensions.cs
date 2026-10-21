using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BgUiPrimitives_Razor;

/// <summary>
/// Registers <see cref="BrowserStorage"/>, and the reporting access over it,
/// with a host's services.
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

    /// <summary>
    /// Registers <see cref="ReportingBrowserStorage"/> and the app's
    /// <see cref="BrowserStorageCondition"/> as scoped services, together with
    /// <see cref="BrowserStorage"/> (by calling <see cref="AddBrowserStorage"/>),
    /// so the condition is never registered without the access that reports to
    /// it. Registering them more than once registers each once.
    /// </summary>
    /// <remarks>
    /// Scoped, so there is one condition per loaded app — per circuit on a
    /// server — beside the <see cref="BrowserStorage"/> whose refusals it
    /// records. The access's logger comes from the host's logging.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddReportingBrowserStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddBrowserStorage();
        services.TryAddScoped<BrowserStorageCondition>();
        services.TryAddScoped<ReportingBrowserStorage>();
        return services;
    }
}
