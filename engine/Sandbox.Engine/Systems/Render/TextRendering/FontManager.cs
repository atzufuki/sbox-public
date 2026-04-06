using SkiaSharp;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Topten.RichTextKit;

namespace Sandbox;

file class SKTypefaceEqualityComparer : IEqualityComparer<SKTypeface>
{
	public bool Equals( SKTypeface x, SKTypeface y )
	{
		return x.FamilyName == y.FamilyName
			&& x.FontWeight == y.FontWeight
			&& x.FontSlant == y.FontSlant;
	}

	public int GetHashCode( [DisallowNull] SKTypeface obj )
	{
		return HashCode.Combine( obj.FamilyName, obj.FontWeight, obj.FontSlant );
	}
}

internal class FontManager : FontMapper
{
	public static FontManager Instance = new FontManager();

	static ConcurrentDictionary<int, SKTypeface> LoadedFonts = new();

	static Dictionary<int, SKTypeface> Cache = new();

	public static IEnumerable<string> FontFamilies => LoadedFonts.Values.Select( x => x.FamilyName ).Distinct();

	private void Load( System.IO.Stream stream )
	{
		if ( stream == null ) return;

		var face = SKTypeface.FromStream( stream );
		if ( face is null ) return;

		var hash = HashCode.Combine( face.FamilyName, face.FontWeight, face.FontSlant );

		LoadedFonts[hash] = face;

		Log.Trace( $"Loaded font {face.FamilyName} weight {face.FontWeight}" );
	}

	List<FileWatch> watchers = new();

	public void LoadAll( BaseFileSystem fileSystem )
	{
		// If we're loading new fonts, we may have cached it already
		Cache.Clear();

		var fontFiles = fileSystem.FindFile( "/fonts/", "*.ttf", true )
			.Union( fileSystem.FindFile( "/fonts/", "*.otf", true ) );

		int count = 0;
		Parallel.ForEach( fontFiles, ( string font ) =>
		{
			Load( fileSystem.OpenRead( $"/fonts/{font}" ) );
			System.Threading.Interlocked.Increment( ref count );
		} );

		Log.Info( $"FontManager.LoadAll: loaded {count} fonts from VFS /fonts/" );

		// On Linux without the editor the VFS sub-filesystem for base assets is
		// mounted at the root of FileSystem.Mounted, but FindFile("/fonts/") may
		// still return nothing if the aggregate hasn't resolved paths yet, or if
		// there are no system fonts registered.  Fall back to loading directly
		// from disk so text never crashes with a null typeface stream.
		if ( count == 0 )
		{
			var engineDir = System.IO.Path.GetDirectoryName( typeof( FontManager ).Assembly.Location );
			// Walk up from bin/managed/ to game/ to find addons/base/assets/fonts/
			var gameDir = engineDir;
			for ( int i = 0; i < 3; i++ )
				gameDir = System.IO.Path.GetDirectoryName( gameDir );
			var diskFontsDir = System.IO.Path.Combine( gameDir ?? "", "addons", "base", "assets", "fonts" );
			Log.Info( $"FontManager.LoadAll: VFS found no fonts, trying disk path: {diskFontsDir}" );
			if ( System.IO.Directory.Exists( diskFontsDir ) )
			{
				foreach ( var file in System.IO.Directory.EnumerateFiles( diskFontsDir, "*.ttf" )
					.Union( System.IO.Directory.EnumerateFiles( diskFontsDir, "*.otf" ) ) )
				{
					Load( System.IO.File.OpenRead( file ) );
					count++;
				}
				Log.Info( $"FontManager.LoadAll: loaded {count} fonts from disk fallback" );
			}
		}

		// Load any new fonts
		var ttfWatch = fileSystem.Watch( $"*.ttf" );
		ttfWatch.OnChanges += ( w ) => OnFontFilesChanged( w, fileSystem );
		var otfWatch = fileSystem.Watch( $"*.otf" );
		otfWatch.OnChanges += ( w ) => OnFontFilesChanged( w, fileSystem );

		watchers.Add( ttfWatch );
		watchers.Add( otfWatch );
	}

	private void OnFontFilesChanged( FileWatch w, BaseFileSystem fs )
	{
		Cache.Clear();

		foreach ( var file in w.Changes )
		{
			Load( fs.OpenRead( file ) );
		}
	}

	/// <summary>
	/// Tries to get the best matching font for the given style.
	/// Will return a matching font family with the closest font weight and optionally slant.
	/// </summary>
	private SKTypeface GetBestTypeface( IStyle style )
	{
		// Must be of same family
		var familyFonts = LoadedFonts.Values.Where( x => string.Equals( x.FamilyName, style.FontFamily, StringComparison.OrdinalIgnoreCase ) );
		if ( !familyFonts.Any() ) return null;

		// Get matching slants, if no matching fallback to regular
		var slantFonts = familyFonts.Where( x => x.IsItalic == style.FontItalic );
		if ( slantFonts.Any() ) familyFonts = slantFonts;

		// Finally get the closest font weight
		return familyFonts.Select( x => new { x, distance = Math.Abs( x.FontWeight - style.FontWeight ) } )
			.OrderBy( x => x.distance )
			.First().x;
	}

	public override SKTypeface TypefaceFromStyle( IStyle style, bool ignoreFontVariants )
	{
		var hash = HashCode.Combine( style.FontFamily, style.FontWeight, style.FontItalic );

		lock ( Cache )
		{
			if ( Cache.TryGetValue( hash, out var cachedFace ) ) return cachedFace;
		}

		var f = GetBestTypeface( style );

		// Fallback on system font
		f ??= Default.TypefaceFromStyle( style, ignoreFontVariants );

		// Last resort: use SkiaSharp's built-in default typeface.
		// SKFont(null) throws, so we must never return null from here.
		f ??= SKTypeface.Default;

		lock ( Cache )
		{
			Cache[hash] = f;
		}

		return f;
	}

	public void Reset()
	{
		foreach ( var watcher in watchers )
		{
			watcher.Dispose();
		}
		watchers.Clear();

		foreach ( var (_, font) in LoadedFonts )
		{
			font?.Dispose();
		}

		foreach ( var (_, font) in Cache )
		{
			font?.Dispose();
		}

		LoadedFonts.Clear();
		Cache.Clear();
	}
}

