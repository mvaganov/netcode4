using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	/// <summary>
	/// keep track of filled data as separate from allocated data
	/// handle my own memory for networking buffers
	/// build my own convenience functions for network data and buffering
	/// </summary>
	public class NetBuffer {
		/// <summary>
		/// byte buffer for receiving and sending data
		/// </summary>
		private byte[] _buffer;
		/// <summary>
		/// how much the buffer is filled
		/// </summary>
		private int _filled;
		
		public byte[] Buffer => _buffer;
		public int Count { get => _filled; set => _filled = value; }
		public int Capacity { get => _buffer.Length; set => Resize(value); }
		public byte this[int i] { get { return _buffer[i]; } set { _buffer[i] = value; } }
		
		public NetBuffer() : this (new byte[0], 0) { }

		/// <summary>
		/// assumes the entire given buffer is filled with data
		/// </summary>
		/// <param name="buffer"></param>
		public NetBuffer(byte[] buffer) : this(buffer, buffer.Length) { }

		public NetBuffer(byte[] buffer, int filled) { _buffer = buffer; _filled = filled; }

		public static NetBuffer FromString(string message) => Utf8(message);
		public static NetBuffer Utf8(string value) => new NetBuffer(Encoding.UTF8.GetBytes(value));
		
		public void AddUtf8(List<char> value) => Add(Encoding.UTF8.GetBytes(value.ToArray()));
		
		public void AddUtf8(string value) => Add(Encoding.UTF8.GetBytes(value));
		
		public void Add(byte[] bytes) => Add(bytes, bytes.Length);

		public void Add(NetBuffer otherBuffer) => Add(otherBuffer.Buffer, otherBuffer.Count);

		public void Add(byte[] bytes, int count) {
			if (_buffer.Length < _filled + count) {
				Resize(_filled + count);
			}
			Array.Copy(bytes, 0, _buffer, _filled, count);
			_filled += count;
		}

		public void Resize(int newSize) {
			Array.Resize(ref _buffer, newSize);
		}

		public void Clear() { _filled = 0; }

		public string ToUtf8() {
			return Encoding.UTF8.GetString(_buffer, 0, _filled);
		}

		public static bool IsClearedDeadStream(TcpClient c, int i, List<TcpClient> clients) {
			if (c == null || !c.Connected) {
				Console.WriteLine($"removing client {i} {c == null}");
				if (clients != null) {
					clients.RemoveAt(i);
				}
				return true;
			}
			return false;
		}

		public async Task WriteFlushStream(TcpClient c) {
			NetworkStream ns = c.GetStream();
			await ns.WriteAsync(Buffer, 0, Count);
			ns.Flush();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="client"></param>
		/// <param name="intermediateBuffer">where bytes being read directly from the socket go</param>
		/// <param name="onIntermediateReceived">callback whenever data is put into the given intermediate buffer</param>
		/// <returns></returns>
		public async Task Read(TcpClient client, NetBuffer intermediateBuffer, Action<NetBuffer> onIntermediateReceived) {
			NetworkStream ns = client.GetStream();
			while (ns.DataAvailable) {
				int received = await ns.ReadAsync(intermediateBuffer.Buffer);
				intermediateBuffer.Count = received;
				if (received <= 0) { return; }
				onIntermediateReceived?.Invoke(intermediateBuffer);
				Add(intermediateBuffer.Buffer, received);
			}
		}
	}
}
