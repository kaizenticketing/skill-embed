using System.Text.Json.Serialization;

namespace Kaizen.SkillEmbed.Stamps;

/// <summary>
/// Reads and writes a stamp without reflection, so the library works in a Native AOT binary.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SkillStamp))]
internal sealed partial class SkillStampJsonContext : JsonSerializerContext;
