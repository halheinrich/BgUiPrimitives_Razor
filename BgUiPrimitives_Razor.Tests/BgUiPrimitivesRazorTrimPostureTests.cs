using System.Reflection;
using System.Xml.Linq;

namespace BgUiPrimitives_Razor.Tests;

/// <summary>
/// The declarations that make this library's half of the trim gate a gate
/// rather than a suggestion (in the mould of XgFilter_Razor's and
/// BgFolderAccess_Razor's posture pins). The analyzer's own verdict is
/// enforced by the build under TreatWarningsAsErrors; what these pin is that
/// nobody quietly switches the premises off — dropping IsTrimmable or the
/// analyzer switch from the csproj fails a test rather than silently
/// deferring the next trim-unsafe construct to BgQuiz's trimmed publish.
/// </summary>
public class BgUiPrimitivesRazorTrimPostureTests
{
    // IsTrimmable surfaces in the built assembly as SDK-emitted metadata: the
    // effective value, whatever file it was set in.
    [Fact]
    public void TheAssembly_DeclaresItselfTrimmable()
    {
        Assert.Contains(
            typeof(Notice).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
            a => a.Key == "IsTrimmable" && a.Value == "True");
    }

    // The analyzer switch leaves no trace in the assembly, and a build with it
    // off is simply green, so its one readable trace is the project file
    // itself — linked into this project's output by the test csproj.
    //
    // Stated limit: this reads the declaration, not the evaluated property; a
    // later import that overrode it would pass here. Nothing in this repo
    // imports after the csproj.
    [Theory]
    [InlineData("IsTrimmable")]
    [InlineData("EnableTrimAnalyzer")]
    public void TheProjectFile_DeclaresTheTrimPosture(string property)
    {
        var projectFile = Path.Combine(
            AppContext.BaseDirectory, "TrimPosture", "BgUiPrimitives_Razor.csproj");

        var declaration = Assert.Single(XDocument.Load(projectFile).Descendants(property));

        Assert.Equal("true", declaration.Value);
        Assert.Null(declaration.Attribute("Condition"));
        Assert.Null(declaration.Parent!.Attribute("Condition"));
    }
}
