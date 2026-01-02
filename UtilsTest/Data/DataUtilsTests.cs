using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Data;

namespace UtilsTests.Data;

/// <summary>
/// Validates mappings provided by <see cref="DataUtils"/>.
/// </summary>
[TestClass]
public class DataUtilsTests
{
    /// <summary>
    /// Ensures <see cref="DataUtils.GetDbType(Type)"/> returns the expected mapping for known CLR types.
    /// </summary>
    /// <param name="type">The CLR type to evaluate.</param>
    /// <param name="expected">The expected <see cref="DbType"/>.</param>
    [DataTestMethod]
    [DataRow(typeof(string), DbType.String)]
    [DataRow(typeof(bool), DbType.Boolean)]
    [DataRow(typeof(byte), DbType.Byte)]
    [DataRow(typeof(sbyte), DbType.SByte)]
    [DataRow(typeof(short), DbType.Int16)]
    [DataRow(typeof(ushort), DbType.UInt16)]
    [DataRow(typeof(int), DbType.Int32)]
    [DataRow(typeof(uint), DbType.UInt32)]
    [DataRow(typeof(long), DbType.Int64)]
    [DataRow(typeof(ulong), DbType.UInt64)]
    [DataRow(typeof(float), DbType.Single)]
    [DataRow(typeof(double), DbType.Double)]
    [DataRow(typeof(decimal), DbType.Decimal)]
    [DataRow(typeof(Guid), DbType.Guid)]
    [DataRow(typeof(DateTime), DbType.DateTime)]
    [DataRow(typeof(DateOnly), DbType.Date)]
    [DataRow(typeof(TimeOnly), DbType.Time)]
    [DataRow(typeof(DateTimeOffset), DbType.DateTimeOffset)]
    [DataRow(typeof(TimeSpan), DbType.Time)]
    [DataRow(typeof(byte[]), DbType.Binary)]
    public void GetDbType_ReturnsMappingForKnownTypes(Type type, DbType expected)
    {
        DbType dbType = DataUtils.GetDbType(type);

        Assert.AreEqual(expected, dbType);
    }

    /// <summary>
    /// Verifies nullable types are resolved to the underlying <see cref="DbType"/>.
    /// </summary>
    [TestMethod]
    public void GetDbType_HandlesNullableTypes()
    {
        DbType dbType = DataUtils.GetDbType(typeof(int?));

        Assert.AreEqual(DbType.Int32, dbType);
    }

    /// <summary>
    /// Ensures unmapped types fall back to <see cref="DbType.Object"/>.
    /// </summary>
    [TestMethod]
    public void GetDbType_ReturnsObjectForUnknownTypes()
    {
        DbType dbType = DataUtils.GetDbType(typeof(DataUtilsTests));

        Assert.AreEqual(DbType.Object, dbType);
    }
}
