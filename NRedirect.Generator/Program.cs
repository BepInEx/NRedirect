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

					assemblyNamePaths.Add(AssemblyNameReference.Parse(refAssembly.Name.FullName), file);

					LogVerbose($"Found reference: '{refAssembly.Name.FullName}' => {file}");
				}
				catch (Exception ex)
				{
					LogVerbose($"Unable to parse dll: '{file}' => {ex.Message}");
				}
			}

			LogVerbose($"Found {assemblyNamePaths.Count} possible reference assemblies\r\n");

			AssemblyNamePath foundRef = null;

			foreach (var reference in assembly.MainModule.AssemblyReferences)
			{
				if (reference.PublicKeyToken.Length != 0)
					continue;

				if (!assemblyNamePaths.TryFind(out var foundPair, x => x.Key.FullName == reference.FullName))
					continue;

				foundRef = new AssemblyNamePath(foundPair.Key, foundPair.Value);
				break;
			}

			if (foundRef == null)
			{
				LogError("Couldn't find a suitable dll");
				return;
			}

			LogMessage($"Using '{foundRef.AssemblyLocation}' as library to generate a proxy for");


			string proxyPath = Path.Combine(Path.GetDirectoryName(foundRef.AssemblyLocation),
				Path.GetFileNameWithoutExtension(foundRef.AssemblyLocation) + "-proxy" +
				Path.GetExtension(foundRef.AssemblyLocation));

			using (var nredirectAssembly = AssemblyDefinition.ReadAssembly("NRedirect.dll"))
			using (var tempAssembly = AssemblyDefinition.ReadAssembly(foundRef.AssemblyLocation))
			{
				AssemblyStripper.StripAssembly(tempAssembly);


				var targetMethod = nredirectAssembly.MainModule.GetType("NRedirect.Main").Methods.First(x => x.Name == "Start");
				var targetMethodRef = tempAssembly.MainModule.ImportReference(targetMethod);

				var moduleType = tempAssembly.MainModule.Types.First(x => x.Name == "<Module>");

				var cctorMethod = new MethodDefinition(".cctor",
					MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
					tempAssembly.MainModule.ImportReference(typeof(void)));

				var il = cctorMethod.Body.GetILProcessor();
				il.Emit(OpCodes.Call, targetMethodRef);
				il.Emit(OpCodes.Ret);

				moduleType.Methods.Add(cctorMethod);

				LogMessage($"Writing proxy file to '{proxyPath}'");

				tempAssembly.Write(proxyPath);
			}

			var generator = new ConfigGenerator(foundRef.NameReference.Name, foundRef.NameReference.Version,
				Path.GetFileName(proxyPath));

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
	}
}