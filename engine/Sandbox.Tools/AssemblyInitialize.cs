global using Editor;
global using Sandbox;
global using Sandbox.Diagnostics;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using static Sandbox.Internal.GlobalGameNamespace;
global using static Sandbox.Internal.GlobalToolsNamespace;
using Sandbox.Engine;
using Sandbox.Internal;
using System.Reflection;



namespace Editor;

/// <summary>
/// Called before anything else. The only purpose of this is to load the native dlls
/// and swap function pointers with them. We should not be doing anything else here.
/// </summary>
internal static class AssemblyInitialize
{
	public static void Initialize()
	{
		// On Linux the editor-native .so files (libtoolframework2.so etc.) are not
		// publicly distributed. Skip native initialisation entirely; the editor will
		// simply not be available, which is expected when running with -no-editor.
		if ( System.OperatingSystem.IsLinux() )
			return;

		Managed.SourceTools.NativeInterop.Initialize();
		Managed.SourceAssetSytem.NativeInterop.Initialize();
		Managed.SourceHammer.NativeInterop.Initialize();
		Managed.SourceModelDoc.NativeInterop.Initialize();
		Managed.SourceAnimgraph.NativeInterop.Initialize();

		IToolsDll.Current = new ToolsDll();
	}

	public static void InitializeUnitTest( System.Reflection.Assembly callingAssembly )
	{
		Initialize();

		var callerName = callingAssembly.GetName().Name;

		//
		// Set up TypeLibrary with data from our base assembly and game assemblies
		//

		Game.TypeLibrary = new TypeLibrary();
		Game.TypeLibrary.AddIntrinsicTypes();
		Game.TypeLibrary.AddAssembly( Assembly.Load( "Sandbox.System" ), false );
		Game.TypeLibrary.AddAssembly( Assembly.Load( "Sandbox.Engine" ), false );

		try
		{
			var gameDll = callerName.Replace( ".unittest", "" );
			var gameAssembly = Assembly.Load( gameDll );
			if ( gameAssembly is null ) System.Console.Error.Write( $"Couldn't find [{gameAssembly}.dll]" );

			Game.TypeLibrary.AddAssembly( Assembly.Load( "Base Library" ), true );
			Game.TypeLibrary.AddAssembly( Assembly.Load( gameDll ), true );
		}
		catch ( System.Exception )
		{
			// ignore - we can only load these dlls in unit tests in addon sln
		}

		Json.Initialize();
	}
}
