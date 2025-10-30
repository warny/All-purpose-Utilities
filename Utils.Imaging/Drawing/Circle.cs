using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Utils.Collections;

namespace Utils.Drawing
{
    /// <summary>
    /// Represents an ellipse or circle drawable approximation that can be converted into
    /// points or segments.
    /// </summary>
    public class Circle : IDrawable
    {
        /// <summary>
        /// Gets the perimeter length of the computed polyline representation.
        /// </summary>
        public float Length
        {
            get
            {
                ComputeLines();
                return polylines.Length;
            }
        }

        /// <summary>
        /// Gets the geometric center of the circle or ellipse.
        /// </summary>
        public PointF Center { get; }

        /// <summary>
        /// Gets the rotation of the ellipse around its center in radians.
        /// </summary>
        public double Orientation { get; }

        /// <summary>
        /// Gets the first radius of the ellipse. When equal to <see cref="Radius2"/> it
        /// represents the radius of a circle.
        /// </summary>
        public float Radius1 { get; }

        /// <summary>
        /// Gets the second radius of the ellipse.
        /// </summary>
        public float Radius2 { get; }

        /// <summary>
        /// Gets the angle, in radians, at which the arc starts.
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// Gets the angle, in radians, at which the arc ends.
        /// </summary>
        public double EndAngle { get; }

        private Polylines polylines;

        /// <summary>
        /// Initializes a new instance of the <see cref="Circle"/> class representing an
        /// arc of a circle.
        /// </summary>
        /// <param name="center">Center of the circle.</param>
        /// <param name="radius">Radius of the circle.</param>
        /// <param name="startAngle">Starting angle of the arc in radians.</param>
        /// <param name="endAngle">Ending angle of the arc in radians.</param>
        public Circle(PointF center, float radius, double startAngle = 0, double endAngle = Math.PI * 2)
                : this(center, radius, radius, 0, startAngle, endAngle)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Circle"/> class representing an
        /// ellipse arc.
        /// </summary>
        /// <param name="center">Center of the ellipse.</param>
        /// <param name="radius1">Radius along the first axis.</param>
        /// <param name="radius2">Radius along the second axis.</param>
        /// <param name="orientation">Rotation of the ellipse in radians.</param>
        /// <param name="startAngle">Starting angle of the arc in radians.</param>
        /// <param name="endAngle">Ending angle of the arc in radians.</param>
        public Circle(PointF center, float radius1, float radius2, double orientation = 0, double startAngle = 0, double endAngle = Math.PI * 2)
        {
            Center = center;
            Radius1 = radius1;
            Radius2 = radius2;
            Orientation = orientation;

            double angle = endAngle - startAngle;
            if (Math.Abs(angle) > Math.PI * 2)
            {
                startAngle = 0;
                endAngle = Math.PI * 2;
            }
            StartAngle = startAngle;
            EndAngle = endAngle;
        }

        /// <summary>
        /// Generates the polyline approximation of the ellipse if it has not been created yet.
        /// </summary>
        private void ComputeLines()
        {
            polylines ??= new Polylines(ComputePoints().ToArray());
        }

        /// <summary>
        /// Lazily computes the points that describe the ellipse.
        /// </summary>
        /// <returns>Sequence of points describing the ellipse.</returns>
        private IEnumerable<PointF> ComputePoints()
        {
            double angle = EndAngle - StartAngle;
            var angularResolution = angle / (Math.Max(Radius1, Radius2) * Math.PI * 2);

            var cosR = Math.Cos(Orientation);
            var sinR = Math.Sin(Orientation);

            Func<double, bool> test;
            if (StartAngle > EndAngle)
            {
                test = alpha => alpha >= EndAngle;
            }
            else
            {
                test = alpha => alpha <= EndAngle;
            }

            double delta1 = Math.Sin(StartAngle) * Radius1;
            double delta2 = Math.Cos(StartAngle) * Radius2;

            int deltaX = (int)(cosR * delta1 + sinR * delta2);
            int deltaY = (int)(sinR * delta1 + cosR * delta2);

            PointF lastPoint = new PointF(Center.X + deltaX, Center.Y + deltaY);
            yield return lastPoint;

            for (double a = StartAngle + angularResolution; test(a); a += angularResolution)
            {
                delta1 = Math.Sin(a) * Radius1;
                delta2 = Math.Cos(a) * Radius2;

                deltaX = (int)(cosR * delta1 + sinR * delta2);
                deltaY = (int)(sinR * delta1 + cosR * delta2);

                var newPoint = new PointF(Center.X + deltaX, Center.Y + deltaY);
                if (newPoint.X == lastPoint.X && newPoint.Y == lastPoint.Y)
                {
                    continue;
                }

                lastPoint = newPoint;
                yield return newPoint;
            }
        }

        /// <summary>
        /// Returns oriented points along the ellipse arc.
        /// </summary>
        /// <param name="closed">Indicates whether the sequence should be closed.</param>
        /// <param name="position">Starting accumulated length value for the first point.</param>
        /// <returns>Enumeration of oriented points.</returns>
        public IEnumerable<DrawPoint> GetPoints(bool closed, float position = 0)
        {
            ComputeLines();
            return polylines.GetPoints(closed, position);
        }

        /// <summary>
        /// Returns segments describing the ellipse arc.
        /// </summary>
        /// <param name="closed">Indicates whether the returned segments should form a closed path.</param>
        /// <returns>Enumeration of segments representing the ellipse.</returns>
        public IEnumerable<Segment> GetSegments(bool closed)
        {
            return polylines.GetSegments(closed);
        }
    }
}
