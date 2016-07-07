using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Utils.XML
{
	public abstract class XmlDriver
	{
		/// <summary>
		/// Fonctions déclenchées par les noeuds XML
		/// </summary>
		private Dictionary<XPathExpression, Method> triggers;

		/// <summary>
		/// Gestionnaire de noms
		/// </summary>
		public XmlNamespaceManager NamespaceManager { get; }

		/// <summary>
		/// Noeud en cours
		/// </summary>
		protected XPathNavigator Current { get; private set; }

		/// <summary>
		/// Descripteur de méthode
		/// </summary>
		private class Method
		{
			public MethodInfo MethodInfo { get; }
			public ParameterInfo[] Parameters { get; }

			public Method( MethodInfo methodInfo, ParameterInfo[] parameters )
			{
				this.MethodInfo = methodInfo;
				this.Parameters = parameters;
			}
		}

		/// <summary>
		/// Classe de base d'un lecteur de fichier piloté par les données XML
		/// </summary>
		public XmlDriver()
		{

			Type t = this.GetType();
			NamespaceManager = new XmlNamespaceManager(new NameTable());
			foreach (XmlNamespaceAttribute namespaceAttribute in t.GetCustomAttributes<XmlNamespaceAttribute>(true)) {
				NamespaceManager.AddNamespace(namespaceAttribute.Prefix, namespaceAttribute.Namespace);
			}

			triggers = new Dictionary<XPathExpression, Method>();
			foreach (MethodInfo method in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod | BindingFlags.Instance)) {
				var parameters = method.GetParameters();
				foreach (MatchAttribute matchAttribute in method.GetCustomAttributes<MatchAttribute>()) {
					XPathExpression expression = XPathExpression.Compile(matchAttribute.XPathExpression, NamespaceManager);
					triggers.Add(expression, new Method(method, parameters));
				}
			}
		}

		/// <summary>
		/// Créé une expression xPath à partir du xPath passé en paramètre et des espaces de nom du lecteur
		/// </summary>
		/// <param name="xPath">xPath</param>
		/// <returns>object XPathExpression compilé</returns>
		protected XPathExpression CreateExpression( string xPath )
		{
			return XPathExpression.Compile(xPath, NamespaceManager);
		}

		/// <summary>
		/// Appelle les fonctions correspondant au noeuds trouvés par le xPath indiqué
		/// </summary>
		/// <param name="xPath">xPath</param>
		/// <param name="objects">paramètres de la fonction</param>
		protected void Apply( XPathExpression xPath, params object[] objects )
		{
			var nodes = Current.Select(xPath);
			InvokeMethods(nodes, objects);
		}

		/// <summary>
		/// Appelle les fonctions correspondant au noeuds trouvés par le xPath indiqué
		/// </summary>
		/// <param name="xPath">xPath</param>
		/// <param name="objects">paramètres de la fonction</param>
		protected void Apply( string xPath, params object[] objects )
		{
			var nodes = Current.Select(xPath, NamespaceManager);
			InvokeMethods(nodes, objects);
		}

		/// <summary>
		/// Appelle la fonction correspondant au noeud passé en paramètre
		/// </summary>
		/// <param name="node">Noeud</param>
		/// <param name="objects">paramètres de la fonction</param>
		protected void Apply( XPathNavigator node, params object[] objects )
		{
			Type[] types = objects.Select(o => o.GetType()).ToArray();
			InvokeMethod(node, objects, types);
		}

		/// <summary>
		/// Appelle les fonctions correspondant au noeuds trouvés par le xPath indiqué
		/// </summary>
		/// <param name="node">noeud de référence de la recherche</param>
		/// <param name="xPath">xPath</param>
		/// <param name="objects">paramètres de la fonction</param>
		protected void Apply( XPathNavigator node, XPathExpression xPath, params object[] objects )
		{
			var nodes = node.Select(xPath);
			InvokeMethods(nodes, objects);
		}

		/// <summary>
		/// Appelle les fonctions correspondant au noeuds trouvés par le xPath indiqué
		/// </summary>
		/// <param name="node">noeud de référence de la recherche</param>
		/// <param name="xPath">xPath</param>
		/// <param name="objects">paramètres de la fonction</param>
		protected void Apply( XPathNavigator node, string xPath, params object[] objects )
		{
			var nodes = node.Select(xPath, NamespaceManager);
			InvokeMethods(nodes, objects);
		}

		/// <summary>
		/// Sélectionne les noeuds grâce au xPath spécifié
		/// </summary>
		/// <param name="xPath">xPath</param>
		/// <returns>Noeud</returns>
		protected XPathNodeIterator GetNodes( XPathExpression xPath )
		{
			return Current.Select(xPath);
		}

		/// <summary>
		/// Sélectionne les noeuds grâce au xPath spécifié
		/// </summary>
		/// <param name="xPath">xPath</param>
		/// <returns>Noeud</returns>
		protected XPathNodeIterator GetNodes( string xPath )
		{
			return Current.Select(xPath, NamespaceManager);
		}

		/// <summary>
		/// Sélectionne les noeuds grâce au xPath spécifié relativement au noeud passé en paramètre
		/// </summary>
		/// <param name="node">Noeud de référence</param>
		/// <param name="xPath">xPath</param>
		/// <returns>Liste de noeuds</returns>
		protected XPathNodeIterator GetNodes( XPathNavigator node, XPathExpression xPath )
		{
			return node.Select(xPath);
		}


		/// <summary>
		/// Sélectionne les noeuds grâce au xPath spécifié relativement au noeud passé en paramètre
		/// </summary>
		/// <param name="node">Noeud de référence</param>
		/// <param name="xPath">xPath</param>
		/// <returns>Liste de noeuds</returns>
		protected XPathNodeIterator GetNodes( XPathNavigator node, string xPath )
		{
			return node.Select(xPath, NamespaceManager);
		}

		/// <summary>
		/// Renvoie la valeur du noeud passé en paramètre
		/// </summary>
		/// <param name="xPath">Chemin du noeud</param>
		/// <returns></returns>
		protected string ValueOf( string xPath = "." )
		{
			var node = Current.SelectSingleNode(xPath, NamespaceManager);
			return node?.Value;
		}

		/// <summary>
		/// Renvoie la valeur du noeud passé en paramètre
		/// </summary>
		/// <param name="xPath">Chemin du noeud</param>
		/// <returns></returns>
		protected T ValueOf<T>(string xPath = "." )
		{
			var node = Current.SelectSingleNode(xPath, NamespaceManager);
			return (T)(node?.ValueAs(typeof(T)) ?? default(T));
		}

		/// <summary>
		/// Renvoie la valeur du noeud passé en paramètre
		/// </summary>
		/// <param name="xPath">Chemin du noeud</param>
		/// <returns></returns>
		protected string ValueOf( XPathNavigator node, string xPath = "." )
		{
			node = Current.SelectSingleNode(xPath, NamespaceManager);
			return node?.Value;
		}

		/// <summary>
		/// Renvoie la valeur du noeud passé en paramètre
		/// </summary>
		/// <param name="xPath">Chemin du noeud</param>
		/// <returns></returns>
		protected T ValueOf<T>( XPathNavigator node, string xPath = "." )
		{
			node = node.SelectSingleNode(xPath, NamespaceManager);
			return (T)(node?.ValueAs(typeof(T)) ?? default(T));
		}

		/// <summary>
		/// Invokes toutes la méthode de chaque élément de la liste de noeuds
		/// </summary>
		/// <param name="nodes"></param>
		/// <param name="objects"></param>
		private void InvokeMethods(XPathNodeIterator nodes, params object[] objects)
		{
			Type[] types = objects.Select(o=>o.GetType()).ToArray();

			foreach (XPathNavigator node in nodes) {
				InvokeMethod(node, objects, types);
			}
		}

		/// <summary>
		/// Invoke la méthode du noeud passé en paramètre
		/// </summary>
		/// <param name="node"></param>
		/// <param name="objects"></param>
		/// <param name="types"></param>
		private void InvokeMethod( XPathNavigator node, object[] objects, Type[] types )
		{
			foreach (var trigger in triggers) {
				List<object> defaultValues = new List<object>();
				Array paramsValues = null;
				if (node.Matches(trigger.Key)) {
					var method = trigger.Value;
					var parameters = method.Parameters;

					bool isOk = true;
					for (int i = 0 ; i < parameters.Length ; i++) {
						var parameter = parameters[i];
						var type = i < types.Length ? types[i] : null;

						if (parameter.GetCustomAttribute<ParamArrayAttribute>()!= null) {
							var paramsLength = objects.Length - i;
							if (!parameter.ParameterType.IsAssignableFrom(type)) {
								var elementType = parameter.ParameterType.GetElementType();
								for (int j = i ; j < types.Length ; j++) {
									if (!elementType.IsAssignableFrom(types[j])) {
										isOk = false;
										break;
									}
								}
								if (!isOk) break;

								paramsValues = Array.CreateInstance(parameter.ParameterType.GetElementType(), objects.Length - i);
								Array.Copy(objects, i, paramsValues, 0, paramsLength);
								object[] newObjects = new object[i];
								Array.Copy(objects, newObjects, i);
								objects = newObjects;
							}
						} else {

							if (type!= null) {
								if (!parameter.ParameterType.IsAssignableFrom(type)) {
									isOk = false;
									break;
								}
							} else {
								if (parameter.IsOptional) {
									defaultValues.Add(parameter.DefaultValue);
								} else {
									isOk = false;
									break;
								}
							}
						}
					}
					if (isOk) {
						var arguments = new List<object>();
						arguments.AddRange(objects);
						if (paramsValues!= null)
							arguments.Add(paramsValues);

						var oldContext = this.Current;
						Current = node;
						method.MethodInfo.Invoke(this, arguments.ToArray());
						this.Current = oldContext;
						break;
					}
				}
			}
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		/// <param name="navigator"></param>
		public void Read( XPathNavigator navigator)
		{
			InvokeMethods(navigator.Select("/"));
		}
		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( string uri )
		{
			Read(new XPathDocument(uri).CreateNavigator());
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( string uri, XmlSpace xmlSpace )
		{
			Read(new XPathDocument(uri, xmlSpace).CreateNavigator());
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( Stream stream )
		{
			Read(new XPathDocument(stream).CreateNavigator());
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( TextReader reader)
		{
			Read(new XPathDocument(reader).CreateNavigator());
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( XmlReader reader )
		{
			Read(new XPathDocument(reader).CreateNavigator());
		}
		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( XmlReader reader, XmlSpace xmlSpace )
		{
			Read(new XPathDocument(reader, xmlSpace).CreateNavigator());
		}

		/// <summary>
		/// Lit le document XML
		/// </summary>
		public void Read( XmlDocument xmlDocument )
		{
			Read(xmlDocument.CreateNavigator());
		}

		/// <summary>
		/// Lit la racime du document
		/// </summary>
		[Match("/")]
		protected abstract void Root();

	}


	/// <summary>
	/// Ajoute un espace de nom pour la lecture du fichier XML
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
	class XmlNamespaceAttribute : Attribute {
		public static readonly Regex validatePrefix = new Regex("^[A-Za-z]\\w*$");

		public string Prefix { get; }
		public string Namespace { get; }

		public XmlNamespaceAttribute(string prefix, string @namespace) {
			if (!validatePrefix.IsMatch(prefix)) {
				throw new ArgumentException($"Valeur incohérente du préfixe : {prefix}", nameof(prefix));
			}

			this.Prefix = prefix;
			this.Namespace = @namespace;
		}
	}

	/// <summary>
	/// Ajoute un text xPath pour le déclenchement d'une fonction
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	class MatchAttribute : Attribute
	{
		public string XPathExpression { get; }

		public MatchAttribute( string xPathExpression )
		{
			this.XPathExpression = xPathExpression;
		}
	}
}
