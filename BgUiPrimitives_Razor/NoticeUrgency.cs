namespace BgUiPrimitives_Razor;

/// <summary>
/// How insistently assistive technology announces a <see cref="Notice"/>.
/// Urgency is independent of <see cref="NoticeKind"/>: a warning may be
/// either, and the caller chooses by what the notice reports, not by its
/// colour. <see cref="Notice"/> owns which ARIA role each value means.
/// </summary>
public enum NoticeUrgency
{
    /// <summary>
    /// An outcome the user should understand. Announced when the screen
    /// reader is next idle, without interrupting. The default.
    /// </summary>
    Polite,

    /// <summary>
    /// A gesture that failed, or a loss about to happen. Announced at once,
    /// interrupting whatever is being read.
    /// </summary>
    Assertive,
}
