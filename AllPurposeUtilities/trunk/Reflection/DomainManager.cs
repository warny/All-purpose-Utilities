using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Reflection
{
	public class DomainManager : IDisposable
	{
		/// <summary>
		/// Domaine de chargement d'assemblies
		/// </summary>
		private AppDomain domain;
		/// <summary>
		/// Assembly en cours d'execution
		/// </summary>
		private Assembly currentAssembly;

		/// <summary>
		/// nom du domaine
		/// </summary>
		public string DomainName { get; private set; }

		/// <summary>
		/// Permet de faire des opérations de reflexion sur les objets du domaine
		/// </summary>
		private DomainWrapper wrapper;

		/// <summary>
		/// Créer un domaine de chargement d'assemblies
		/// </summary>
		/// <param name="domainName">Nom du domaine</param>
		public DomainManager( string domainName, string applicationName = null )
		{
			currentAssembly = typeof(DomainManager).Assembly;

			this.DomainName = domainName;
			AppDomainSetup setup = AppDomain.CurrentDomain.SetupInformation;
			setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
			setup.ApplicationName = applicationName ?? domainName;
			setup.ShadowCopyFiles = "true";

			//Création du nouveau domaine d'application
			domain = AppDomain.CreateDomain(domainName, null, setup);
			wrapper = (DomainWrapper) domain.CreateObjRef(typeof(DomainWrapper)).GetRealObject(new StreamingContext(StreamingContextStates.All));
		}

		/// <summary>
		/// Créé un objet dans le contexte du domaine
		/// </summary>
		/// <param name="assemblyName">Nom de l'assembly</param>
		/// <param name="typeName">Nom du type</param>
		/// <returns>Instance du type</returns>
		public object CreateInstanceFromAndUnwrap( string assemblyName, string typeName, Func<string, string> pathResolver = null )
		{
			if (pathResolver != null && !Path.IsPathRooted(assemblyName)) assemblyName = pathResolver(assemblyName);
			return domain.CreateInstanceFromAndUnwrap(assemblyName, typeName);
		}

		/// <summary>
		/// Créé un objet dans le contexte du domaine
		/// </summary>
		/// <param name="assemblyName">Nom de l'assembly</param>
		/// <param name="typeName">Nom du type</param>
		/// <returns>Instance du type</returns>
		public T CreateInstanceFromAndUnwrap<T>( string assemblyName, string typeName, Func<string, string> pathResolver = null ) 
		{
			return (T)CreateInstanceFromAndUnwrap(assemblyName, typeName);
		}

		/// <summary>
		/// Appelle une méthode de l'objet séléectionné par reflexion
		/// </summary>
		/// <param name="o"></param>
		/// <param name="Name"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public object CallMethod( object o, string Name, Dictionary<string, object> parameters )
		{
			return wrapper.CallMethod(o, Name, parameters);
		}

		public void Collect()
		{
			wrapper.Collect();
		}

		/// <summary>
		/// Détruit le domaine lorsque celui-ci n'est plus utile
		/// </summary>
		public void Dispose()
		{
			if (domain==null) return;
			AppDomain.Unload(domain);
			domain = null;
			DomainName = null;
		}

		~DomainManager()
		{
			this.Dispose();
		}


	}

	internal class DomainWrapper : MarshalByRefObject
	{
		public object CallMethod( object o, string Name, Dictionary<string, object> parameters )
		{
			Type type = o.GetType();
			MethodInfo method = type.GetMethod(Name);
			var parametersInfo = method.GetParameters();
			object[] parametersArray = new object[parametersInfo.Length];
			foreach (var parameterInfo in parametersInfo) {
				parametersArray[parameterInfo.Position] =
							Convert.ChangeType(parameters[parameterInfo.Name], parameterInfo.ParameterType);
			}
			return method.Invoke(o, parametersArray);
		}

		public void Collect()
		{
			GC.Collect();
		}
	}

}
