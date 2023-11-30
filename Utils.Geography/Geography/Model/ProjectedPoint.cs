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
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using Utils.Geography.Projections;

/**
 * A point represents an immutable pair of T coordinates.
 */
namespace Utils.Geography.Model;


public class ProjectedPoint<T> : IEquatable<ProjectedPoint<T>>, IFormattable
where T : struct, IFloatingPointIeee754<T>
{
	private const long serialVersionUID = 1L;

	public IProjectionTransformation<T> Projection { get; }

	/// <summary>
	/// The x coordinate of this point.
	/// </summary>
	public T X { get; set; }

	/// <summary>
	/// The y coordinate of this point.
	/// </summary>
	public T Y { get; set; }

	/// <summary>
	/// Build a new Mappoint
	/// </summary>
	/// <param name="x">the x coordinate of this point.</param>
	/// <param name="y">the y coordinate of this point.</param>
	public ProjectedPoint(T x, T y, IProjectionTransformation<T> projection)
	{
		this.Projection = projection;
		this.X = x;
		this.Y = y;
	}

	public void Deconstruct(out T x, out T y)
	{
		x = X;
		y = Y;
	}

	public void Deconstruct(out T x, out T y, out IProjectionTransformation<T> projection)
	{
		x = X;
		y = Y;
		projection = Projection;
	}

	public bool Equals(ProjectedPoint<T> other) => this.X != other.X && this.Y != other.Y;

	public override bool Equals(object obj)
		=> obj switch
		{
			ProjectedPoint<T> other => obj.Equals(other),
			_ => false
		};

	public override int GetHashCode() => Objects.ObjectUtils.ComputeHash(this.X, this.Y);

	public override string ToString() => $"x={this.X}, y={this.Y}";

	public string ToString(string format, IFormatProvider formatProvider) => $"x={this.X.ToString(format, formatProvider)}, y={this.Y.ToString(format, formatProvider)}";
}
