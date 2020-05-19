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
			if (args.Length == 0 || args.Any(x => x == "--help"))
			{
				LogMessage("Usage: NDirect.Generator.exe <.NET framework .exe> [--verbose] [--help]");
				return;
			}

			if (!args[0].EndsWith(".exe") || !File.Exists(args[0]))
			{
				LogError("Please specify an executable file to generate a proxy for");
				return;
			}

			if (args.Any(x => x == "--verbose"))
				Verbose = true;

			string exeName = args[0];
			string exeDirectory = Path.GetDirectoryName(args[0]);
			string configName = Path.ChangeExtension(args[0], ".exe.config");

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

				foundReferences.Add(new AssemblyNamePath(reference, path));
			}

			if (foundReferences.Count == 0)
			{
				LogError("Couldn't find a suitable dll");
				return;
			}

			var foundRef = foundReferences.OrderByDescending(x => x.AssemblyLocation != null ? 1 : 0).First();

			LogMessage($"Using '{foundRef.AssemblyLocation}' as library to generate a proxy for");

			string proxyPath;
			AssemblyDefinition assemblyDefinition;

			if (foundRef.AssemblyLocation != null)
			{
				proxyPath = Path.Combine(exeDirectory, foundRef.NameReference.Name + "-proxy.dll");

				assemblyDefinition = AssemblyDefinition.ReadAssembly(foundRef.AssemblyLocation);
			}
			else
			{
				proxyPath = Path.Combine(exeDirectory, foundRef.NameReference.Name + "-proxy.dll");
				assemblyDefinition = resolver.Resolve(foundRef.NameReference);
			}


			using (var nredirectAssembly = AssemblyDefinition.ReadAssembly("NRedirect.dll"))
			using (assemblyDefinition)
			{
				AssemblyStripper.StripAssembly(assemblyDefinition);

				if (assemblyDefinition.Name.PublicKeyToken.Length > 0)
				{
					assemblyDefinition.Name.Version = new Version(99, 0, 0);
					//assemblyDefinition.Name.PublicKeyToken = new byte[0];
				}

				var targetMethod = nredirectAssembly.MainModule.GetType("NRedirect.Main").Methods.First(x => x.Name == "Start");
				var targetMethodRef = assemblyDefinition.MainModule.ImportReference(targetMethod);

				var moduleType = assemblyDefinition.MainModule.Types.First(x => x.Name == "<Module>");

				var cctorMethod = new MethodDefinition(".cctor",
					MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
					assemblyDefinition.MainModule.ImportReference(typeof(void)));

				var il = cctorMethod.Body.GetILProcessor();
				il.Emit(OpCodes.Call, targetMethodRef);
				il.Emit(OpCodes.Ret);

				moduleType.Methods.Add(cctorMethod);

				LogMessage($"Writing proxy file to '{proxyPath}'");

				assemblyDefinition.Write(proxyPath);
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