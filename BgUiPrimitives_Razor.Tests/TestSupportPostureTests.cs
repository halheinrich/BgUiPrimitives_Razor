using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using BgUiPrimitives_Razor.TestSupport;

namespace BgUiPrimitives_Razor.Tests;

/// <summary>
/// The declarations that keep the test-support assembly out of products. It
/// carries bunit and must never reach the trimmed WebAssembly publish; what
/// stops that from this repository's side is one assembly-level declaration,
/// and these pin that nobody quietly removes it — and that the direction of
/// the dependency inside this repository stays the one it is.
/// </summary>
public class TestSupportPostureTests
{
    // Unsupported on "browser": in any project that declares the browser
    // platform — BgQuiz's WebAssembly client and every Razor member do — the
    // platform-compatibility analyzer reports a use of this assembly as
    // CA1416, which the house TreatWarningsAsErrors makes a build error.
    //
    // Stated limit: this pins the declaration, not the analyzer's verdict,
    // which belongs to the referencing project's build (measured once, in a
    // scratch project, when the declaration was introduced). A product that
    // declares no browser platform — a server host — is not stopped by it.
    [Fact]
    public void TheTestSupportAssembly_DeclaresItselfUnsupportedInTheBrowser()
    {
        var declaration = Assert.Single(
            typeof(NoticeBox).Assembly.GetCustomAttributes<UnsupportedOSPlatformAttribute>());

        Assert.Equal("browser", declaration.PlatformName);
    }

    // Trimmable is a promise about a shipped surface; this assembly makes none.
    [Fact]
    public void TheTestSupportAssembly_DoesNotDeclareItselfTrimmable()
    {
        Assert.DoesNotContain(
            typeof(NoticeBox).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key == "IsTrimmable");
    }

    // The library is the product; it references no project at all — neither
    // another member (it sits below them all) nor its own test support. Read
    // from the project file the trim-posture pin already stages.
    [Fact]
    public void TheLibrary_ReferencesNoProject()
    {
        var projectFile = Path.Combine(
            AppContext.BaseDirectory, "TrimPosture", "BgUiPrimitives_Razor.csproj");

        Assert.Empty(XDocument.Load(projectFile).Descendants("ProjectReference"));
    }

    // No test framework rides along into a consumer's test project: a failure
    // is this assembly's own exception, which every framework reports.
    [Fact]
    public void TheTestSupportAssembly_ReferencesNoTestFramework()
    {
        Assert.DoesNotContain(
            typeof(NoticeBox).Assembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
                || a.Name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)
                || a.Name.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase));
    }
}
