using AspireForm.Cli;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpCommandRegistrationTests
{
    [Fact]
    public void BuildRegistry_registers_14_verbs_3_macros_12_entity_tools_total_29()
    {
        var r = McpCommand.BuildRegistry(".");
        r.All.Count.Should().Be(30); // 14 low-level + 12 entity + 3 macros + 1 theme = 30

        string[] expectedLowLevel =
        [
            "aspireform_new", "aspireform_add", "aspireform_config", "aspireform_plan",
            "aspireform_apply", "aspireform_destroy", "aspireform_import",
            "aspireform_state_list", "aspireform_state_show", "aspireform_doctor",
            "aspireform_plugin_list", "aspireform_plugin_install",
            "aspireform_plugin_update", "aspireform_plugin_remove",
        ];
        foreach (var n in expectedLowLevel)
            r.Contains(n).Should().BeTrue(because: $"low-level tool '{n}' must be registered");

        string[] expectedMacros =
        [
            "scaffold_aspire_app_with_data", "add_cache_layer", "add_authentication",
        ];
        foreach (var n in expectedMacros)
            r.Contains(n).Should().BeTrue(because: $"macro '{n}' must be registered");

        string[] expectedEntityTools =
        [
            "aspireform_entity_list", "aspireform_entity_show", "aspireform_dbcontext_list",
            "aspireform_entity_create", "aspireform_entity_delete",
            "aspireform_property_add", "aspireform_property_remove", "aspireform_property_rename",
            "aspireform_attribute_set", "aspireform_attribute_clear",
            "aspireform_relationship_add", "aspireform_relationship_remove",
        ];
        foreach (var n in expectedEntityTools)
            r.Contains(n).Should().BeTrue(because: $"entity tool '{n}' must be registered");
    }
}
