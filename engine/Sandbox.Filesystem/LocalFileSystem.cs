namespace Sandbox;

/// <summary>
/// A directory on a disk
/// </summary>
internal class LocalFileSystem : BaseFileSystem
{
	Zio.FileSystems.PhysicalFileSystem Physical { get; }

	internal LocalFileSystem( string rootFolder, bool makereadonly = false )
	{
		Physical = new Zio.FileSystems.PhysicalFileSystem();

		// On Linux, paths are case-sensitive — do NOT lowercase them.
		var normalizedFolder = OperatingSystem.IsLinux() ? rootFolder : rootFolder.ToLowerInvariant();
		var rootPath = Physical.ConvertPathFromInternal( normalizedFolder );
		system = new Zio.FileSystems.SubFileSystem( Physical, rootPath );

		if ( makereadonly )
		{
			system = new Zio.FileSystems.ReadOnlyFileSystem( system );
		}
	}

	internal override void Dispose()
	{
		base.Dispose();

		Physical?.Dispose();
	}
}
