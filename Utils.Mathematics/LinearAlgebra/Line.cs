using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Mathematics.LinearAlgebra
{
    public class Line<T> : IFormattable, IEquatable<Line<T>>, ICloneable
        where T : struct, IFloatingPoint<T>, IPowerFunctions<T>, ITrigonometricFunctions<T>, IRootFunctions<T>
    {
        public Vector<T> Point { get; }

        public Vector<T> Direction { get; }

        public int Dimension => Point.Dimension;

        public Line(Vector<T> point, Vector<T> direction)
        {
            if (point.Dimension != direction.Dimension)
                throw new ArgumentException("point and direction must be of the same dimension");


            Point = point;
            Direction = direction;
        }

        public T DistanceTo(Vector<T> point)
        {
            if (point.Dimension != this.Point.Dimension)
                throw new ArgumentException("Tous les vecteurs doivent avoir la même dimension.");

            // Calculer le vecteur PQ
            var pq = point - this.Point;

            // Projeter PQ sur la direction
            T t = (pq * this.Direction) / (this.Direction * this.Direction);
            var projection = t * this.Direction;

            // Calculer le vecteur de la projection de PQ sur la droite
            var closestPoint = this.Point + projection;

            // Calculer la distance entre le point original et le point projeté
            var distanceVector = point - closestPoint;
            return distanceVector.Norm;
        }

        public object Clone() => new Line<T> (new Vector<T>(Point), new Vector<T>(Direction));

        public bool Equals(Line<T>? other)
            => other is not null && Point.Equals(other.Point) && Direction.Equals(other.Direction);

        public override bool Equals(object? other)
            => other switch
            {
                Line<T> line => Equals(line),
                _ => false
            };

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            throw new NotImplementedException();
        }
    }
}
