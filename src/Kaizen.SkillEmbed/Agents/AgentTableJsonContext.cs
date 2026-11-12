using System.Text.Json.Serialization;
using Kaizen.SkillEmbed.Agents.Models;

namespace Kaizen.SkillEmbed.Agents;

/// <summary>
/// Reads agents.json without reflection, so the library works in a Native AOT binary.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AgentTable))]
internal sealed partial class AgentTableJsonContext : JsonSerializerContext;
