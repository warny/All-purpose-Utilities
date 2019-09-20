using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.Reflection.BindingFlags;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Utils.Mathematics.Expressions.Compiler
{
	public interface INameResolver
	{
		/// <summary>
		/// Récupère le type d'après son nom
		/// </summary>
		/// <param name="typeName"></param>
		/// <param name="constructorArgumentTypes"></param>
		/// <returns></returns>
		Type ResolveType(string typeName, Type[] constructorArgumentTypes);

		/// <summary>
		/// Récupère le membre static avec le nom passé en paramètre
		/// </summary>
		MemberInfo[] GetStaticMembers(Type t, string memberName);

		/// <summary>
		/// Récupère le membre d'instance avec le nom passé en paramètre
		/// </summary>
		MemberInfo[] GetInstanceMembers(Type t, string memberName);

		/// <summary>
		/// Récupère la fonction du type en paramètre
		/// </summary>
		/// <param name="type">Type dont on veux récupérer la fonction statique</param>
		/// <param name="functionName">Nom de la fonction</param>
		/// <param name="argumentsTypes">Type des arguments de la fonction</param>
		/// <param name="genericArgumentsTypes">Argument génériques explicites</param>
		/// <returns></returns>
		MemberInfo ResolveFunction(MemberInfo[] members, Parameter[] parameters, Type[] genericArgumentsTypes);
	}

	public class Parameter
	{
		public int? Position { get; set; }
		public string Name { get; set; }
		public Type Type { get; set; }
	}

	public class DefaultNameResolver : INameResolver
	{
		private static Regex selectMember = new Regex(@"(?<name>\w+)`\d+");

		public MemberInfo[] GetInstanceMembers(Type t, string memberName)
		{
			IEnumerable<MemberInfo> members = t.GetMembers(Instance | Public | GetField | GetProperty | InvokeMethod);
			members = members.Where(m => selectMember.Match(m.Name).Groups["name"].Value == memberName);
			return members.ToArray();
		}

		public MemberInfo[] GetStaticMembers(Type t, string memberName)
		{
			IEnumerable<MemberInfo> members = t.GetMembers(Static | Public | GetField | GetProperty | InvokeMethod);
			members = members.Where(m => selectMember.Match(m.Name).Groups["name"].Value == memberName);
			return members.ToArray();
		}

		public MemberInfo ResolveFunction(MemberInfo[] members, Parameter[] parameters, Type[] genericArgumentsTypes)
		{
			int distance = int.MaxValue;
			MethodInfo methodInfo = null;
			foreach (var member in members) {
				if (member is MethodInfo mi) {
					var computedDistance = Distance(mi, parameters, genericArgumentsTypes);
					if (computedDistance < distance) methodInfo = mi;
				}
				else if (member is PropertyInfo pi) {
					return pi;
				}
				else if (member is FieldInfo fi) {
					return fi;
				}
			}
			return methodInfo;
		}

		private int? Distance(MethodInfo methodInfo, Parameter[] parameters, Type[] genericArgumentsTypes)
		{
			if (methodInfo.IsGenericMethod && genericArgumentsTypes == null) {
				foreach (var parameter in parameters) {
					
				}
			}
			else if (methodInfo.IsGenericMethod && genericArgumentsTypes != null) {
				if (methodInfo.GetGenericArguments().Length == genericArgumentsTypes.Length) {
					methodInfo = methodInfo.MakeGenericMethod(genericArgumentsTypes);
				}
				else {
					return null;
				}
			}

			var methodParameters = methodInfo.GetParameters();

			int distance = 0;
			foreach (var parameter in parameters) {
				ParameterInfo methodParameter;
				if (parameter.Position != null) {
					methodParameter = methodParameters?[parameter.Position.Value];
					if (methodParameter == null) {
						methodParameter = methodParameters.LastOrDefault();
						if (methodParameter == null) return null;
						if (methodParameter.GetCustomAttribute<ParamArrayAttribute>() == null) return null;
					}
				}
				else if (parameter.Name != null) {
					methodParameter = methodParameters.FirstOrDefault(p => p.Name == parameter.Name);
					if (methodParameters == null) return null;
				}
				else {
					return null;
				}

				Type testType = parameter.Type;
				var objectType = typeof(object);
				while (parameter.Type != methodParameter.ParameterType) {
					if (testType == objectType) return null;
					distance++;
					testType = testType.BaseType;
				}
			}
			return distance;
			
		}

		public Type ResolveType(string typeName, Type[] constructorArgumentTypes)
		{
			return null;
		}

	}

}
