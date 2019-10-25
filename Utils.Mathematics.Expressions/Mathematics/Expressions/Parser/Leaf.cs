using System.Collections.Generic;

namespace Utils.Mathematics.Expressions.Parser
{
	public class Leaf
	{
		public Result Result { get; set; }
		public Context Context { get; }
		public Rule Rule { get; }

		public string Name { get; private set; }
		public Leaf Parent { get; private set; }
		
		private readonly List<Leaf> subTrees = new List<Leaf>();
		public IReadOnlyList<Leaf> SubTrees { get => subTrees; }

		public Leaf(Leaf parent, string name)
		{
			this.Parent = parent;
			this.Name = name;
		}

		public Leaf SubLeafCurrentLeaf(string name)
		{
			var newLeaf = new Leaf(this.Parent, name);
			this.Parent = newLeaf;
			return newLeaf;
		}
	}
}