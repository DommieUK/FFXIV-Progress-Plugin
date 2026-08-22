using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVProgressPlugin.Sync;
using FFXIVProgressPlugin.Trackers;
using FFXIVProgressPlugin.Windows;

namespace FFXIVProgressPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/progresstracker";

    public Configuration Configuration { get; }

    public ContentTrackerRegistry TrackerRegistry { get; }

    public SyncService SyncService { get; }

    public readonly WindowSystem WindowSystem = new("FFXIVProgressPlugin");

    private ConfigWindow ConfigWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        TrackerRegistry = new ContentTrackerRegistry();
        SyncService = new SyncService(Configuration, TrackerRegistry, DataManager, UnlockState, PlayerState, Framework, Log);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the FFXIV Progress Tracker settings window.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        SyncService.Start();

        Log.Information("FFXIV Progress Tracker loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        SyncService.Dispose();
    }

    private void OnCommand(string command, string args) => ConfigWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
