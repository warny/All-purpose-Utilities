using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Utils.Resources
{
	public class ExternalResource
	{
		public ExternalResource(string resourceName) : this(AppContext.BaseDirectory, resourceName, CultureInfo.CurrentUICulture) { }
		public ExternalResource(string resourceName, string culture) : this(AppContext.BaseDirectory, resourceName, culture) { }
		public ExternalResource(string resourceName, CultureInfo cultureInfo) : this(AppContext.BaseDirectory, resourceName, cultureInfo) { }
		public ExternalResource(string baseDirectory, string resourceName, string culture) : this(baseDirectory, resourceName, CultureInfo.CreateSpecificCulture(culture)) { }

		public ExternalResource(string baseDirectory, string resourceName, CultureInfo cultureInfo) 
		{
		}
	}
}
