using Utils.OData;

if (typeof(ProductContext.Product).GetProperty("Id") is null)
{
    throw new InvalidOperationException("The OData generator did not emit Product.Id.");
}

Console.WriteLine("odata-generator-executed");

/// <summary>Provides the packaged generator with a local metadata-backed context.</summary>
public partial class ProductContext : ODataContext
{
    /// <summary>Initializes the acceptance context.</summary>
    public ProductContext() : base("Sample.edmx") { }
}
