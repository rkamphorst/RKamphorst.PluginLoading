namespace RKamphorst.PluginLoading.Contract;

public readonly struct Plugin
{
    public Type Implementation { get; init; }
    public Type[] Services { get; init; }
    
}