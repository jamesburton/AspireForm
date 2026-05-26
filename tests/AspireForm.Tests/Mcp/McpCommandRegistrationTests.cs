using AspireForm.Cli;
using AwesomeAssertions;
using Xunit;

namespace AspireForm.Tests.Mcp;

public sealed class McpCommandRegistrationTests
{
    [Fact]
    public void BuildRegistry_registers_14_verbs_3_macros_12_entity_tools_10_endpoint_tools_1_theme_tool_total_40()
    {
        var r = McpCommand.BuildRegistry(".");
        r.All.Count.Should().Be(40); // 14 low-level + 12 entity + 3 macros + 10 endpoint + 1 theme = 40

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

        string[] expectedEndpointTools =
        [
            "aspireform_endpoint_list", "aspireform_endpoint_show", "aspireform_endpoint_emit",
            "aspireform_endpoint_create", "aspireform_endpoint_delete",
            "aspireform_endpoint_parameter_add", "aspireform_endpoint_parameter_remove",
            "aspireform_endpoint_auth_set",
            "aspireform_endpoint_attribute_set", "aspireform_endpoint_attribute_clear",
        ];
        foreach (var n in expectedEndpointTools)
            r.Contains(n).Should().BeTrue(because: $"endpoint tool '{n}' must be registered");

        r.Contains("aspireform_theme_show").Should().BeTrue(because: "theme tool must be registered");
    }
}
