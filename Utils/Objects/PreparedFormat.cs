using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public interface IPreparedFormat
	{
		/// <summary>
		/// Liste de constante 
		/// </summary>
		Dictionary<char, GetConstantDelegate> GetConstants { get; set; }

		/// <summary>
		/// Liste de surcharge de fonctions
		/// </summary>
		Overrides Overrides { get; set; }
	}


	public class PreparedFormat : IPreparedFormat
	{
		/// <summary>
		/// Regex permettant de découper une chaîne de la forme
		/// <example>{field[:format]}text{field2[:format2]}...</example>
		/// <remarks>
		/// Les doubles accolades ("{{" ou "}}") sont considérées comme du texte et sont remplacées
		/// par un seule accolade
		/// </remarks>
		/// </summary>
		private static readonly Regex parseFormatString = new Regex(@"(\{(?<text>\{)|\}(?<text>\})|\{(?<field>[^:{}]+)(:(?<format>[^{}]+))?\}|(?<text>[^{}]+)|(?<error>\{|\}))");
		private static readonly Regex parseObjectString = new Regex(@"(?<method>\w+)\s*\(\s*(?<param>\'.+?\'|[\d.-]+)(\s*,\s*(?<param>\'.+?\'|[\d.-]+))*\s*\)|(?<method>\w+)\s*\(\s*\)|\[\s*(?<indexer>([\d.]|\'.*?\')+?)(\s*,\s*(?<indexer>([\d.]|\'.*?\')+?))*\s*\]|(?<name>\w+)");

		/// <summary>
		/// Descripteur de partie de la chaîne de format
		/// </summary>
		private struct FormatPart
		{
			/// <summary>
			/// Indique si le FormatPart contient du texte (true) ou une expression (false)
			/// </summary>
			public bool text;
			/// <summary>
			/// Texte ou expression
			/// </summary>
			public string datas;
			/// <summary>
			/// Format à appliquer sur l'expression
			/// </summary>
			public string format;
		}

		/// <summary>
		/// Culture utilisée comme fournisseur de format de chaînes
		/// </summary>
		public CultureInfo CultureInfo { get; set; }

		/// <summary>
		/// Parties de la chaîne de format
		/// </summary>
		private List<FormatPart> formatParts = new List<FormatPart>();

		/// <summary>
		/// Chaîne de format
		/// </summary>
		public string FormatString { get; private set; }

		/// <summary>
		/// Liste de recherche de constante
		/// </summary>
		private Dictionary<char, GetConstantDelegate> getConstants { get; set; }

		/// <summary>
		/// Liste de recherche de constante
		/// </summary>
		public Dictionary<char, GetConstantDelegate> GetConstants
		{
			get
			{
				if (getConstants is null) {
					getConstants = new Dictionary<char, GetConstantDelegate>();
				}
				return getConstants;
			}
			set { getConstants = value; }
		}


		/// <summary>
		/// Liste de surcharge de fonctions
		/// </summary>
		private Overrides overrides;

		/// <summary>
		/// Liste de surcharge de fonctions
		/// </summary>
		public Overrides Overrides
		{
			get
			{
				if (overrides is null) {
					overrides = new Overrides();
				}
				return overrides;
			}
			set { overrides = value; }
		}

		/// <summary>
		/// Créé un formateur de chaîne à partir de la chaîne passée en paramètre
		/// </summary>
		/// <param name="formatString">Format de chaîne</param>
		public PreparedFormat( string formatString )
		{
			if (formatString is null) return;
			this.FormatString = formatString;

			StringBuilder text = new StringBuilder();

			// découpe la chaîne de format
			foreach (Match match in parseFormatString.Matches(formatString)) {
				if (match.Groups["error"].Success) {
					throw new FormatException(string.Format("Chaîne de format incorrect : {0} était inattendu", match.Groups["error"].Value));
				}
				if (match.Groups["text"].Success) {
					text.Append(match.Groups["text"].Value);
				} else if (match.Groups["field"].Success) {
					if (text.Length > 0) {
						formatParts.Add(new FormatPart() { text = true, datas = text.ToString(), format = null });
						text = new StringBuilder();
					}

					formatParts.Add(new FormatPart() { text = false, datas = match.Groups["field"].Value, format = match.Groups["format"].Value });
				}
			}

			// s'il reste un texte, l'ajoute aux textes à écrire à la fin
			if (text.Length > 0) {
				formatParts.Add(new FormatPart() { text = true, datas = text.ToString(), format = null });
			}

		}

		/// <summary>
		/// Renvoie la chaîne formatée en fonction de la source
		/// </summary>
		/// <param name="getDatasFromSource">Source de donnée</param>
		/// <returns>Chaîne formatée</returns>
		private string FormatDatas( Func<string, object, object> getDatasFromSource, object obj )
		{
			if (FormatString is null) return null;
			StringBuilder formattedText = new StringBuilder();
			foreach (var part in formatParts) {
				if (part.text) {
					formattedText.Append(part.datas);
				} else {
					object data;
					// teste si l'objet FormatPart en cours contient une constante
					// les constantes sont identifiées par un caractère spécial (§, %, etc.) en début de chaîne
					if (GetConstants.ContainsKey(part.datas[0])) {
						string[] dataparts = part.datas.Split(new char[] { '.' }, 2);
						object current = GetConstants[dataparts[0][0]](dataparts[0].Substring(1));
						if (dataparts.Length > 1)
							data = getDatasFromSource(dataparts[1], current);
						else
							data = current;
					} else {
						data = getDatasFromSource(part.datas, obj);
					}

					if (data is string) {
						if (part.format is not null) {
							string temp = (string)data;
							foreach (string formatpart in part.format.Split(',')) {

								switch (formatpart.Trim().ToLower()) {
									case "uc":
									case "uppercase": {
											temp = temp.ToUpper();
											break;
										}
									case "lc":
									case "lowercase": {
											temp = temp.ToLower();
											break;
										}
									case "fluc":
									case "firstletteruppercase": {
											if (temp.Length > 0) {
												temp = temp.Substring(0, 1).ToUpper() + temp.Substring(1).ToLower();
											}
											break;
										}
								}
							}
							formattedText.Append(temp);
						} else {
							formattedText.Append(data);
						}
					} else if (data is bool) {
						if (!string.IsNullOrWhiteSpace(part.format)) {
							string[] values = part.format.Split(',');
							if (values.Length == 2) {
								formattedText.Append((bool)data ? values[1] : values[0]);
							} else {
								formattedText.Append(data);
							}
						} else {
							formattedText.Append(data);
						}
					} else if (data is Int16 || data is Int32 || data is Int64) {
						if (!string.IsNullOrWhiteSpace(part.format)) {
							string[] values = part.format.Split(',');
							Int64 value = Convert.ToInt64(data);
							if (values.Length > 1 && value >= 0 && value < values.Length) {
								formattedText.Append(values[value]);
							} else if (values.Length == 1) {
								formattedText.Append(value.ToString(values[0]));
							} else {
								formattedText.Append(data);
							}
						} else {
							formattedText.Append(data);
						}
					} else if (data is IFormattable) {
						formattedText.Append(((IFormattable)data).ToString(part.format, CultureInfo));
					} else if (data is not null) {
						formattedText.Append(data.ToString());
					}

				}
			}
			return formattedText.ToString();
		}

		/// <summary>
		/// Renvoie une chaîne formatée à partir d'un dictionnaire
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string Format( IDictionary obj )
		{
			Func<string, object, object> getDatasFromSource = (( name, o ) => ((IDictionary)o)[name]);
			return FormatDatas(getDatasFromSource, obj);
		}

		/// <summary>
		/// Renvoie une chaîne formatée à partir d'une ligne de donnée lue dans une base de donnée
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string Format( IDataRecord obj )
		{
			if (FormatString is null) return null;
			Func<string, object, object> getDatasFromSource = (( name, o ) => ((IDataRecord)o)[name]);
			return FormatDatas(getDatasFromSource, obj);
		}

		/// <summary>
		/// Renvoie la chîne formatté à partir d'un tableau
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string Format( params object[] obj )
		{
			return Format((object)obj);
		}

		/// <summary>
		/// Renvoie la chîne formatté à partir d'un objet
		/// </summary>
		/// <param name="obj">Objet</param>
		/// <returns></returns>
		public string Format( object obj )
		{
			if (FormatString is null) return null;
			// Vérifie s'il existe un cache d'execution
			if (ObjectShadow is null) ObjectShadow = new Dictionary<Type, Dictionary<string, objectShadow[]>>();

			// Vérifie s'il existe un cache d'execution pour l'objet passé en paramètre
			Type BaseType = obj.GetType();
			Dictionary<string, objectShadow[]> CurrentShadow;
			// Si non, le créé
			if (ObjectShadow.ContainsKey(BaseType)) {
				CurrentShadow = ObjectShadow[BaseType];
			} else {
				CurrentShadow = new Dictionary<string, objectShadow[]>();
				ObjectShadow.Add(BaseType, CurrentShadow);
			}

			var getDatasFromSource = new Func<string, object, object>(
				( name, current ) => ExecuteObjectMembers(name, current, CurrentShadow)
			);

			return FormatDatas(getDatasFromSource, obj);
		}

		public object ExecuteObjectMembers( string name, object current )
		{
			if (ObjectShadow is null) ObjectShadow = new Dictionary<Type, Dictionary<string, objectShadow[]>>();
			return ExecuteObjectMembers(name, current, ObjectShadow[current.GetType()]);
		}

		/// <summary>
		/// Exécute la chaîne des membres de l'objet current passé en paramètre
		/// </summary>
		/// <param name="name">liste des membres séparés par '.'</param>
		/// <param name="current">object sur lequel appliquer l'exécution</param>
		/// <param name="CurrentShadow">cache d'execution</param>
		/// <returns>résultat de l'execution</returns>
		private object ExecuteObjectMembers( string name, object current, Dictionary<string, objectShadow[]> currentShadow )
		{
			if (currentShadow.ContainsKey(name)) {
				// Cas on on a caché l'execution. On cherche le type du paramètre, et on l'execute
				foreach (objectShadow shadow in currentShadow[name]) {
					// dans le cas où on obtient un objet null, on arrête le traitement
					if (current is null) return null;
					// sinon, on execute la prochaine procédure, fonction ou propriété
					if (shadow.member is FieldInfo) {
						current = ((FieldInfo)shadow.member).GetValue(current);
					} else if (shadow.member is PropertyInfo) {
						current = ((PropertyInfo)shadow.member).GetValue(current, shadow.parameters);
					} else if (shadow.member is MethodInfo) {
						object obj = null;
						try {
							if (shadow.External) {
								shadow.parameters[0] = current;
								obj = null;
							} else {
								obj = current;
							}
							current = ((MethodInfo)shadow.member).Invoke(obj, shadow.parameters);
						} catch (TargetException e) {
							HandleTargetException(e, (MethodInfo)shadow.member, obj, shadow.parameters);
						}
					}
				}
			} else {
				List<objectShadow> shadowObjects = new List<objectShadow>();
				// si on a pas de cache, on le créé à la première execution
				foreach (Match match in parseObjectString.Matches(name)) {
					// dans le cas où on obtient un objet null, on arrête le traitement
					if (current is null) return null;
					// sinon, on execute la prochaine procédure, fonction ou propriété
					objectShadow shadow = new objectShadow();
					Type t = current.GetType();
					if (match.Groups["name"].Success) {
						long index;
						if (current is Array && long.TryParse(match.Groups["name"].Value, out index)) {
							// si l'objet en cours est un tableau et qu'on trouve un nombre, lis l'objet comme un tableau
							object[] convertedParameters;
							bool external;
							MethodInfo m = GetMethod(typeof(Array), "GetValue", new string[] { match.Groups["name"].Value }, out convertedParameters, out external);
							shadow.member = m;
							shadow.External = external;
							shadow.parameters = convertedParameters;
							current = m.Invoke(current, shadow.parameters);
						} else {
							// Si on trouve un nom dans la chaîne, on cherche une propriété ou un champ de l'objet en cours
							PropertyInfo p = t.GetProperty(match.Groups["name"].Value, BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public);
							if (p is not null) {
								shadow.member = p;
								shadow.parameters = new object[] { };
								current = p.GetValue(current, shadow.parameters);
							} else {
								FieldInfo f = t.GetField(match.Groups["name"].Value, BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public);
								if (f is not null) {
									shadow.member = f;
									shadow.parameters = null;
									current = f.GetValue(current);
								} else {
									PropertyInfo i = GetDefaultStringIndexer(t);
									if (i is not null) {
										shadow.member = i;
										shadow.parameters = new object[] { match.Groups["name"].Value };
										current = i.GetValue(current, shadow.parameters);
									} else {
										throw new Exception(string.Format("Champ ou propriété inconnu ({0})", match.Groups["name"].Value));
									}
								}
							}
						}
					} else if (match.Groups["indexer"].Success) {
						if (current is Array) {
							object[] convertedParameters;
							bool external;
							string[] parameters = (from Capture c in match.Groups["indexer"].Captures select c.Value).ToArray();
							MethodInfo m = GetMethod(typeof(Array), "GetValue", parameters, out convertedParameters, out external);
							shadow.member = m;
							shadow.parameters = convertedParameters;
							shadow.External = external;
							try {
								current = m.Invoke(current, shadow.parameters);
							} catch (TargetException e) {
								HandleTargetException(e, m, current, shadow.parameters);
							}
						} else {
							// si on trouve un crochet, on cherche l'indexeur par défaut de l'objet
							string[] parameters = (from Capture c in match.Groups["indexer"].Captures select c.Value).ToArray();
							object[] convertedParameters;
							PropertyInfo p = GetIndexer(t, parameters, out convertedParameters);
							shadow.member = p;
							shadow.parameters = convertedParameters;
							current = p.GetValue(current, convertedParameters);
						}
					} else if (match.Groups["method"].Success) {
						string methodName = match.Groups["method"].Value;

						// si on trouve un nom suivi de parenthèses, on assume qu'il s'agit d'une méthode 
						string[] parameters = (from Capture c in match.Groups["param"].Captures select c.Value).ToArray();
						object[] convertedParameters;
						bool external;
						MethodInfo m = GetMethod(t, methodName, parameters, out convertedParameters, out external);
						shadow.member = m;
						shadow.External = external;
						shadow.parameters = convertedParameters;
						object obj = null;
						try {
							if (external) {
								convertedParameters[0] = current;
								obj = null;
							} else {
								obj = current;
							}
							current = m.Invoke(obj, convertedParameters);
						} catch (TargetException e) {
							HandleTargetException(e, m, obj, convertedParameters);
						}
					}
					shadowObjects.Add(shadow);
				}
				// on cache les appels
				currentShadow.Add(name, shadowObjects.ToArray());
			}
			// on revoie l'objet trouvé
			return current;
		}

		private void HandleTargetException( Exception e, MethodInfo m, object obj, object[] parameters )
		{
			if (e is TargetException) {
				string message = string.Format(
					"Exception de type {0} lors de l'appel de Invoke(obj, parameters) sur la méthode {1}. Le type de obj est {2} (valeur {3}), les paramètres sont ({4})",
					e.GetType().Name,
					m.DeclaringType.FullName + "." + m.Name,
					obj is not null ? obj.GetType().FullName : "N.A.",
					obj is not null ? obj.ToString() : "null",
					string.Join(", ", parameters.Select(p => p is not null ? p.ToString() : "null"))
				);
				throw new CustomTargetException(message, (TargetException)e);
			} else {
				throw e;
			}
		}

		/// <summary>
		/// Cache de la lecture des object
		/// </summary>
		private Dictionary<Type, Dictionary<string, objectShadow[]>> ObjectShadow;

		/// <summary>
		/// Structure de cache des objets
		/// </summary>
		struct objectShadow
		{
			public MemberInfo member;
			public bool External;
			public object[] parameters;
		}

		/// <summary>
		/// Renvoie une chaîne formatée à partir d'une propriété indexée
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public string FormatFromIndexer( object obj )
		{
			if (FormatString is null) return null;
			Type t = obj.GetType();
			var Indexer = GetDefaultStringIndexer(t);
			var getMethod = Indexer.GetGetMethod();

			Func<string, object, object> getDatasFromSource = (( name, o ) => getMethod.Invoke(o, new object[] { name }));
			return FormatDatas(getDatasFromSource, obj);
		}

		/// <summary>
		/// Récupère l'indexeur par défaut ayant pour argument une chaîne de caractère
		/// </summary>
		/// <param name="t">Type dont on doit récupérer l'indexeur</param>
		/// <returns></returns>
		private PropertyInfo GetDefaultStringIndexer( Type t )
		{
			var properties =
				from p in
					(
						from p in t.GetDefaultMembers()
						where p.MemberType == MemberTypes.Property
						select (PropertyInfo)p
					)

				where p.GetIndexParameters().Length == 1
				   && p.GetIndexParameters()[0].ParameterType == typeof(string)
				select p;
			if (!properties.Any()) return null;
			return properties.First();
		}

		/// <summary>
		/// Récupère l'indexeur par défaut ayant pour argument une chaîne de caractère
		/// </summary>
		/// <param name="t">Type dont on doit récupérer l'indexeur</param>
		/// <returns></returns>
		private PropertyInfo GetIndexer( Type t, string[] parameters, out object[] ConvertedParameters )
		{
			var properties =
				from p in
					(
						from p in t.GetDefaultMembers()
						where p.MemberType == MemberTypes.Property
						select (PropertyInfo)p
					)

				where p.GetIndexParameters().Length == parameters.Length
				select p;

			ConvertedParameters = new object[parameters.Length];

			foreach (var property in properties) {
				bool found = VerifyParameters(property.GetIndexParameters(), parameters, ConvertedParameters, false);
				if (found) return property;
			}

			throw new Exception("L'objet n'a pas d'indexeur");
		}

		/// <summary>
		/// Récupère l'indexeur par défaut ayant pour argument une chaîne de caractère
		/// </summary>
		/// <param name="t">Type dont on doit récupérer l'indexeur</param>
		/// <returns></returns>
		private MethodInfo GetMethod( Type t, string name, string[] parameters, out object[] ConvertedParameters, out bool external )
		{
			// Recherche la méthode dans la liste des méthodes définies dans overrides
			if (Overrides.Contains(t, name)) {
				MethodInfo m = Overrides[t, name];
				ConvertedParameters = new object[parameters.Length + 1];
				bool found = VerifyParameters(m.GetParameters(), parameters, ConvertedParameters, true);
				if (found) {
					external = true;
					return m;
				}
			}

			// Recherche les méthodes dans la classe
			var methods =
				from p in t.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public)
				where String.Compare(p.Name, name, true) == 0
				   && p.GetParameters().Length == parameters.Length
				select p;

			ConvertedParameters = new object[parameters.Length];

			foreach (var method in methods) {
				bool found = VerifyParameters(method.GetParameters(), parameters, ConvertedParameters, false);
				if (found) {
					external = false;
					return method;
				}
			}

			throw new Exception(string.Format("Méthode inconnue : {0}", name));
		}

		/// <summary>
		/// Vérifie les paramètres d'un indexeur ou d'une méthode
		/// </summary>
		/// <param name="parametersInfo">Descripteurs des parametres de la fonction ou de la méthode</param>
		/// <param name="parameters">Parametres passés à la fonction ou à la méthode</param>
		/// <param name="ConvertedParameters">Paramètres convertis</param>
		/// <param name="external">Indique si la méthode appartient à la classe testée</param>
		/// <returns></returns>
		private bool VerifyParameters( ParameterInfo[] parametersInfo, string[] parameters, object[] ConvertedParameters, bool external )
		{
			bool found = true;
			foreach (ParameterInfo parameter in parametersInfo) {
				int position = 0;
				if (external) {
					if (parameter.Position == 0) continue;
					position = parameter.Position - 1;
				} else {
					position = parameter.Position;
				}
				string data = parameters[position];
				if (parameter.ParameterType == typeof(string)) {
					if (data.StartsWith("\'") && data.EndsWith("\'")) {
						ConvertedParameters[parameter.Position] = data.Substring(1, data.Length - 2).Replace("''", "'");
					} else {
						found = false;
						break;
					}
				} else if (this.GetConstants.ContainsKey(data[0])) {
					object value = this.GetConstants[data[0]](data.Substring(1));
					if (!(value.GetType() == parameter.ParameterType)) {
						ConvertedParameters[parameter.Position] = value;
						found = false;
						break;
					}
				} else {
					try {
						ConvertedParameters[parameter.Position] = Convert.ChangeType(data, parameter.ParameterType);
					} catch {
						found = false;
						break;
					}
				}
			}
			return found;
		}

		/// <summary>
		/// Returns a hash code for this instance.
		/// </summary>
		/// <returns>
		/// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
		/// </returns>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		public override int GetHashCode()
		{
			return FormatString.GetHashCode();
		}
	}

	/// <summary>
	/// Récupère une constante
	/// </summary>
	/// <param name="constantName">Nom de la constante à récupérer</param>
	/// <returns>object</returns>
	public delegate object GetConstantDelegate( string constantName );

	internal class CustomTargetException : Exception
	{
		public CustomTargetException( string msg, TargetException e ) : base(msg, e) { }

	}


	/// <summary>
	/// Classe servant à définir des redéfinition et des surcharges de commandes
	/// </summary>
	public class Overrides
	{
		/// <summary>
		/// Fonctions redéfinies
		/// </summary>
		private Dictionary<Type, Dictionary<string, MethodInfo>> functions;

		/// <summary>
		/// Créé une classe de redéfinition
		/// </summary>
		public Overrides()
		{
			functions = new Dictionary<Type, Dictionary<string, MethodInfo>>();
		}

		/// <summary>
		/// Ajoute une fonction de redéfinition à partir d'un délégué
		/// </summary>
		/// <param name="name">Nom de la fonction</param>
		/// <param name="delegateMethod">Délégué qui effectue réellement la fonction</param>
		public void Add( string name, Delegate delegateMethod )
		{
			Add(name, delegateMethod.Method);
		}

		/// <summary>
		/// Ajoute une fonction de redéfinition à partir d'un délégué
		/// </summary>
		/// <param name="name">Nom de la fonction</param>
		/// <param name="method">Methode reflixive qui effectue réellement la fonction</param>
		public void Add( string name, MethodInfo method )
		{
			ParameterInfo[] parameters = method.GetParameters();
			Dictionary<string, MethodInfo> typedFunctions;
			if (!functions.ContainsKey(parameters[0].ParameterType)) {
				typedFunctions = new Dictionary<string, MethodInfo>();
				functions.Add(parameters[0].ParameterType, typedFunctions);
			} else {
				typedFunctions = functions[parameters[0].ParameterType];
			}
			try {
				typedFunctions.Add(name, method);
			} catch (ArgumentException) {
				throw new ArgumentException(string.Format(
					"Impossible d'insérer une méthode nommée {0} car elle a déjà été définie", name)
				);
			}
		}

		/// <summary>
		/// Vérifie si une redéfinition de function existe
		/// </summary>
		/// <param name="t">Type auquel s'applique la fonction</param>
		/// <param name="name">Nom redéfini</param>
		/// <returns>Vrai si la redéfinition existe</returns>
		public bool Contains( Type t, String name )
		{
			return functions.ContainsKey(t) && functions[t].ContainsKey(name);
		}

		/// <summary>
		/// Retrouve la méthode refléchie qui correspond au type et au nom indiqués 
		/// </summary>
		/// <param name="t"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public MethodInfo this[Type t, String name]
		{
			get
			{
				if (functions.ContainsKey(t) && functions[t].ContainsKey(name))
					return functions[t][name];
				else return null;
			}
		}
	}

}
