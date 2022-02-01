using System;
using System.Runtime.Serialization;

namespace Utils.VirtualMachine
{
	[Serializable]
	internal class VirtualProcessorException : Exception
	{
		public VirtualProcessorException()
		{
		}

		public VirtualProcessorException(string message) : base(message)
		{
		}

		public VirtualProcessorException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected VirtualProcessorException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}