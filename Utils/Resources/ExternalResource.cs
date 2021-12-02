using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Utils.Resources
{
	public class ExternalResource
	{
		private string baseDirectory;
		private string resourceName;

		public ExternalResource(string resourceName) : this(AppDomain.CurrentDomain.BaseDirectory, resourceName) { }

		public ExternalResource(string baseDirectory, string resourceName)
		{
			this.baseDirectory = baseDirectory;
			this.resourceName = resourceName;
		}
	}
}
