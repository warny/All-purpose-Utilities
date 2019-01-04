using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
		/// Récupère la fonction statique du type en paramètre
		/// </summary>
		/// <param name="type">Type dont on veux récupérer la fonction statique</param>
		/// <param name="functionName">Nom de la fonction</param>
		/// <param name="argumentsTypes">Type des arguments de la fonction</param>
		/// <param name="genericArgumentsTypes">Argument génériques explicites</param>
		/// <returns></returns>
		MethodInfo ResolveStaticFunction(Type type, string functionName, Type[] argumentsTypes, Type[] genericArgumentsTypes);
		/// <summary>
		/// Récupère la fonction d'instance du type en paramètre
		/// </summary>
		/// <param name="type">Type dont on veux récupérer la fonction statique</param>
		/// <param name="functionName">Nom de la fonction</param>
		/// <param name="argumentsTypes">Type des arguments de la fonction</param>
		/// <param name="genericArgumentsTypes">Argument génériques explicites</param>
		/// <returns></returns>
		MethodInfo ResolveInstanceFunction(Type type, string functionName, Type[] argumentsTypes, Type[] genericArgumentsTypes);
	
	}
}
