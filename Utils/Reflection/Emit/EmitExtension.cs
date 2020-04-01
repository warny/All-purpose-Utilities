using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Utils.Reflection.Emit
{
	public static class EmitExtension
	{
		public static TypeBuilder DefineDelegate(this TypeBuilder moduleBuilder, MethodInfo methodInfo)
		{
			var delegateClass = moduleBuilder.DefineNestedType(
				methodInfo.Name + "Delegate",
				TypeAttributes.NestedPrivate | TypeAttributes.AnsiClass | TypeAttributes.Sealed,
				typeof(System.MulticastDelegate)
			);
			return InnerDefineDelegate(delegateClass, methodInfo);
		}

		public static TypeBuilder DefineDelegate(this ModuleBuilder moduleBuilder, MethodInfo methodInfo)
		{
			var delegateClass = moduleBuilder.DefineType(
				methodInfo.Name + "Delegate",
				TypeAttributes.NestedPrivate | TypeAttributes.AnsiClass | TypeAttributes.Sealed,
				typeof(System.MulticastDelegate)
			);
			return InnerDefineDelegate(delegateClass, methodInfo);
		}

		private static TypeBuilder InnerDefineDelegate(TypeBuilder delegateClass, MethodInfo methodInfo)
		{
			var delegateConstructor = delegateClass.DefineConstructor(
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				CallingConventions.HasThis,
				new[] { typeof(object), typeof(int) }
			);
			delegateConstructor.DefineParameter(0, ParameterAttributes.None, "object");
			delegateConstructor.DefineParameter(1, ParameterAttributes.None, "method");

			var parameters = methodInfo.GetParameters();
			var delegateInvoke = delegateClass.DefineMethod(
				"Invoke", MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
				CallingConventions.ExplicitThis | CallingConventions.HasThis,
				methodInfo.ReturnType,
				null,
				null,
				parameters.Select(p => p.ParameterType).ToArray(),
				parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
				parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
			);
			foreach (var parameterInfo in parameters)
			{
				delegateInvoke.DefineParameter(parameterInfo.Position, parameterInfo.Attributes, parameterInfo.Name);
			}

			var delegateBeginInvoke = delegateClass.DefineMethod(
				"BeginInvoke", MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
				CallingConventions.ExplicitThis | CallingConventions.HasThis,
				typeof(IAsyncResult),
				null,
				null,
				parameters.Where(p => !p.IsOut).Select(p => p.ParameterType)
				.Concat(new Type[] {
					typeof(AsyncCallback),
					typeof(object)
				})
				.ToArray(),
				parameters.Where(p => !p.IsOut).Select(p => p.GetRequiredCustomModifiers())
				.Concat(new Type[][] {
					new Type[] { },
					new Type[] { } })
				.ToArray(),
				parameters.Where(p => !p.IsOut).Select(p => p.GetOptionalCustomModifiers())
				.Concat(new Type[][] {
					new Type[] { },
					new Type[] { } })
				.ToArray()
			);
			foreach (var parameterInfo in
				parameters.Where(p => !p.IsOut).Select(p => (p.Attributes, p.Name))
				.Concat(
					new (ParameterAttributes Attributes, string Name)[] {
						(ParameterAttributes.None, "callback"),
						(ParameterAttributes.None, "object")
					}
				)
				.Select((p, position) => (Position: position, p.Attributes, p.Name))
			)
			{
				delegateBeginInvoke.DefineParameter(parameterInfo.Position, parameterInfo.Attributes, parameterInfo.Name);
			}

			var delegateEndInvoke = delegateClass.DefineMethod(
				"EndInvoke", MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
				CallingConventions.ExplicitThis | CallingConventions.HasThis,
				typeof(uint),
				null, null,
				new Type[] {
					methodInfo.ReturnType,
				}
				.Concat(parameters.Where(p => !p.IsIn).Select(p => p.ParameterType))
				.Concat(new Type[] { typeof(IAsyncResult) })
				.ToArray(),
				null, null
			);
			foreach (var parameterInfo in
				(parameters.Where(p => !p.IsIn).Select(p => (p.Attributes, p.Name)))
				.Concat(
					new (ParameterAttributes Attributes, string Name)[] {
						(ParameterAttributes.None, "result"),
					}
				)
				.Select((p, position) => (Position: position, p.Attributes, p.Name))
			)
			{
				delegateEndInvoke.DefineParameter(parameterInfo.Position, parameterInfo.Attributes, parameterInfo.Name);
			}

			return delegateClass;
		}

		public static void MapDelegate(ModuleBuilder moduleBuilder, TypeBuilder typeBuilder, MethodInfo methodInfo)
		{
			var delegateClass = moduleBuilder.DefineDelegate(methodInfo);
			typeBuilder.DefineField("_" + methodInfo.Name, delegateClass, FieldAttributes.Private);

			var parameters = methodInfo.GetParameters();
			var method = typeBuilder.DefineMethod(methodInfo.Name, methodInfo.Attributes,
				methodInfo.CallingConvention,
				methodInfo.ReturnType,
				null, null,
				parameters.Select(p => p.ParameterType).ToArray(),
				parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
				parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
			);
			foreach (var parameterInfo in parameters)
			{
				method.DefineParameter(parameterInfo.Position, parameterInfo.Attributes, parameterInfo.Name);
			}
			typeBuilder.DefineMethodOverride(method, methodInfo);

		}
	}
}
