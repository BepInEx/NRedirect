using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NRedirect.Generator
{
	public static class AssemblyStripper
	{
		static void ClearMethodBodies(AssemblyDefinition assembly)
		{
			foreach (TypeDefinition type in assembly.MainModule.Types)
			{
				ClearMethodBodies(type.Methods);
			}
		}

		static void ClearMethodBodies(ICollection<MethodDefinition> methods)
		{
			foreach (MethodDefinition method in methods)
			{
				if (!method.HasBody)
					continue;

				MethodBody body = new MethodBody(method);
				body.GetILProcessor().Emit(OpCodes.Ret);

				method.Body = body;

				method.AggressiveInlining = false;
				method.NoInlining = true;
			}
		}

		public static void StripAssembly(AssemblyDefinition assembly)
		{
			ClearMethodBodies(assembly);

			assembly.MainModule.Resources.Clear();
		}
	}
}