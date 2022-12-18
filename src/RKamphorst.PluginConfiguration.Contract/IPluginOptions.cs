namespace RKamphorst.PluginConfiguration.Contract;

public interface IPluginOptions<out TOptions>
{
    public TOptions? Value { get; }
}