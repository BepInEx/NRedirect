using System;
using System.IO;
using System.Xml.Linq;

namespace NRedirect.Generator
{
	public class ConfigGenerator
	{
		public string TargetAssemblyName { get; set; }

		public Version ReplacementAssemblyVersion { get; set; }

		public string DllLocation { get; set; }

		public ConfigGenerator(string targetAssemblyName, Version replacementAssemblyVersion, string dllLocation)
		{
			TargetAssemblyName = targetAssemblyName;
			ReplacementAssemblyVersion = replacementAssemblyVersion;
			DllLocation = dllLocation;
		}

		private static XNamespace assemblyNamespace = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");

		private XElement GenerateDependentAssemblyElement()
		{
			return new XElement(assemblyNamespace + "dependentAssembly",
				GenerateIdentityElement(),
				GenerateCodeBaseElement());
		}

		private XElement GenerateIdentityElement()
		{
			return new XElement(assemblyNamespace + "assemblyIdentity",
				new XAttribute("name", TargetAssemblyName),
				new XAttribute("culture", "neutral"));
		}

		private XElement GenerateCodeBaseElement()
		{
			return new XElement(assemblyNamespace + "codeBase",
				new XAttribute("version", ReplacementAssemblyVersion.ToString()),
				new XAttribute("href", DllLocation));
		}

		public string BuildConfig()
		{
			XDocument xmlDocument = new XDocument();


			xmlDocument.Add(
				new XElement("configuration",
					new XElement("runtime",
						new XElement(assemblyNamespace + "assemblyBinding",
							GenerateDependentAssemblyElement()))));

			return xmlDocument.ToString(SaveOptions.None);
		}

		public void BuildAndWriteConfig(string configPath)
		{
			File.WriteAllText(configPath, BuildConfig());
		}
	}
}