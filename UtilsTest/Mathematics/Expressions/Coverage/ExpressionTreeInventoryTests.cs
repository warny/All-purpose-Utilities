using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UtilsTest.Mathematics.Expressions.Coverage;

/// <summary>
/// ETC R0: programmatic completeness checks of the expression-tree inventory against the running framework. These
/// tests fail when the runtime exposes a node type, public class, public property or visitor entry point that the
/// ETC roadmap (<c>Utils/TODO-2026-10-27-etc-expression-tree-coverage-roadmap.md</c>) has not classified.
/// </summary>
[TestClass]
[TestCategory("ETC-R0")]
public class ExpressionTreeInventoryTests
{
    /// <summary>Every <see cref="ExpressionType"/> value has exactly one primary catalog sample whose root has that node type.</summary>
    [TestMethod]
    public void EveryExpressionType_HasExactlyOnePrimarySample()
    {
        var missing = new List<string>();
        foreach (ExpressionType nodeType in Enum.GetValues<ExpressionType>())
        {
            int count = ExpressionNodeCatalog.All.Count(sample => sample.IsPrimary && sample.RootNodeType == nodeType);
            if (count != 1) missing.Add($"{nodeType}: {count}");
        }

        Assert.AreEqual(0, missing.Count, "Node types without exactly one primary sample: " + string.Join(", ", missing));
    }

    /// <summary>Every sample's root really has its declared node type, and sample names are unique.</summary>
    [TestMethod]
    public void EverySample_RootNodeTypeMatchesDeclaration_AndNamesAreUnique()
    {
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All)
        {
            Assert.AreEqual(sample.RootNodeType, sample.Build().NodeType, sample.Name);
        }

        Assert.AreEqual(ExpressionNodeCatalog.All.Count, ExpressionNodeCatalog.All.Select(sample => sample.Name).Distinct().Count());
    }

    /// <summary>Building a sample twice yields reference-distinct roots, so equality tests compare independent trees.</summary>
    [TestMethod]
    public void EverySample_BuildsIndependentTrees()
    {
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All)
        {
            // The bare parameter sample is the shared free parameter by construction.
            if (sample.RootNodeType == ExpressionType.Parameter) continue;
            Assert.AreNotSame(sample.Build(), sample.Build(), sample.Name);
        }
    }

    /// <summary>Every <see cref="ExpressionType"/> value is mapped to a public class, and every primary sample's root is that class.</summary>
    [TestMethod]
    public void EveryExpressionType_IsMappedToItsPublicClass()
    {
        CollectionAssert.AreEquivalent(Enum.GetValues<ExpressionType>(), ExpressionNodeInventory.NodeClasses.Keys.ToArray());

        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All.Where(sample => sample.IsPrimary))
        {
            Type expected = ExpressionNodeInventory.NodeClasses[sample.RootNodeType];
            Type actual = ExpressionNodeInventory.PublicClassOf(sample.Build().GetType());
            Assert.AreEqual(expected, actual, sample.Name);
        }
    }

    /// <summary>The inventory lists exactly the public expression-tree classes (nodes and structural objects) of the running framework.</summary>
    [TestMethod]
    public void EveryPublicExpressionTreeClass_IsInventoried()
    {
        string[] discovered = ExpressionNodeInventory.DiscoverPublicClasses().Select(type => type.Name).OrderBy(name => name).ToArray();
        string[] inventoried = ExpressionNodeInventory.Fields.Keys.Select(type => type.Name).OrderBy(name => name).ToArray();

        CollectionAssert.AreEqual(discovered, inventoried, "Discovered: " + string.Join(", ", discovered));
    }

    /// <summary>Every public property declared by every inventoried class has a classified role, and no role refers to a vanished property.</summary>
    [TestMethod]
    public void EveryDeclaredPublicProperty_HasARole()
    {
        var problems = new List<string>();
        foreach ((Type type, IReadOnlyDictionary<string, NodeFieldRole> roles) in ExpressionNodeInventory.Fields)
        {
            var declared = ExpressionNodeInventory.DeclaredPublicProperties(type).ToHashSet();
            problems.AddRange(declared.Where(name => !roles.ContainsKey(name)).Select(name => $"{type.Name}.{name} unclassified"));
            problems.AddRange(roles.Keys.Where(name => !declared.Contains(name)).Select(name => $"{type.Name}.{name} no longer declared"));
        }

        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    /// <summary>Every overridable <c>Visit*</c> entry point of <see cref="ExpressionVisitor"/> targets an inventoried class.</summary>
    [TestMethod]
    public void EveryExpressionVisitorEntryPoint_TargetsAnInventoriedClass()
    {
        Type[] unknown = ExpressionNodeInventory.VisitorVisitedTypes()
            .Where(type => !ExpressionNodeInventory.Fields.ContainsKey(type))
            .ToArray();

        Assert.AreEqual(0, unknown.Length, "Visited but not inventoried: " + string.Join(", ", unknown.Select(type => type.Name)));
    }

    /// <summary>
    /// The catalog exercises every inventoried class (nodes and structural objects), so no class is reported in the
    /// coverage matrix without an executable sample.
    /// </summary>
    [TestMethod]
    public void EveryInventoriedClass_IsExercisedByTheCatalog()
    {
        var collector = new ClassCollector();
        foreach (ExpressionNodeSample sample in ExpressionNodeCatalog.All)
        {
            collector.Visit(sample.Build());
        }

        string[] missing = ExpressionNodeInventory.Fields.Keys
            .Where(type => type != typeof(Expression) && !collector.Seen.Any(seen => Derives(seen, type)))
            .Select(type => type.Name)
            .ToArray();

        Assert.AreEqual(0, missing.Length, "Not exercised: " + string.Join(", ", missing));
    }

    /// <summary>Whether <paramref name="runtimeType"/> is, or derives from, <paramref name="inventoried"/> (an open generic definition included).</summary>
    /// <param name="runtimeType">A runtime class reached by the visitor.</param>
    /// <param name="inventoried">An inventoried class, possibly a generic type definition.</param>
    /// <returns><see langword="true"/> when <paramref name="runtimeType"/> is an instance class of <paramref name="inventoried"/>.</returns>
    private static bool Derives(Type runtimeType, Type inventoried)
    {
        for (Type? type = runtimeType; type is not null; type = type.BaseType)
        {
            if (type == inventoried || (type.IsGenericType && type.GetGenericTypeDefinition() == inventoried)) return true;
        }

        return false;
    }

    /// <summary>Records the runtime classes of every node and structural object reached by <see cref="ExpressionVisitor"/>.</summary>
    private sealed class ClassCollector : ExpressionVisitor
    {
        /// <summary>Gets the runtime classes seen so far.</summary>
        public HashSet<Type> Seen { get; } = [];

        /// <inheritdoc/>
        public override Expression? Visit(Expression? node)
        {
            if (node is not null) Seen.Add(node.GetType());
            return base.Visit(node);
        }

        /// <inheritdoc/>
        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            Seen.Add(node.GetType());
            return base.VisitCatchBlock(node);
        }

        /// <inheritdoc/>
        protected override SwitchCase VisitSwitchCase(SwitchCase node)
        {
            Seen.Add(node.GetType());
            return base.VisitSwitchCase(node);
        }

        /// <inheritdoc/>
        protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
        {
            if (node is not null) Seen.Add(node.GetType());
            return base.VisitLabelTarget(node);
        }

        /// <inheritdoc/>
        protected override MemberBinding VisitMemberBinding(MemberBinding node)
        {
            Seen.Add(node.GetType());
            return base.VisitMemberBinding(node);
        }

        /// <inheritdoc/>
        protected override ElementInit VisitElementInit(ElementInit node)
        {
            Seen.Add(node.GetType());
            return base.VisitElementInit(node);
        }

        /// <inheritdoc/>
        protected override Expression VisitDebugInfo(DebugInfoExpression node)
        {
            Seen.Add(node.Document.GetType());
            return base.VisitDebugInfo(node);
        }
    }
}
