using Sandbox.Engine;
using System;
using System.Diagnostics;
using System.Linq;

namespace Sandbox;

public static class Launcher
{
	public static int Main()
	{
		if ( HasCommandLineSwitch( "-generatesolution" ) )
		{
			NetCore.InitializeInterop( Environment.CurrentDirectory );
			Bootstrap.InitMinimal( Environment.CurrentDirectory );
			Project.InitializeBuiltIn( false ).GetAwaiter().GetResult();
			Project.GenerateSolution().GetAwaiter().GetResult();
			Managed.SandboxEngine.NativeInterop.Free();
			EngineFileSystem.Shutdown();
			return 0;
		}

		if ( !HasCommandLineSwitch( "-project" ) && !HasCommandLineSwitch( "-test" ) )
		{
			if ( OperatingSystem.IsWindows() )
			{
				// we pass the command line, so we can pass it on to the sbox-launcher (for -game etc)
				ProcessStartInfo info = new ProcessStartInfo( "sbox-launcher.exe", Environment.CommandLine );
				info.UseShellExecute = true;
				info.CreateNoWindow = true;
				info.WorkingDirectory = System.Environment.CurrentDirectory;

				Process.Start( info );
			}

			return 0;
		}

		// On Linux, editor-native libraries (libtoolframework2.so etc.) are not publicly
		// distributed. Use DevAppSystem which skips CreateEditor() to avoid the dependency.
		// The -no-editor flag forces this mode on any platform.
		var noEditor = HasCommandLineSwitch( "-no-editor" ) || OperatingSystem.IsLinux();
		AppSystem appSystem = noEditor ? new DevAppSystem() : new EditorAppSystem();
		appSystem.Run();

		return 0;
	}

	private static bool HasCommandLineSwitch( string switchName )
	{
		return Environment.GetCommandLineArgs().Any( arg => arg.Equals( switchName, StringComparison.OrdinalIgnoreCase ) );
	}
}
