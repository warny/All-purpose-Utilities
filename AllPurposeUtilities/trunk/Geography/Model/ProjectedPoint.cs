/*
 * Copyright 2010, 2011, 2012 mapsforge.org
 *
 * This program is free software: you can redistribute it and/or modify it under the
 * terms of the GNU Lesser General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
 * PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License along with
 * this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.Text;
using Utils.Geography.Projections;

/**
 * A point represents an immutable pair of double coordinates.
 */
namespace Utils.Geography.Model
{

	public class ProjectedPoint : IEquatable<ProjectedPoint> /*, ISerializable*/ {
		private const long serialVersionUID = 1L;

		public IProjectionTransformation Projection { get; }

		/// <summary>
		/// The x coordinate of this point.
		/// </summary>
		public double X { get; set; }

		/// <summary>
		/// The y coordinate of this point.
		/// </summary>
		public double Y { get; set; }

		/// <summary>
		/// Build a new Mappoint
		/// </summary>
		/// <param name="x">the x coordinate of this point.</param>
		/// <param name="y">the y coordinate of this point.</param>
		public ProjectedPoint ( double x, double y, IProjectionTransformation projection)
		{
			this.Projection = projection;
			this.X = x;
			this.Y = y;
		}

		public bool Equals( ProjectedPoint point )
		{
			if (this.X != point.X) {
				return false;
			} else if (this.Y != point.Y) {
				return false;
			}
			return true;
		}

		public override bool Equals ( object obj )
		{
			if (this == obj) {
				return true;
			} else if (!(obj is ProjectedPoint)) {
				return false;
			}
			ProjectedPoint other = (ProjectedPoint)obj;
			return this.Equals (other);
		}

		public override int GetHashCode ()
		{
			const int prime = 31;
			int result = 1;
			long temp;
			temp = BitConverter.DoubleToInt64Bits(this.X);
			result = prime * result + (int)(temp ^ (temp >> 32));
			temp = BitConverter.DoubleToInt64Bits(this.Y);
			result = prime * result + (int)(temp ^ (temp >> 32));
			return result;
		}

		public override string ToString ()
		{
			return string.Format("x={0}, y={1}", this.X, this.Y);
		}
	}
}
