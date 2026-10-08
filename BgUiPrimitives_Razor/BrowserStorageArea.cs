namespace BgUiPrimitives_Razor;

/// <summary>
/// Which of the browser's two Web Storage areas a <see cref="BrowserStorage"/>
/// operation addresses (HTML Living Standard, "Web storage",
/// https://html.spec.whatwg.org/multipage/webstorage.html). The caller names
/// the area; <see cref="BrowserStorage"/> alone knows which browser object
/// that means, so no caller spells a JavaScript identifier.
/// </summary>
public enum BrowserStorageArea
{
    /// <summary>The origin's <c>localStorage</c>, kept across visits.</summary>
    Local,

    /// <summary>The tab's <c>sessionStorage</c>, kept for its session.</summary>
    Session,
}
