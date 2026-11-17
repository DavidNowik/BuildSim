using System;
using System.Collections.Generic;

/// <summary>
/// What a product can be USED for. Different products share use cases: stone, cut stone, wood and planks can all be
/// "building material". Crates (and later buildings, upgrades...) ask for a tag, never for one specific product, so a
/// player may pay with whatever they have. When the product tree grows: add the new product to ResourceKind and list
/// its tags in <see cref="Products.TagsOf"/>; every consumer picks it up automatically.
/// </summary>
[Flags]
public enum ProductTag
{
    None = 0,
    BuildingMaterial = 1,
    Food = 2,
}

public static class Products
{
    public static ProductTag TagsOf(ResourceKind k)
    {
        switch (k)
        {
            case ResourceKind.Stone: return ProductTag.BuildingMaterial;
            case ResourceKind.Wood: return ProductTag.BuildingMaterial;
            case ResourceKind.Fish: return ProductTag.Food;
            default: return ProductTag.None;
        }
    }

    public static bool Has(ResourceKind k, ProductTag tag) { return (TagsOf(k) & tag) != 0; }

    /// <summary>All products that can be used for a tag, in enum order.</summary>
    public static List<ResourceKind> Accepting(ProductTag tag)
    {
        var list = new List<ResourceKind>();
        foreach (ResourceKind k in Enum.GetValues(typeof(ResourceKind)))
            if (k != ResourceKind.None && Has(k, tag)) list.Add(k);
        return list;
    }

    public static string TagName(ProductTag tag)
    {
        switch (tag) { case ProductTag.BuildingMaterial: return "Building material"; case ProductTag.Food: return "Food"; default: return "Anything"; }
    }
}
