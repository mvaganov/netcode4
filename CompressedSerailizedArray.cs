using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace _20221123_netcode4 {
	public class CompressedSerailizedFloatArray : CompressedSerailizedArray <float> {
		// if the value changed less than this, don't send the data
		private const float _EPSILON = 0.001f;

		public override bool IsEqual(float a, float b) => Math.Abs(a - b) <= _EPSILON;
		public override float ConvertFromSingle(float n) => n;
		public override float ConvertToSingle(float n) => n;
	}
	public abstract class CompressedSerailizedArray <TYPE> {
		private List<TYPE> _values = new List<TYPE>(), _prevValues = new List<TYPE>();
		private int _index = 0;

		public abstract bool IsEqual(TYPE a, TYPE b);
		public abstract TYPE ConvertFromSingle(float n);
		public abstract float ConvertToSingle(TYPE n);

		public void NewFrame() {
			var temp = _prevValues; _prevValues = _values; _values = temp;
			_values.Clear();
			_index = 0;
		}

		public void NewKeyframe() {
			_values.Clear();
			_prevValues.Clear();
			_index = 0;
		}

		public bool HasValues() => _values.Count > _index;

		public void Add(TYPE f) {
			_values.Add(f);
		}

		public TYPE GetValue() => _values[_index++];

		public byte[] ToCompressedData() {
			UInt16 length = (UInt16)_values.Count;
			bool isKeyframe = _prevValues.Count != _values.Count;

			// changedFlags
			// [0]: is key frame. If so, ignore changedFlags
			// [1] through [length]: changedFlags
			BitArray changedFlags = isKeyframe ? new BitArray(1) : new BitArray(length + 1);
			changedFlags[0] = isKeyframe;
			var changedValues = new List<TYPE>();
			if (!isKeyframe) {
				for (int i = 0; i < length; i++) {
					bool valChanged = !IsEqual(_values[i], _prevValues[i]);
					changedFlags[i + 1] = valChanged;
					if (valChanged) {
						changedValues.Add(_values[i]);
					}
				}
			}
			byte[] changedFlagsBytes = new byte[NBytesToFitBits(changedFlags.Count)];
			changedFlags.CopyTo(changedFlagsBytes, 0);
			MemoryStream binaryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(binaryStream);
			binaryWriter.Write(length);
			binaryWriter.Write(changedFlagsBytes);
			var valuesToWrite = (isKeyframe ? _values : changedValues);
			foreach (var f in valuesToWrite) {
				binaryWriter.Write(ConvertToSingle(f));
			}
			var result = new byte[binaryStream.Length];
			Array.Copy(binaryStream.GetBuffer(), result, result.Length);
			return result;
		}

		public void FromCompressedData(byte[] bytes) {
			MemoryStream binaryStream = new MemoryStream(bytes);
			BinaryReader binaryReader = new BinaryReader(binaryStream);
			UInt16 length = binaryReader.ReadUInt16();
			byte first8Bits = binaryReader.ReadByte();
			BitArray first8BitsArr = new BitArray(new byte[] { first8Bits });
			bool isKeyFrame = first8BitsArr[0];
			if (isKeyFrame) {
				_values.Clear();
				for (int i = 0; i < length; i++) {
					_values.Add(ConvertFromSingle(binaryReader.ReadSingle()));
				}
			} else {
				if (_prevValues.Count != length) {
					Console.Write("Received length and previous length don't match. Values will not be deserialized until the next key frame arrives");
					return;
				}
				_values.Clear();
				// seek backward one byte to grab the full bit array
				binaryStream.Seek(-1, SeekOrigin.Current);
				byte[] changedFlagsBytes = binaryReader.ReadBytes(NBytesToFitBits(length + 1));
				BitArray changedFlags = new BitArray(changedFlagsBytes);
				for (int i = 0; i < length; i++) {
					_values.Add(changedFlags[i + 1] ? ConvertFromSingle(binaryReader.ReadSingle()) : _prevValues[i]);
				}
			}
		}

		private int NBytesToFitBits(int nBits) {
			return (nBits - 1) / 8 + 1;
		}
	}
}
