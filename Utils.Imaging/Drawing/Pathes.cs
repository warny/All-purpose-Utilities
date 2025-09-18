using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Drawing
{
        /// <summary>
        /// Represents a simple collection of drawable paths.
        /// </summary>
        public class Pathes : List<IDrawable>
        {
                /// <summary>
                /// Initializes a new, empty collection of drawables.
                /// </summary>
                public Pathes() { }

                /// <summary>
                /// Initializes the collection with the provided drawables.
                /// </summary>
                /// <param name="drawables">The initial drawables.</param>
                public Pathes(ICollection<IDrawable> drawables) : base(drawables) { }

                /// <summary>
                /// Initializes the collection with the provided drawables.
                /// </summary>
                /// <param name="drawables">The initial drawables.</param>
                public Pathes(params IDrawable[] drawables) : base(drawables) { }

                /// <summary>
                /// Adds a range of drawables to the collection.
                /// </summary>
                /// <param name="drawables">The drawables to append.</param>
                public void AddRange(params IDrawable[] drawables)
                {
                        AddRange((IEnumerable<IDrawable>)drawables);
                }
        }
}
