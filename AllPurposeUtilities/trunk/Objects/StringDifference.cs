using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public class StringDifference : IEnumerable<StringChange>
	{

		private readonly StringChange[] changes;

		public StringDifference( string old, string @new )
		{
			//int lengthStart;
			//for (
			//	lengthStart = 0 ;
			//	lengthStart < old.Length && lengthStart < @new.Length && old[lengthStart] == @new[lengthStart] ;
			//	lengthStart++) ;
			//
			//if (lengthStart == @new.Length && lengthStart == old.Length) {
			//	changes.Add(new StringChange(StringComparisonStatus.Unchanged, old));
			//	return;
			//} else if (lengthStart == @new.Length) {
			//	changes.Add(new StringChange(StringComparisonStatus.Unchanged, @new));
			//	changes.Add(new StringChange(StringComparisonStatus.Removed, old.Substring(lengthStart)));
			//	return;
			//} else if (lengthStart == old.Length) {
			//	changes.Add(new StringChange(StringComparisonStatus.Unchanged, old));
			//	changes.Add(new StringChange(StringComparisonStatus.Added, @new.Substring(lengthStart)));
			//	return;
			//}
			//
			//AddChange(old, @new, StringComparisonStatus.Unchanged, 0, lengthStart, 0);
			//if (lengthStart > 0) {
			//	changes.Add(new StringChange(StringComparisonStatus.Unchanged, old.Substring(0, lengthStart)));
			//}

			//int lengthEnd;
			//for (
			//	lengthEnd = 0 ;
			//	lengthEnd < old.Length - lengthStart && lengthEnd < @new.Length - lengthStart && old[old.Length - lengthEnd - 1] == @new[@new.Length - lengthEnd - 1] ;
			//	lengthEnd++) ;

			//Compare(old, @new, lengthStart, lengthEnd);
			changes = Compare(old, @new, 0, 0);

			//if (lengthEnd > 0) {
			//	changes.Add(new StringChange(StringComparisonStatus.Unchanged, old.Substring(old.Length - lengthEnd, lengthEnd)));
			//}

		}

		private StringChange[] Compare( string old, string @new, int lengthStart, int lengthEnd )
		{
			List<StringChange> changes = new List<StringChange>();

			int[,] lengths = new int[old.Length+1, @new.Length+1];

			// row 0 and column 0 are initialized to 0 already

			for (int i = lengthStart ; i < old.Length - lengthEnd ; i++)
				for (int j = lengthStart ; j < @new.Length  - lengthEnd ; j++)
					if (old[i] == @new[j])
						lengths[i+1, j+1] = lengths[i, j] + 1;
					else
						lengths[i+1, j+1] = Math.Max(lengths[i+1, j], lengths[i, j+1]);

			// read the substring out from the matrix
			StringComparisonStatus currentStatus = StringComparisonStatus.Unchanged;
			int previousStatePosition = old.Length - lengthEnd;
			int oldPosition, newPosition;
			for (oldPosition = old.Length - lengthEnd, newPosition = @new.Length - lengthEnd ;
				   oldPosition > lengthStart && newPosition != lengthStart ;) {
				if (lengths[oldPosition, newPosition] == lengths[oldPosition-1, newPosition]) {
					if (currentStatus != StringComparisonStatus.Removed) {
						AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
						previousStatePosition = oldPosition;
						currentStatus = StringComparisonStatus.Removed;
					}
					oldPosition--;
				} else if (lengths[oldPosition, newPosition] == lengths[oldPosition, newPosition-1]) {
					if (currentStatus != StringComparisonStatus.Added) {
						AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
						previousStatePosition = newPosition;
						currentStatus = StringComparisonStatus.Added;
					}
					newPosition--;
				} else {
					if (old[oldPosition-1] != @new[newPosition-1]) {
						throw new Exception($"oops.. something got wrong in method {nameof(Compare)}!");
					}
					if (currentStatus != StringComparisonStatus.Unchanged) {
						AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);
						previousStatePosition = oldPosition;
						currentStatus = StringComparisonStatus.Unchanged;
					}

					oldPosition--;
					newPosition--;
				}
			}

			AddChange(changes, old, @new, currentStatus, previousStatePosition, oldPosition, newPosition);

			if (newPosition > 0) {
				AddChange(changes, old, @new, StringComparisonStatus.Added, newPosition, oldPosition, 0);
			}
			if (oldPosition > 0) {
				AddChange(changes, old, @new, StringComparisonStatus.Removed, oldPosition, 0, oldPosition);
			}

			changes.Reverse();
			return changes.ToArray();
		}

		private void AddChange( List<StringChange> changes, string old, string @new, StringComparisonStatus currentStatus, int previousStatePosition, int oldPosition, int newPosition )
		{
			string change = currentStatus == StringComparisonStatus.Added ? @new.Substring(newPosition, previousStatePosition - newPosition) : old.Substring(oldPosition, previousStatePosition - oldPosition);
			if (change.Length > 0) {
				changes.Add(new StringChange(currentStatus, change));
			}
		}

		public IEnumerator<StringChange> GetEnumerator()
		{
			return ((IEnumerable <StringChange>)changes).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return changes.GetEnumerator();
		}
	}

	public enum StringComparisonStatus
	{
		Removed = -1,
		Unchanged = 0,
		Added = 1
	}

	public class StringChange
	{
		internal StringChange( StringComparisonStatus status, string @string )
		{
			this.Status = status;
			this.String = @string;
		}

		public StringComparisonStatus Status { get; }
		public string String { get; }
	}

}
