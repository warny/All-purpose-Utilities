using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.Loader;
using System.Xml.Xsl;

namespace Utils.Reflection.Reflection.Emit
{
	static class EmitDllMappableClass
	{
		private static Dictionary<Type, Type> emittedLibraries = new Dictionary<Type, Type>();

		public static T Emit<T>(CallingConvention callingConvention) => (T)Emit(typeof(T));

		public static object Emit(Type type)
		{
			if (!type.IsInterface) throw new NotSupportedException($"{type.Name} n'est pas une interface");
			if (!emittedLibraries.TryGetValue(type, out Type t))
			{

				var returnedClassName = type.Name.StartsWith("I", StringComparison.InvariantCultureIgnoreCase) ? type.Name.Substring(1, type.Name.Length - 1) : "C" + type.Name;

				StringBuilder csCode = new StringBuilder();
				csCode.AppendLine("namespace dllMapperClassses {");
				csCode.AppendLine("[System.Runtime.CompilerServices.CompilerGenerated]");
				csCode.AppendLine($"\tpublic class {returnedClassName} : {typeof(DllMapper).FullName}, {type.FullName}");
				csCode.AppendLine("\t{");

				foreach (var method in type.GetMethods())
				{
					string delegateClassName = csCode.WriteDelegateClass(method);
					string delegateFieldName = csCode.WriteDelegateField(method, delegateClassName);
					csCode.WriteFunctionCall(method, delegateFieldName);
				}

				csCode.AppendLine("\t}");
				csCode.AppendLine("}");

				Assembly assembly = Compile(type, csCode);
				t = assembly.GetTypes().FirstOrDefault(it => it.Name == returnedClassName);
				emittedLibraries.Add(type, t);
			}
			return Activator.CreateInstance(t);			
		}

		private static string WriteDelegateClass(this StringBuilder csCode, MethodInfo methodInfo)
		{
			string delegateName = methodInfo.Name + "Delegate";
			csCode.Append($"\t\tprivate delegate {methodInfo.ReturnType.FullName} {delegateName} ");
			csCode.WriteFunctionParameters(methodInfo, true);
			csCode.AppendLine(";");

			return delegateName;
		}

		private static void WriteFunctionParameters(this StringBuilder csCode, MethodInfo methodInfo, bool declaration)
		{
			csCode.Append(" ( ");
			foreach (var parameter in methodInfo.GetParameters())
			{
				if (parameter.Position > 0) csCode.Append(", ");
				if (parameter.ParameterType.IsByRef)
				{
					if (parameter.IsOut)
						csCode.Append("out ");
					else
						csCode.Append("ref ");
				}
				if (declaration)
				{
					if (parameter.GetCustomAttribute<InAttribute>() != null) csCode.Append($"[{typeof(InAttribute).FullName}]");
					if (parameter.GetCustomAttribute<OutAttribute>() != null) csCode.Append($"[{typeof(OutAttribute).FullName}]");
					csCode.Append(parameter.ParameterType.FullName.TrimEnd('&'));
					csCode.Append(" ");
				}
				csCode.Append(parameter.Name);
			}
			csCode.Append(" )");
		}

		private static string WriteDelegateField(this StringBuilder csCode, MethodInfo methodInfo, string delegateClassName)
		{
			var externalAttribute = methodInfo.GetCustomAttribute<ExternalAttribute>(true);
			if (externalAttribute == null)
			{
				csCode.AppendLine($"\t\t[{typeof(ExternalAttribute).FullName}(\"{methodInfo.Name}\")]");
			}
			else
			{
				csCode.AppendLine($"\t\t[{typeof(ExternalAttribute).FullName}(\"{externalAttribute.Name ?? methodInfo.Name}\")]");
			}
			string delegateFieldName = "__" + methodInfo.Name;
			csCode.AppendLine($"\t\tprivate {delegateClassName} {delegateFieldName};");
			return delegateFieldName;
		}

		private static string WriteFunctionCall(this StringBuilder csCode, MethodInfo methodInfo, string delegateFieldName)
		{

			csCode.Append("public ");
			csCode.Append(methodInfo.ReturnType.FullName);
			csCode.Append(" ");
			csCode.Append(methodInfo.Name);
			csCode.WriteFunctionParameters(methodInfo, true);
			csCode.Append(" => ");
			csCode.Append(delegateFieldName);
			csCode.WriteFunctionParameters(methodInfo, false);
			csCode.AppendLine(";");

			return methodInfo.Name;

		}

		private static Assembly Compile(Type type, StringBuilder csCode)
		{
			var compiledCode = GenerateCode(csCode.ToString(), type);
			MemoryStream output = new MemoryStream();

			var result = compiledCode.Emit(output);
			if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
			output.Position = 0;
			var assembly = AssemblyLoadContext.Default.LoadFromStream(output);
			return assembly;
		}

		private static CSharpCompilation GenerateCode(string sourceCode, Type type)
		{
			var codeString = SourceText.From(sourceCode);
			var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp7_3);

			var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

			Assembly System_Runtime = Assembly.Load("System.Runtime");
			Assembly NetStandard = Assembly.Load("netstandard");
			var references = new MetadataReference[]
			{
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(type.Assembly.Location),
				MetadataReference.CreateFromFile(typeof(DllMapper).Assembly.Location),
				MetadataReference.CreateFromFile(System_Runtime.Location),
				MetadataReference.CreateFromFile(NetStandard.Location)
			};

			return CSharpCompilation.Create(type.Name + ".dll",
				new[] { parsedSyntaxTree },
					references: references,
					options: new CSharpCompilationOptions(
						OutputKind.DynamicallyLinkedLibrary,
						optimizationLevel: OptimizationLevel.Release,
						assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default
					)
				);
		}


	}
}
