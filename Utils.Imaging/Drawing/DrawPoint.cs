namespace Utils.Drawing
{
    /// <summary>
    /// Represents a sampled point produced by a drawing algorithm.
    /// </summary>
    /// <remarks>
    /// Instances carry both spatial information and the derivative of the drawing trajectory.  The directional
    /// metadata is required by flood-fill and polygon filling routines to determine winding and sweep direction.
    /// </remarks>
    public class DrawPoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawPoint"/> class.
        /// </summary>
        /// <param name="x">Horizontal coordinate.</param>
        /// <param name="y">Vertical coordinate.</param>
        /// <param name="horizontalDirection">Horizontal direction of the drawing step.</param>
        /// <param name="verticalDirection">Vertical direction of the drawing step.</param>
        /// <param name="sin">Sine of the drawing direction.</param>
        /// <param name="cos">Cosine of the drawing direction.</param>
        /// <param name="position">Normalized position along the drawing.</param>
        /// <remarks>
        /// The sine and cosine values are precomputed so that consumers can derive tangents without recomputing
        /// trigonometric functions for every pixel.
        /// </remarks>
        public DrawPoint(int x, int y, int horizontalDirection, int verticalDirection, float sin, float cos, float position)
        {
            X = x;
            Y = y;
            HorizontalDirection = horizontalDirection;
            VerticalDirection = verticalDirection;
            Sin = sin;
            Cos = cos;
            Position = position;
        }

        /// <summary>
        /// Gets the horizontal coordinate.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the vertical coordinate.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the horizontal direction of the drawing step.
        /// </summary>
        public int HorizontalDirection { get; }

        /// <summary>
        /// Gets the vertical direction of the drawing step.
        /// </summary>
        public int VerticalDirection { get; }

        /// <summary>
        /// Gets the sine of the drawing direction.
        /// </summary>
        public float Sin { get; }

        /// <summary>
        /// Gets the cosine of the drawing direction.
        /// </summary>
        public float Cos { get; }

        /// <summary>
        /// Gets the normalized position along the drawing.
        /// </summary>
        public float Position { get; }

        /// <summary>
        /// Returns a textual representation of the point.
        /// </summary>
        /// <returns>Text describing the position and directions.</returns>
        public override string ToString()
        {
            string vDir, hDir;
            switch (HorizontalDirection)
            {
                case -1:
                    hDir = "◄";
                    break;
                case 1:
                    hDir = "►";
                    break;
                default:
                    hDir = " ";
                    break;
            }
            switch (VerticalDirection)
            {
                case -1:
                    vDir = "▲";
                    break;
                case 1:
                    vDir = "▼";
                    break;
                default:
                    vDir = " ";
                    break;
            }

            return $"X={X},Y={Y} (P={Position}{hDir}{vDir})";
        }
    }
}
