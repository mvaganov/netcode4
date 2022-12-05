using System;

namespace MrV {
	public struct Coord {
		public short row, col;

		public Coord(int col, int row) {
			this.col = (short)col;
			this.row = (short)row;
		}

		public int X { get => col; set => col = (short)value; }
		public int Y { get => row; set => row = (short)value; }

		public static readonly Coord Zero = new Coord(0, 0);
		public static readonly Coord One = new Coord(1, 1);
		public static readonly Coord Two = new Coord(2, 2);
		public static readonly Coord Up = new Coord(0, -1);
		public static readonly Coord Left = new Coord(-1, 0);
		public static readonly Coord Down = new Coord(0, 1);
		public static readonly Coord Right = new Coord(1, 0);
		public static Coord[] CardinalDirections = new Coord[] { Up, Left, Down, Right };
		public override string ToString() => "(" + col + "," + row + ")";
		public override int GetHashCode() => row * 0x00010000 + col;
		public override bool Equals(object o) {
			return (o == null || o.GetType() != typeof(Coord)) ? false : Equals((Coord)o);
		}
		public bool Equals(Coord c) => row == c.row && col == c.col;

		public static bool operator ==(Coord a, Coord b) => a.Equals(b);
		public static bool operator !=(Coord a, Coord b) => !a.Equals(b);
		public static Coord operator +(Coord a, Coord b) => new Coord(a.col + b.col, a.row + b.row);
		public static Coord operator *(Coord a, Coord b) => new Coord(a.col * b.col, a.row * b.row);
		public static Coord operator -(Coord a, Coord b) => new Coord(a.col - b.col, a.row - b.row);
		public static Coord operator -(Coord a) => new Coord(-a.col, -a.row);

		public Coord Scale(Coord scale) { col *= scale.col; row *= scale.row; return this; }
		public Coord InverseScale(Coord scale) { col /= scale.col; row /= scale.row; return this; }

		/// <param name="min">inclusive starting point</param>
		/// <param name="max">exclusive limit</param>
		/// <returns>if this is within the given range</returns>
		public bool IsWithin(Coord min, Coord max) {
			return row >= min.row && row < max.row && col >= min.col && col < max.col;
		}

		/// <param name="max">exclusive limit</param>
		/// <returns>IsWithin(<see cref="Coord.Zero"/>, max)</returns>
		public bool IsWithin(Coord max) => IsWithin(Zero, max);

		public bool IsGreaterThan(Coord other) => col > other.col && row > other.row;
		public bool IsGreaterThanOrEqualTo(Coord other) => col >= other.col && row >= other.row;

		public void Clamp(Coord min, Coord max) {
			col = (col < min.col) ? min.col : (col > max.col) ? max.col : col;
			row = (row < min.row) ? min.row : (row > max.row) ? max.row : row;
		}

		public static Coord SizeOf(Array map) {
			return new Coord { row = (short)map.GetLength(0), col = (short)map.GetLength(1) };
		}

		/// <summary>
		/// use in a do-while, since it increments
		/// </summary>
		/// <param name="max"></param>
		/// <param name="mincol"></param>
		/// <returns></returns>
		public bool Iterate(Coord max, short mincol = 0) {
			if (++col >= max.col) {
				if (++row >= max.row) { return false; }
				col = mincol;
			}
			return true;
		}
		public static void ForEach(Coord min, Coord max, Action<Coord> action) {
			Coord cursor = min;
			for (cursor.row = min.row; cursor.row < max.row; ++cursor.row) {
				for (cursor.col = min.col; cursor.col < max.col; ++cursor.col) {
					action(cursor);
				}
			}
		}

		public void ForEach(Action<Coord> action) => ForEach(Zero, this, action);

		/// <summary>
		/// stops iterating as soon as action returns true
		/// </summary>
		/// <param name="action">runs till the first return true</param>
		/// <returns>true if action returned true even once</returns>
		public static bool ForEach(Coord min, Coord max, Func<Coord, bool> action) {
			Coord cursor = min;
			for (cursor.row = min.row; cursor.row < max.row; ++cursor.row) {
				for (cursor.col = min.col; cursor.col < max.col; ++cursor.col) {
					if (action(cursor)) { return true; }
				}
			}
			return false;
		}

		public bool ForEach(Func<Coord, bool> action) => ForEach(Zero, this, action);

		public static void ForEachInclusive(Coord start, Coord end, Action<Coord> action) {
			bool colIncrease = start.col < end.col, rowIncrease = start.row < end.row;
			Coord cursor = start;
			do {
				cursor.col = start.col;
				do {
					action(cursor);
					if (cursor.col == end.col || (colIncrease ? cursor.col > end.col : cursor.col < end.col)) { break; }
					if (colIncrease) { ++cursor.col; } else { --cursor.col; }
				} while (true);
				if (cursor.row == end.row || (rowIncrease ? cursor.row > end.row : cursor.row < end.row)) { break; }
				if (rowIncrease) { ++cursor.row; } else { --cursor.row; }
			} while (true);
		}

		public static int ManhattanDistance(Coord a, Coord b) {
			Coord delta = b - a;
			return Math.Abs(delta.col) + Math.Abs(delta.row);
		}

		public void SetCursorPosition() => Console.SetCursorPosition(col, row);
		public static Coord GetCursorPosition() => new Coord(Console.CursorLeft, Console.CursorTop);

		public short this[int i] {
			get { switch (i) {
				case 0: return col;
				case 1: return row;
				default: throw new Exception("must be 0 or 1, not " + i);
				}
			}
			set { switch (i) {
				case 0: col = value; break;
				case 1: row = value; break;
				default: throw new Exception("must be 0 or 1, not " + i);
				}
			}
		}
	}

	public static class MatrixCoordExtension {
		public static Coord GetSize<TYPE>(this TYPE[,] matrix) {
			return new Coord(matrix.GetLength(1), matrix.GetLength(0));
		}
		public static TYPE At<TYPE>(this TYPE[,] matrix, Coord coord) {
			return matrix[coord.row, coord.col];
		}
		public static void SetAt<TYPE>(this TYPE[,] matrix, Coord position, TYPE value) {
			matrix[position.row, position.col] = value;
		}
		public static void SetAt<TYPE>(this TYPE[,] matrix, Coord position, Coord size, TYPE value) {
			Coord cursor;
			for (cursor.row = 0; cursor.row < size.row; ++cursor.row) {
				for (cursor.col = 0; cursor.col < size.col; ++cursor.col) {
					matrix.SetAt(cursor + position, value); ;
				}
			}
		}
		public static void Fill<TYPE>(this TYPE[,] matrix, TYPE value) {
			SetAt(matrix, Coord.Zero, matrix.GetSize(), value);
		}
	}
}
