using Newtonsoft.Json;

namespace AllenStreetNetDaemonApps.Models;

public class Attributes
{
    [JsonProperty("device_class")]
    public string DeviceClass { get; set; } = "";

    [JsonProperty("friendly_name")]
    public string FriendlyName { get; set; } = "";
}

public class Context
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("parent_id")]
    public object ParentId { get; set; } = "";

    [JsonProperty("user_id")]
    public object UserId { get; set; } = "";
}

public class NewState
{
    [JsonProperty("entity_id")]
    public string EntityId { get; set; } = "";

    [JsonProperty("state")]
    public string State { get; set; } = "";

    [JsonProperty("attributes")]
    public Attributes? Attributes { get; set; }

    [JsonProperty("last_changed")]
    public DateTime LastChanged { get; set; }

    [JsonProperty("last_reported")]
    public DateTime LastReported { get; set; }

    [JsonProperty("last_updated")]
    public DateTime LastUpdated { get; set; }

    [JsonProperty("context")]
    public Context? Context { get; set; }
}

public class OldState
{
    [JsonProperty("entity_id")]
    public string EntityId { get; set; } = "";

    [JsonProperty("state")]
    public string State { get; set; } = "";

    [JsonProperty("attributes")]
    public Attributes? Attributes { get; set; }

    [JsonProperty("last_changed")]
    public DateTime LastChanged { get; set; }

    [JsonProperty("last_reported")]
    public DateTime LastReported { get; set; }

    [JsonProperty("last_updated")]
    public DateTime LastUpdated { get; set; }

    [JsonProperty("context")]
    public Context? Context { get; set; }
}

public class MotionEventValue
{
    [JsonProperty("entity_id")]
    public string EntityId { get; set; } = "";

    [JsonProperty("old_state")]
    public OldState? OldState { get; set; }

    [JsonProperty("new_state")]
    public NewState? NewState { get; set; }
}