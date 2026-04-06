using System.Threading.Tasks;

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

	/// <summary>
	/// Loads the -project argument as the active game project, compiles its code,
	/// and launches the game — without requiring any editor tools.
	/// Called from Bootstrap.Init() when no IToolsDll is present.
	/// </summary>
	public override async Task LoadProject()
	{
		if ( Utility.CommandLine.HasSwitch( "-test" ) && !Utility.CommandLine.HasSwitch( "-project" ) )
			return;

		var path = Utility.CommandLine.GetSwitch( "-project", "" ).TrimQuoted();
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			log.Warning( "[DevAppSystem] No -project argument provided, skipping project load." );
			return;
		}

		log.Info( $"[DevAppSystem] Loading project: {path}" );

		// Add the project to the global list and set it as Current
		var project = Project.AddFromFile( path );
		if ( project is null || project.Broken )
		{
			NativeEngine.EngineGlobal.Plat_MessageBox( "Project load failed", $"Failed to load project: {path}" );
			return;
		}

		Project.Current = project;
		Project.Current.LastOpened = System.DateTime.Now;
		project.Active = true;

		// Register the project's assets folder with the native filesystem
		var assetsPath = project.GetAssetsPath();
		if ( System.IO.Directory.Exists( assetsPath ) )
		{
			EngineFileSystem.AddAssetPath( project.Config.FullIdent, assetsPath );
			log.Info( $"[DevAppSystem] Mounted assets: {assetsPath}" );
		}

		// Initialize ProjectSettings filesystem (needed by Input.config etc.)
		var projectSettingsFolder = System.IO.Path.Combine( project.GetRootPath(), "ProjectSettings" );
		EngineFileSystem.InitializeProjectSettingsFolder( projectSettingsFolder );

		// Sync project with the package manager so the mock package is registered
		await Project.SyncWithPackageManager();

		// Compile the project's C# code
		log.Info( "[DevAppSystem] Compiling project code..." );
		var compiled = await Project.CompileAsync();
		if ( !compiled )
		{
			log.Warning( "[DevAppSystem] Compilation failed — launching anyway with last known assembly." );
		}
		else
		{
			log.Info( "[DevAppSystem] Compilation succeeded." );
		}

		// Launch the game
		var ident = project.Package.FullIdent;
		log.Info( $"[DevAppSystem] Launching game package: {ident}" );
		await Engine.IGameInstanceDll.Current.LoadGamePackageAsync( ident, Engine.GameLoadingFlags.Host, default );
	}
}
