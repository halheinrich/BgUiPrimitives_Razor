namespace BgUiPrimitives_Razor;

/// <summary>
/// What a <see cref="Notice"/> is telling the user. The caller names the kind;
/// <see cref="Notice"/> alone decides which stylesheet class — and so which
/// colour — that means, so no caller ever passes a CSS string.
/// </summary>
/// <remarks>
/// Kind decides colour and nothing else. Whether a notice may be dismissed
/// (<see cref="Notice.Dismissible"/>) and how urgently it is announced
/// (<see cref="Notice.Urgency"/>) are separate decisions, because what a box
/// <em>is</em> decides them, never its colour (the umbrella's
/// SPEC-notices.md, sections 1 and 3).
/// </remarks>
public enum NoticeKind
{
    /// <summary>Neutral information the user should know.</summary>
    Information,

    /// <summary>Something the user should take note of or act on.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,

    /// <summary>Something the user asked for succeeded.</summary>
    Success,
}
