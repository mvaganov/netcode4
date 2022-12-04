using System;
using System.Collections.Generic;
using System.Text;

namespace networking {
	public class CommandLineInput {
		List<char> consoleInput = new List<char>();
		string animation = "/-\\|";
		int animationIndex = 0, whenToAnimateNext = Environment.TickCount;
		int animationFrameMs = 200;
		private Dictionary<ConsoleKey, Action> keyBind = new Dictionary<ConsoleKey, Action>();

		public CommandLineInput() { }

		public CommandLineInput(string animation, int animationFrameMs) {
			this.animation = animation; this.animationFrameMs = animationFrameMs;
		}

		public void BindKey(ConsoleKey key, Action action) { keyBind[key] = action; }
		public void UpdateAsciiAnimation() {
			if (consoleInput.Count == 0 && Environment.TickCount >= whenToAnimateNext) {
				whenToAnimateNext += animationFrameMs;
				if (consoleInput.Count == 0) {
					Console.Write(animation[animationIndex++] + "\r");
					if (animationIndex >= animation.Length) { animationIndex = 0; }
				}
			}
		}
		public byte[] GetInputAsBytes() { return Encoding.UTF8.GetBytes(consoleInput.ToArray()); }
		public byte[] PopInputAsBytes() { byte[] bytes = GetInputAsBytes(); consoleInput.Clear(); return bytes; }
		public string GetInputAsString() { return new string(consoleInput.ToArray()); }
		public string PopInputAsString() { string str = GetInputAsString(); consoleInput.Clear(); return str; }

		/// <returns>true if a key was read into the input buffer</returns>
		public bool UpdateKeyInput() {
			if (!Console.KeyAvailable) { return false; }
			ConsoleKeyInfo keyInfo = Console.ReadKey();
			if (keyBind.TryGetValue(keyInfo.Key, out Action action)) {
				action.Invoke();
				return false;
			}
			if (keyInfo.KeyChar != 0) { consoleInput.Add(keyInfo.KeyChar); }
			return true;
		}
	}

}
