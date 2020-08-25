using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NRedirect.Generator
{
	class Program
	{
		private static bool Verbose { get; set; }

		static void LogError(string message)
		{
			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;

			Console.WriteLine(message);

			Console.ForegroundColor = oldColor;
		}

		static void LogMessage(string message)
		{
			Console.WriteLine(message);
		}

		static void LogVerbose(string message)
		{
			if (!Verbose)
				return;

			var oldColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;

			Console.WriteLine(message);

			Console.ForegroundColor = oldColor;
		}

		private static string[] UnusableAssemblies = new[]
		{
			"mscorlib",
			"System",
			"System.Core"
		};

		static void Main(string[] args)
		{
			Arguments arguments = Arguments.Parse(args);

			if (arguments.Values.Count == 0 || arguments.Flag("help"))
			{
				LogMessage("Usage: NDirect.Generator.exe [--verbose] [--help] ( <.NET framework .exe> | --hook [--strip] <.NET assembly to install hook in> )");
				return;
			}

			if (arguments.Flag("verbose"))
				Verbose = true;

			string exeName = arguments.Values[0];

			if (arguments.Flag("hook"))
			{
				string outputPath = Path.Combine(Path.GetDirectoryName(exeName),
					Path.GetFileNameWithoutExtension(exeName) + "-proxy" +
					Path.GetExtension(exeName));

				using var assemblyDefinition = AssemblyDefinition.ReadAssembly(exeName);

				InstallHook(assemblyDefinition, outputPath, arguments.Flag("strip"));

				LogMessage("Finished!");
				return;
			}

			if (!exeName.EndsWith(".exe") || !File.Exists(exeName))
			{
				LogError("Please specify an executable file to generate a proxy for");
				return;
			}

			string exeDirectory = Path.GetDirectoryName(exeName);
			string configName = Path.ChangeExtension(exeName, ".exe.config");

			LogVerbose($"Target executable: {exeName}");
			LogVerbose($"Target executable directory: {exeDirectory}");
			LogVerbose($"Target configuration file: {configName}");
			LogVerbose("");


			using var assembly = AssemblyDefinition.ReadAssembly(exeName);

			var assemblyNamePaths = new Dictionary<AssemblyNameReference, string>();

			foreach (var file in Directory.EnumerateFiles(exeDirectory, "*.dll", SearchOption.AllDirectories))
			{
				try
				{
					if (Path.GetFileNameWithoutExtension(file).EndsWith("-proxy"))
						continue;

					using var refAssembly = AssemblyDefinition.ReadAssembly(file);

					bool isMixedMode = (refAssembly.MainModule.Attributes & ModuleAttributes.ILOnly) == 0;

					if (isMixedMode)
						continue;

					assemblyNamePaths.Add(AssemblyNameReference.Parse(refAssembly.Name.FullName), file);

					LogVerbose($"Found reference: '{refAssembly.Name.FullName}' => {file}");
				}
				catch (Exception ex)
				{
					LogVerbose($"Unable to parse dll: '{file}' => {ex.Message}");
				}
			}

			LogVerbose($"Found {assemblyNamePaths.Count} possible reference assemblies\r\n");

			List<AssemblyNamePath> foundReferences = new List<AssemblyNamePath>();
			var resolver = new DefaultAssemblyResolver();

			foreach (var reference in assembly.MainModule.AssemblyReferences)
			{
				if (UnusableAssemblies.Contains(reference.Name))
					continue;

				if (assemblyNamePaths.TryFind(out var foundPair, x => x.Key.FullName == reference.FullName))
				{
					foundReferences.Add(new AssemblyNamePath(foundPair.Key, foundPair.Value));
					continue;
				}

				AssemblyDefinition resolvedAssembly;

				try
				{
					resolvedAssembly = resolver.Resolve(reference);
				}
				catch (AssemblyResolutionException)
				{
					continue;
				}

				bool isMixedMode = (resolvedAssembly.MainModule.Attributes & ModuleAttributes.ILOnly) == 0;
				string path = resolvedAssembly.MainModule.FileName;

				resolvedAssembly.Dispose();

				if (isMixedMode)
					continue;

				if (resolvedAssembly.Name.HasPublicKey)
					continue;
				
				foundReferences.Add(new AssemblyNamePath(reference, path));
			}

			if (foundReferences.Count == 0)
			{
				LogError("Couldn't find a suitable dll");
				return;
			}

			var foundRef = foundReferences.First();

			LogMessage($"Using '{foundRef.AssemblyLocation}' as library to generate a proxy for");

			string proxyPath = Path.Combine(exeDirectory, foundRef.NameReference.Name + "-proxy.dll");


			using (var originalAssembly = AssemblyDefinition.ReadAssembly(foundRef.AssemblyLocation))
			{
				InstallHook(originalAssembly, proxyPath, true);
			}

			ConfigGenerator generator;

			if (foundRef.NameReference.PublicKeyToken.Length > 0)
			{
				generator = new ConfigGenerator(foundRef.NameReference.Name, foundRef.NameReference.Version, new Version(99, 0, 0),
					Utility.ByteArrayToHexString(foundRef.NameReference.PublicKeyToken), Path.GetFileName(proxyPath));
			}
			else
			{
				generator = new ConfigGenerator(foundRef.NameReference.Name, foundRef.NameReference.Version,
					Path.GetFileName(proxyPath));
			}

			generator.BuildAndWriteConfig(configName);

			LogMessage("Finished!");
		}

		static void InstallHook(AssemblyDefinition assemblyDefinition, string outputPath, bool strip)
		{
			using (var nredirectAssembly = AssemblyDefinition.ReadAssembly("NRedirect.dll"))
			{
				if (strip)
					AssemblyStripper.StripAssembly(assemblyDefinition);

				//if (assemblyDefinition.Name.PublicKeyToken.Length > 0)
				//{
				//	assemblyDefinition.Name.Version = new Version(99, 0, 0);
				//	//assemblyDefinition.Name.PublicKeyToken = new byte[0];
				//}

				var targetMethod = nredirectAssembly.MainModule.GetType("NRedirect.Main").Methods
					.First(x => x.Name == "Start");
				var targetMethodRef = assemblyDefinition.MainModule.ImportReference(targetMethod);

				var moduleType = assemblyDefinition.MainModule.Types.First(x => x.Name == "<Module>");

				var cctorMethod = new MethodDefinition(".cctor",
					MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
					MethodAttributes.HideBySig,
					assemblyDefinition.MainModule.ImportReference(typeof(void)));

				var il = cctorMethod.Body.GetILProcessor();
				il.Emit(OpCodes.Call, targetMethodRef);
				il.Emit(OpCodes.Ret);

				moduleType.Methods.Add(cctorMethod);

				LogMessage($"Writing proxy assembly to '{outputPath}'");

				assemblyDefinition.Write(outputPath);
			}
		}
	}

	public class AssemblyNamePath
	{
		public AssemblyNameReference NameReference { get; set; }
		public string AssemblyLocation { get; set; }

		public AssemblyNamePath(AssemblyNameReference nameReference, string assemblyLocation)
		{
			NameReference = nameReference;
			AssemblyLocation = assemblyLocation;
		}

		public override string ToString()
		{
			return NameReference.ToString();
		}
	}
}