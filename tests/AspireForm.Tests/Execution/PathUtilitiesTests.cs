using AspireForm.Execution;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Execution;

public sealed class PathUtilitiesTests
{
    [Fact]
    public void ToRepoRelative_returns_forward_slashed_relative_path()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var absolute = Path.Combine(projectDir, "src", "Foo.cs");

        PathUtilities.ToRepoRelative(absolute, projectDir).Should().Be("src/Foo.cs");
    }

    [Fact]
    public void ToRepoRelative_returns_the_input_when_the_path_is_outside_the_project_directory()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var elsewhere = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "other", "Foo.cs"));

        // Outside the project — keep absolute (with forward-slash normalisation).
        var result = PathUtilities.ToRepoRelative(elsewhere, projectDir);
        result.Should().Contain("/").And.NotStartWith("../");
    }

    [Fact]
    public void FromRepoRelative_combines_with_projectDir_and_returns_an_absolute_path()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proj"));
        var resolved = PathUtilities.FromRepoRelative("src/Foo.cs", projectDir);

        Path.IsPathRooted(resolved).Should().BeTrue();
        resolved.Replace('\\', '/').Should().EndWith("proj/src/Foo.cs");
    }
}
