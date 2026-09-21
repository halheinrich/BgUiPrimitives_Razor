namespace BgUiPrimitives_Razor;

/// <summary>
/// How assistive technology comes to announce a <see cref="Notice"/>: by the
/// notice itself, politely or assertively, or by a live region the caller
/// already keeps around it. One decision, not two — a notice announced by an
/// enclosing region has no urgency of its own to state, because the region
/// that announces it decides that. Independent of <see cref="NoticeKind"/>: a
/// warning may be any of these, and the caller chooses by what the notice
/// reports and where it sits, not by its colour. <see cref="Notice"/> owns
/// which ARIA role each value means, and where it lands.
/// </summary>
public enum NoticeAnnouncement
{
    /// <summary>
    /// An outcome the user should understand. The notice announces itself
    /// when the screen reader is next idle, without interrupting. The default.
    /// </summary>
    Polite,

    /// <summary>
    /// A gesture that failed, or a loss about to happen. The notice announces
    /// itself at once, interrupting whatever is being read.
    /// </summary>
    Assertive,

    /// <summary>
    /// The notice sits inside a live region the caller keeps mounted, and
    /// that region announces it. The notice renders no live-region role of its
    /// own: one live region nested in another is announced twice by some
    /// screen readers. How insistently it is announced is the enclosing
    /// region's to say.
    /// </summary>
    ByEnclosingRegion,
}
