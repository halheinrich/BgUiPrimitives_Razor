namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// The two gestures a dismissible <see cref="Notice"/> offers the user. An
/// enum rather than two methods so that a test can be a theory over both: the
/// model rules that both dismiss, and a consumer pins both without naming what
/// either one is in markup.
/// </summary>
public enum NoticeDismissGesture
{
    /// <summary>
    /// The visible close button: the affordance with the keyboard and
    /// screen-reader semantics.
    /// </summary>
    CloseButton,

    /// <summary>The whole box: the large, low-vision-friendly target.</summary>
    WholeBox,
}
