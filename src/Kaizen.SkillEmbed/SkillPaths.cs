using System.Security.Cryptography;

namespace Kaizen.SkillEmbed;

/// <summary>
/// The rules for a skill's name and for the paths of its files, and the hash both the skill and its stamp use.
/// </summary>
internal static class SkillPaths
{
	/// <summary>The file the installer leaves in a skill's directory to say that it wrote it.</summary>
	public const string StampFileName = ".skill-embed.json";

	private const int MaximumNameLength = 64;

	/// <summary>
	/// True for a name the Agent Skills specification allows: lower case letters, digits and single hyphens, with no
	/// hyphen at either end. The name is a directory's name, so nothing else may reach it.
	/// </summary>
	public static bool IsValidName(string name)
	{
		if (name.Length == 0 || name.Length > MaximumNameLength || name[0] == '-' || name[^1] == '-')
			return false;

		for (var index = 0; index < name.Length; index++)
		{
			var character = name[index];
			var isHyphen = character == '-';
			if (isHyphen && name[index - 1] == '-')
				return false;
			if (!isHyphen && !(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9'))
				return false;
		}

		return true;
	}

	/// <summary>
	/// A file's path inside a skill's directory with forward slashes, or null when the path could leave the directory
	/// (rooted, a drive, a <c>..</c>) or is the stamp's own.
	/// </summary>
	public static string? TryNormalise(string relativePath)
	{
		var segments = relativePath.Replace('\\', '/').Split('/');
		foreach (var segment in segments)
		{
			if (segment.Length == 0 || segment == "." || segment == ".." || segment.Contains(':') || segment.Contains('\0'))
				return null;
		}

		var normalised = string.Join('/', segments);
		return normalised == StampFileName ? null : normalised;
	}

	/// <summary>The path on disk of one of a skill's files.</summary>
	public static string OnDisk(string skillDirectory, string relativePath)
	{
		return Path.Combine(skillDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
	}

	/// <summary>SHA-256 as lower case hex.</summary>
	public static string Hash(ReadOnlySpan<byte> content)
	{
		return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
	}
}
