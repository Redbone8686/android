using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public partial class GenerateLibraryResources
	{
		/// <summary>
		/// NOTE: all file paths used in this method should be full paths. (Or use AsyncTask.WorkingDirectory)
		/// </summary>
		void GenerateJava (Package package)
		{
			// In some cases (such as ancient support libraries), R.txt does not exist.
			// We can just use the main app's R.txt file and write *all fields* in this case.
			foreach (var r_txt in package.TextFiles) {
				if (!File.Exists (r_txt)) {
					LogDebugMessage ($"Using main R.txt, R.txt does not exist: {r_txt}");
					package.TextFiles.Clear ();
					package.TextFiles.Add (main_r_txt);
					break;
				}
			}

			var lines = LoadValues (package);
			using (var writer = MemoryStreamPool.Shared.CreateStreamWriter ()) {
				// This code is based on the Android gradle plugin
				// https://android.googlesource.com/platform/tools/base/+/908b391a9c006af569dfaff08b37f8fdd6c4da89/build-system/builder/src/main/java/com/android/builder/internal/SymbolWriter.java

				writer.WriteLine ("/* AUTO-GENERATED FILE. DO NOT MODIFY.");
				writer.WriteLine (" *");
				writer.WriteLine (" * This class was automatically generated by");
				writer.WriteLine (" * .NET for Android from the resource data it found.");
				writer.WriteLine (" * It should not be modified by hand.");
				writer.WriteLine (" */");

				writer.Write ("package ");
				writer.Write (package.Name);
				writer.WriteLine (';');
				writer.WriteLine ();
				writer.WriteLine ("public final class R {");

				string currentClass = null;
				foreach (var line in lines) {
					var type  = line [Index.Type];
					var clazz = line [Index.Class];
					var name  = line [Index.Name];
					var value = line [Index.Value];
					if (clazz != currentClass) {
						// If not the first inner class
						if (currentClass != null) {
							writer.WriteLine ("\t}");
						}

						currentClass = clazz;
						writer.Write ("\tpublic static final class ");
						writer.Write (currentClass);
						writer.WriteLine (" {");
					}

					writer.Write ("\t\tpublic static final ");
					writer.Write (type);
					writer.Write (' ');
					writer.Write (name);
					writer.Write (" = ");
					// It may be an int[]
					if (value.StartsWith ("{", StringComparison.Ordinal)) {
						writer.Write ("new ");
						writer.Write (type);
						writer.Write (' ');
					}
					writer.Write (value);
					writer.WriteLine (';');
				}

				// If we wrote at least one inner class
				if (currentClass != null) {
					writer.WriteLine ("\t}");
				}
				writer.WriteLine ('}');

				writer.Flush ();
				var r_java = Path.Combine (output_directory, package.Name.Replace ('.', Path.DirectorySeparatorChar), "R.java");
				if (Files.CopyIfStreamChanged (writer.BaseStream, r_java)) {
					LogDebugMessage ($"Writing: {r_java}");
				} else {
					LogDebugMessage ($"Up to date: {r_java}");
				}
			}
		}
	}
}
