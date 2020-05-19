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


		public string PublicKeyToken { get; set; }
		public Version OriginalAssemblyVersion { get; set; }

		public ConfigGenerator(string targetAssemblyName, Version replacementAssemblyVersion, string dllLocation)
		{
			TargetAssemblyName = targetAssemblyName;
			ReplacementAssemblyVersion = replacementAssemblyVersion;
			DllLocation = dllLocation;
		}

		public ConfigGenerator(string targetAssemblyName, Version originalAssemblyVersion, Version replacementAssemblyVersion, string publicKeyToken, string dllLocation)
		{
			TargetAssemblyName = targetAssemblyName;
			ReplacementAssemblyVersion = replacementAssemblyVersion;
			DllLocation = dllLocation;

			PublicKeyToken = publicKeyToken;
			OriginalAssemblyVersion = originalAssemblyVersion;
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
			var element = new XElement(assemblyNamespace + "assemblyIdentity",
				new XAttribute("name", TargetAssemblyName),
				new XAttribute("culture", "neutral"));

			if (PublicKeyToken != null)
				element.Add(new XAttribute("publicKeyToken", PublicKeyToken));

			return element;
		}

		private XElement GenerateCodeBaseElement()
		{
			return new XElement(assemblyNamespace + "codeBase",
				new XAttribute("version", ReplacementAssemblyVersion.ToString()),
				new XAttribute("href", DllLocation));
		}

		private XElement GenerateBindingRedirectElement()
		{
			return new XElement(assemblyNamespace + "bindingRedirect",
				new XAttribute("oldVersion", OriginalAssemblyVersion.ToString()),
				new XAttribute("newVersion", ReplacementAssemblyVersion.ToString()));
		}

		public string BuildConfig()
		{
			XDocument xmlDocument = new XDocument();

			var bindingElement = new XElement(assemblyNamespace + "assemblyBinding",
				GenerateDependentAssemblyElement());

			if (PublicKeyToken != null)
				bindingElement.Add(GenerateBindingRedirectElement());

			xmlDocument.Add(
				new XElement("configuration",
					new XElement("runtime",
						bindingElement)));

			return xmlDocument.ToString(SaveOptions.None);
		}

		public void BuildAndWriteConfig(string configPath)
		{
			File.WriteAllText(configPath, BuildConfig());
		}
	}
}