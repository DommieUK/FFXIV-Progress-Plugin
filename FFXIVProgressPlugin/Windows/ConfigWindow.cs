using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FFXIVProgressPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string workerUrlBuffer;
    private string secretTokenBuffer;
    private string customProfileIdBuffer;
    private int syncIntervalBuffer;

    public ConfigWindow(Plugin plugin)
        : base("FFXIV Progress Tracker Settings###FFXIVProgressPluginConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(440, 520);
        SizeCondition = ImGuiCond.FirstUseEver;

        this.plugin = plugin;

        var config = plugin.Configuration;
        workerUrlBuffer = config.WorkerUrl;
        secretTokenBuffer = config.SecretToken;
        customProfileIdBuffer = config.CustomProfileId;
        syncIntervalBuffer = config.SyncIntervalSeconds;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        ImGui.TextWrapped("Configure where completion snapshots are sent and which content categories to track.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Worker URL", ref workerUrlBuffer, 512))
        {
            config.WorkerUrl = workerUrlBuffer;
            config.Save();
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Secret Token", ref secretTokenBuffer, 256, ImGuiInputTextFlags.Password))
        {
            config.SecretToken = secretTokenBuffer;
            config.Save();
        }

        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Sync Interval (seconds)", ref syncIntervalBuffer))
        {
            if (syncIntervalBuffer < 10)
                syncIntervalBuffer = 10;
            config.SyncIntervalSeconds = syncIntervalBuffer;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Character Identifier");

        if (ImGui.RadioButton("Character name + world", config.IdentifierMode == CharacterIdentifierMode.CharacterNameAndWorld))
        {
            config.IdentifierMode = CharacterIdentifierMode.CharacterNameAndWorld;
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Custom profile ID", config.IdentifierMode == CharacterIdentifierMode.CustomProfileId))
        {
            config.IdentifierMode = CharacterIdentifierMode.CustomProfileId;
            config.Save();
        }

        if (config.IdentifierMode == CharacterIdentifierMode.CustomProfileId)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Profile ID", ref customProfileIdBuffer, 128))
            {
                config.CustomProfileId = customProfileIdBuffer;
                config.Save();
            }
        }
        else
        {
            ImGui.TextDisabled("Your character's name and home world will be sent as the identifier.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Tracked Categories");

        foreach (var tracker in plugin.TrackerRegistry.All)
        {
            var enabled = plugin.TrackerRegistry.IsEnabled(config, tracker);
            if (ImGui.Checkbox(tracker.DisplayName, ref enabled))
            {
                config.EnabledCategories[tracker.CategoryId] = enabled;
                config.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        var syncing = plugin.SyncService.IsSyncing;
        if (syncing)
            ImGui.BeginDisabled();

        if (ImGui.Button("Sync Now"))
            plugin.SyncService.TriggerSyncNow();

        if (syncing)
            ImGui.EndDisabled();

        if (syncing)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("Syncing...");
        }

        var lastSuccess = plugin.SyncService.LastSuccessUtc;
        var lastError = plugin.SyncService.LastError;

        if (lastError != null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"Last error: {lastError}");
        }
        else if (lastSuccess != null)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"Last synced: {lastSuccess.Value.ToLocalTime():G}");
        }
        else
        {
            ImGui.TextDisabled("Not synced yet.");
        }
    }
}
