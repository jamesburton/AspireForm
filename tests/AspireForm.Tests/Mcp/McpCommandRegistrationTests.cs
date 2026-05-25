using AspireForm.Cli;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpCommandRegistrationTests
{
    [Fact]
    public void BuildRegistry_registers_14_low_level_tools_plus_3_macros()
    {
        var r = McpCommand.BuildRegistry(".");
        r.All.Count.Should().Be(17);

        string[] expectedLowLevel =
        [
            "aspireform_new", "aspireform_add", "aspireform_config", "aspireform_plan",
            "aspireform_apply", "aspireform_destroy", "aspireform_import",
            "aspireform_state_list", "aspireform_state_show", "aspireform_doctor",
            "aspireform_plugin_list", "aspireform_plugin_install",
            "aspireform_plugin_update", "aspireform_plugin_remove",
        ];
        foreach (var n in expectedLowLevel)
        {
            r.Contains(n).Should().BeTrue(because: $"low-level tool '{n}' must be registered");
        }

        string[] expectedMacros =
        [
            "scaffold_aspire_app_with_data", "add_cache_layer", "add_authentication",
        ];
        foreach (var n in expectedMacros)
        {
            r.Contains(n).Should().BeTrue(because: $"macro tool '{n}' must be registered");
        }
    }
}
