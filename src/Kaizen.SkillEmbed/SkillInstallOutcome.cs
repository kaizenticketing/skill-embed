namespace Kaizen.SkillEmbed;

/// <summary>
/// What an install, a refresh or a remove did to one directory.
/// </summary>
public enum SkillInstallOutcome
{
	/// <summary>The skill was not there, and now it is.</summary>
	Installed,

	/// <summary>The skill was there, and now it is the one the program carries.</summary>
	Updated,

	/// <summary>The skill was already the one the program carries. Nothing was written.</summary>
	UpToDate,

	/// <summary>The skill was removed.</summary>
	Removed,

	/// <summary>The skill is not there, and was not put there: a refresh or a remove found nothing.</summary>
	NotInstalled,

	/// <summary>A file has been changed since the installer wrote it, so the directory was left alone. <c>force</c> overrides this.</summary>
	SkippedModified,

	/// <summary>Something this program's installer did not write has the skill's name, so it was left alone. Nothing overrides this.</summary>
	SkippedForeign,

	/// <summary>The file system refused - <see cref="SkillInstallResult.Error"/> says how.</summary>
	Failed,
}
