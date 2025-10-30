using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.OData;
using Utils.OData.Linq;

namespace UtilsTest.OData;

/// <summary>
/// Validates the initial LINQ-to-OData compilation pipeline.
/// </summary>
[TestClass]
public class ODataLinqCompilationTests
{
    /// <summary>
    /// Ensures a simple equality filter is compiled into an OData $filter clause.
    /// </summary>
    [TestMethod]
    public void CompileWhereClauseProducesEqualityFilter()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        var query = context.Products.Where(p => p.Id == 5);
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        Assert.AreEqual("Products?$filter=Id eq 5", compilation.ToUriString());
        Assert.AreEqual(1, compilation.Filters.Count);
        Assert.AreEqual("Id eq 5", compilation.Filters[0]);
    }

    /// <summary>
    /// Ensures multiple predicates are combined using logical AND operators.
    /// </summary>
    [TestMethod]
    public void CompileWhereClauseWithMultiplePredicates()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        string category = "Hardware";
        var query = context.Products.Where(p => p.Price > 10 && p.CategoryName == category);
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        Assert.AreEqual("Products?$filter=(Price gt 10) and (CategoryName eq 'Hardware')", compilation.ToUriString());
        Assert.AreEqual(1, compilation.Filters.Count);
        Assert.AreEqual("(Price gt 10) and (CategoryName eq 'Hardware')", compilation.Filters[0]);
    }

    /// <summary>
    /// Ensures filters can target properties of related entities through navigation paths.
    /// </summary>
    [TestMethod]
    public void CompileWhereClauseWithNavigationProperty()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        var query = context.Products.Where(p => p.Category.Title == "Tools");
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        Assert.AreEqual("Products?$filter=Category/Title eq 'Tools'", compilation.ToUriString());
        Assert.AreEqual(1, compilation.Filters.Count);
        Assert.AreEqual("Category/Title eq 'Tools'", compilation.Filters[0]);
    }

    /// <summary>
    /// Ensures deeply nested navigation paths are preserved within compiled filters.
    /// </summary>
    [TestMethod]
    public void CompileWhereClauseWithDeepNavigationProperty()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        var query = context.Products.Where(p => p.Supplier.Address.City == "Paris");
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        Assert.AreEqual("Products?$filter=Supplier/Address/City eq 'Paris'", compilation.ToUriString());
        Assert.AreEqual(1, compilation.Filters.Count);
        Assert.AreEqual("Supplier/Address/City eq 'Paris'", compilation.Filters[0]);
    }

    /// <summary>
    /// Ensures queries can be composed against entity sets without predefined CLR types.
    /// </summary>
    [TestMethod]
    public void CompileWhereClauseFromUntypedTable()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        string category = "Hardware";
        var query = context.Table("Products")
            .Where(row => row.GetValue<decimal>("Price") > 10 && row.GetValue<string>("CategoryName") == category);
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        Assert.AreEqual("Products?$filter=(Price gt 10) and (CategoryName eq 'Hardware')", compilation.ToUriString());
        Assert.AreEqual(1, compilation.Filters.Count);
        Assert.AreEqual("(Price gt 10) and (CategoryName eq 'Hardware')", compilation.Filters[0]);
    }

    /// <summary>
    /// Ensures expansions are captured and emitted when requested on typed entity sets.
    /// </summary>
    [TestMethod]
    public void CompileExpandClauseIncludesExpandOption()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        var query = context.Products
            .Expand("Category", "Supplier")
            .Where(p => p.Id == 42);
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        CollectionAssert.AreEqual(new[] { "Category", "Supplier" }, compilation.Expansions.ToArray());
        Assert.AreEqual("Products?$expand=Category,Supplier&$filter=Id eq 42", compilation.ToUriString());
    }

    /// <summary>
    /// Ensures expansions are available for untyped table queries.
    /// </summary>
    [TestMethod]
    public void CompileExpandFromUntypedTableIncludesExpandOption()
    {
        string metadataPath = GetSampleMetadataPath();
        var context = new QueryableContext(metadataPath);

        var query = context.Table("Products")
            .Expand("Category")
            .Where(row => row.GetValue<int>("Id") == 7);
        var compilation = query.CompileToODataQuery();

        Assert.AreEqual("Products", compilation.EntitySetName);
        CollectionAssert.AreEqual(new[] { "Category" }, compilation.Expansions.ToArray());
        Assert.AreEqual("Products?$expand=Category&$filter=Id eq 7", compilation.ToUriString());
    }

    /// <summary>
    /// Resolves the absolute path to the sample EDMX metadata used by the tests.
    /// </summary>
    /// <returns>The full file path to <c>Sample.edmx</c>.</returns>
    private static string GetSampleMetadataPath()
    {
        string? baseDirectory = AppContext.BaseDirectory;
        if (baseDirectory is null)
        {
            throw new InvalidOperationException("Unable to resolve the base directory of the test run.");
        }

        string path = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "OData", "TestData", "Sample.edmx"));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Sample EDMX metadata file not found for tests.", path);
        }

        return path;
    }

    /// <summary>
    /// Concrete context used to expose queryable entity sets for the tests.
    /// </summary>
    private sealed class QueryableContext : ODataContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryableContext"/> class.
        /// </summary>
        /// <param name="path">Path to the EDMX metadata file.</param>
        public QueryableContext(string path)
            : base(path)
        {
        }

        /// <summary>
        /// Gets a queryable sequence that targets the Products entity set.
        /// </summary>
        public ODataQueryable<Product> Products => Query<Product>("Products");
    }

    /// <summary>
    /// Represents the product entity used by the tests.
    /// </summary>
    public sealed class Product
    {
        /// <summary>
        /// Gets or sets the identifier of the product.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the product category name.
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// Gets or sets the navigation property referencing the product category entity.
        /// </summary>
        public CategoryInfo Category { get; set; } = new CategoryInfo();

        /// <summary>
        /// Gets or sets the navigation property referencing the product supplier entity.
        /// </summary>
        public SupplierInfo Supplier { get; set; } = new SupplierInfo();

        /// <summary>
        /// Gets or sets the product price.
        /// </summary>
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Represents the category entity referenced by <see cref="Product"/> instances.
    /// </summary>
    public sealed class CategoryInfo
    {
        /// <summary>
        /// Gets or sets the category title.
        /// </summary>
        public string? Title { get; set; }
    }

    /// <summary>
    /// Represents the supplier entity referenced by <see cref="Product"/> instances.
    /// </summary>
    public sealed class SupplierInfo
    {
        /// <summary>
        /// Gets or sets the nested supplier address information.
        /// </summary>
        public SupplierAddress Address { get; set; } = new SupplierAddress();
    }

    /// <summary>
    /// Represents the supplier address structure used to test deep navigation paths.
    /// </summary>
    public sealed class SupplierAddress
    {
        /// <summary>
        /// Gets or sets the supplier city.
        /// </summary>
        public string? City { get; set; }
    }
}
