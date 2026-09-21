namespace BgUiPrimitives_Razor.TestSupport;

/// <summary>
/// Thrown when a rendered notice is not what the test said it is: the element
/// is not a notice box, the box is not a coherent one, it differs from what
/// <see cref="NoticeBox.ShouldBe"/> was told, or a gesture was asked of a
/// notice that does not offer it.
/// </summary>
/// <remarks>
/// This assembly's own type rather than a test framework's assertion
/// exception, so that it references no test framework: every framework
/// reports an exception thrown by a test as that test's failure, with this
/// message. Only this assembly raises it.
/// </remarks>
public sealed class NoticeAssertionException : Exception
{
    internal NoticeAssertionException(string message)
        : base(message)
    {
    }
}
