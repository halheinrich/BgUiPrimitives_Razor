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

    // The planner restates BrowserStorage's interop identifiers privately and
    // offers none of them. A test that took its expected identifiers from the
    // planner would agree with whatever the planner said: BrowserStorageTests,
    // which is the independent statement the planner is held to, and a
    // consumer's test, which must not spell the wire at all.
    //
    // Stated limit: a structural proxy. It finds a public static string —
    // constant, field, property or method — anywhere in the assembly; it does
    // not see a string handed out another way, such as an instance member or
    // a collection.
    [Fact]
    public void TheTestSupportAssembly_OffersNoStaticString_ForATestToTakeAnIdentifierFrom()
    {
        const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var offered = typeof(NoticeBox).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(PublicStatic))
            .Where(member => member switch
            {
                FieldInfo field => field.FieldType == typeof(string),
                PropertyInfo property => property.PropertyType == typeof(string),
                MethodInfo method => !method.IsSpecialName && method.ReturnType == typeof(string),
                _ => false,
            })
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}");

        Assert.Empty(offered);
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
