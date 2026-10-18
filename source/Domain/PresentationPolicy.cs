namespace MediaBack.Domain;

public enum PanelTrigger { AudioStateChanged, VolumeKey, HardwareVolume, MediaKey, ExplicitPreview, Startup }

public static class PresentationPolicy
{
    public static bool ShouldReveal(PanelTrigger trigger, Settings settings) => trigger switch
    {
        PanelTrigger.AudioStateChanged => false,
        PanelTrigger.VolumeKey => settings.InterceptVolume,
        PanelTrigger.HardwareVolume => settings.HardwareControls,
        PanelTrigger.MediaKey => settings.ShowOnMediaKeys,
        PanelTrigger.Startup => settings.ShowOnStartup,
        _ => true
    };
}
