using FluentAssertions;
using RKamphorst.PluginLoading.Test.PluginA;
using Xunit;

namespace RKamphorst.PluginConfiguration.Test;

public class PluginOptionsShould
{

    [Fact]
    public void LoadOptionsFromTheFolderTheAssemblyIsIn()
    {
        var options = new PluginOptions<Options>();

        var value = options.Value;
        value.Should().NotBeNull();
        value!.StringOption.Should().Be("string");
        value!.IntOption.Should().Be(42);
        value!.DictionaryOfOptions.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["a"] = 1, 
            ["b"] = 2
        });
    }
    
}