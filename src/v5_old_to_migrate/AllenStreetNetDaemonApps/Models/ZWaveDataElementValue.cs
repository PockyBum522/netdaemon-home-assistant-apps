using System.Text.Json.Serialization;

namespace AllenStreetNetDaemonApps.Models;

public class ZWaveDataElementValue
{
    [JsonPropertyName("domain")]    
    public string Domain { get; set; } = "";
    
    [JsonPropertyName("node_id")]
    public int NodeId { get; set; }
    
    [JsonPropertyName("home_id")]
    public long HomeId { get; set; }
    
    [JsonPropertyName("endpoint")]
    public int Endpoint { get; set; }
    
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "";
    
    [JsonPropertyName("command_class")]
    public int CommandClass { get; set; }

    [JsonPropertyName("command_class_name")]
    public string CommandClassName { get; set; } = "";
    
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
    
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";
    
    [JsonPropertyName("property_name")]
    public string PropertyName { get; set; } = "";
    
    [JsonPropertyName("property_key")]
    public string PropertyKey { get; set; } = "";
    
    [JsonPropertyName("property_key_name")]
    public string PropertyKeyName { get; set; } = "";
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
    
    [JsonPropertyName("value_raw")]
    public int ValueRaw { get; set; }
}
