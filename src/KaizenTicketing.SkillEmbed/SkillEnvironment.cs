namespace KaizenTicketing.SkillEmbed;

/// <summary>
/// The machine an installer works on: the user's home directory, the operating system and the environment variables.
/// <see cref="Current"/> is the real one; a test makes its own over a temporary directory.
/// </summary>
public sealed class SkillEnvironment
{
	private readonly Func<string, string?> getVariable;

	/// <summary>Makes an environment.</summary>
	/// <param name="homeDirectory">The user's home directory, which <c>{home}</c> in an agent's paths stands for.</param>
	/// <param name="platform">The operating system.</param>
	/// <param name="getVariable">Reads an environment variable; null when it is not set.</param>
	public SkillEnvironment(string homeDirectory, SkillPlatform platform, Func<string, string?> getVariable)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
		ArgumentNullException.ThrowIfNull(getVariable);

		HomeDirectory = Path.GetFullPath(homeDirectory);
		Platform = platform;
		this.getVariable = getVariable;
	}

	/// <summary>The environment of the current user and process.</summary>
	/// <exception cref="InvalidOperationException">The user has no home directory.</exception>
	public static SkillEnvironment Current
	{
		get
		{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (string.IsNullOrWhiteSpace(home))
				throw new InvalidOperationException("The current user has no home directory.");

			return new SkillEnvironment(home, CurrentPlatform(), Environment.GetEnvironmentVariable);
		}
	}

	/// <summary>The user's home directory, as a full path.</summary>
	public string HomeDirectory { get; }

	/// <summary>The operating system.</summary>
	public SkillPlatform Platform { get; }

	/// <summary>An environment variable's value without the white space around it, or null when it is not set or empty.</summary>
	public string? GetVariable(string name)
	{
		var value = getVariable(name);
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	#region Internal Implementation

	private static SkillPlatform CurrentPlatform()
	{
		if (OperatingSystem.IsWindows())
			return SkillPlatform.Windows;

		return OperatingSystem.IsMacOS() ? SkillPlatform.MacOS : SkillPlatform.Linux;
	}

	#endregion
}
