using System.Text;

namespace Kaizen.SkillEmbed.Agents;

/// <summary>
/// A path as the agent table writes it: forward slashes, with <c>{home}</c> for the user's home directory and
/// <c>{env:NAME}</c> for an environment variable.
/// </summary>
internal static class PathTemplate
{
	private const string HomeToken = "{home}";
	private const string VariablePrefix = "{env:";

	/// <summary>The template as a full path, or null when it names an environment variable that is not set.</summary>
	public static string? Resolve(string template, SkillEnvironment environment)
	{
		var resolved = new StringBuilder();
		var position = 0;
		while (position < template.Length)
		{
			var open = template.IndexOf('{', position);
			if (open < 0)
			{
				resolved.Append(template, position, template.Length - position);
				break;
			}

			resolved.Append(template, position, open - position);
			var close = template.IndexOf('}', open);
			if (close < 0)
				throw new FormatException($"The path '{template}' has a '{{' with no '}}'.");

			var token = template.Substring(open, close - open + 1);
			if (token == HomeToken)
			{
				resolved.Append(environment.HomeDirectory);
			}
			else if (token.StartsWith(VariablePrefix, StringComparison.Ordinal))
			{
				var value = environment.GetVariable(token.Substring(VariablePrefix.Length, token.Length - VariablePrefix.Length - 1));
				if (value == null)
					return null;

				resolved.Append(value);
			}
			else
			{
				throw new FormatException($"The path '{template}' has the token '{token}'. A path knows {HomeToken} and {VariablePrefix}NAME}}.");
			}

			position = close + 1;
		}

		return Path.GetFullPath(resolved.Replace('/', Path.DirectorySeparatorChar).ToString());
	}

	/// <summary>The first alternative that resolves - the table puts the ones that need a variable first.</summary>
	public static string? ResolveFirst(IReadOnlyList<string> alternatives, SkillEnvironment environment)
	{
		foreach (var alternative in alternatives)
		{
			var path = Resolve(alternative, environment);
			if (path != null)
				return path;
		}

		return null;
	}
}
