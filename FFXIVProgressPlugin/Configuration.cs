using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace FFXIVProgressPlugin;

public enum CharacterIdentifierMode
{
    CharacterNameAndWorld,
    CustomProfileId,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string WorkerUrl { get; set; } = string.Empty;

    public string SecretToken { get; set; } = string.Empty;

    public int SyncIntervalSeconds { get; set; } = 60;

    public CharacterIdentifierMode IdentifierMode { get; set; } = CharacterIdentifierMode.CharacterNameAndWorld;

    public string CustomProfileId { get; set; } = string.Empty;

    // Keyed by IContentTracker.CategoryId. Absence of a key defaults to that tracker's DefaultEnabled value.
    public Dictionary<string, bool> EnabledCategories { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
