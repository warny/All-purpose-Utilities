using System;
using System.Text;

namespace Utils.IO.Serialization
{
	public interface IReadable { }

	public interface IWritable { }

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class FieldAttribute : Attribute
	{
		public int Order { get; }
		public int? Length { get; }
		public bool BigIndian { get; }
		public FieldEncodingEnum FieldEncoding { get; }
		public Encoding StringEncoding { get; }
		public byte[] Terminators { get; }
		public FieldAttribute( int order, int length = 0, bool bigIndian = false, FieldEncodingEnum FieldEncoding = FieldEncodingEnum.None, string StringEncoding = null )
		{
			if (FieldEncoding == FieldEncodingEnum.FixedLength && length == 0) {
				throw new ArgumentException("La taille d'une chaîne de longueur fixe est obligatoire");
			}

			this.Order = order;
			this.Length = length == 0 ? (int?)null : length;
			this.BigIndian = bigIndian;
			this.FieldEncoding = FieldEncoding;
			this.StringEncoding = StringEncoding is null ? Encoding.Default : Encoding.GetEncoding(StringEncoding) ;
		}
	}

	public enum FieldEncodingEnum
	{
		/// <summary>
		/// Aucun encodage
		/// </summary>
		None = 0,
		/// <summary>
		/// Chaine à longeur fixe
		/// </summary>
		FixedLength,
		/// <summary>
		/// Chaîne à longueur variable
		/// </summary>
		VariableLength,
		/// <summary>
		/// Chaîne à longueur variable terminée par \x0
		/// </summary>
		NullTerminated,
		/// <summary>
		/// Timestamp Unix
		/// </summary>
		TimeStamp,
		/// <summary>
		/// Date OLE
		/// </summary>
		DateTime,
		/// <summary>
		/// Date .NET
		/// </summary>
		Ticks
	}

}
