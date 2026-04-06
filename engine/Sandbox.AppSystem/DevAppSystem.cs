namespace Sandbox;

/// <summary>
/// Development app system for Linux: runs the game with hot-reload support
/// but without editor tools (no libtoolframework2.so dependency).
/// Use with -no-editor command line flag or automatically on Linux.
/// </summary>
public class DevAppSystem : AppSystem
{
	public override void Init()
	{
		LoadSteamDll();

		base.Init();

		// Error as early as possible if invalid project
		if ( !CheckProject() )
			return;

		CreateMenu();
		CreateGame();
		// NOTE: CreateEditor() intentionally omitted — avoids editor-native
		// .so dependencies (libtoolframework2.so, libassetsystem.so, etc.)
		// that are not publicly distributed for Linux.

		var createInfo = new AppSystemCreateInfo()
		{
			WindowTitle = "s&box",
			Flags = AppSystemFlags.IsGameApp
		};

		InitGame( createInfo );
	}

	/// <summary>
	/// Checks if a valid -project parameter was passed
	/// </summary>
	protected bool CheckProject()
	{
		// -test special case
		if ( !Utility.CommandLine.HasSwitch( "-project" ) && Utility.CommandLine.HasSwitch( "-test" ) )
			return true;

		var path = Utility.CommandLine.GetSwitch( "-project", "" ).TrimQuoted();

		Project project = new() { ConfigFilePath = path };
		if ( project.LoadMinimal() )
			return true;

		NativeEngine.EngineGlobal.Plat_MessageBox( "Couldn't open project", $"Couldn't open project file: {path}" );
		return false;
	}
}
