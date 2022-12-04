using System;
using System.Collections.Generic;
using System.Text;

namespace networking {
	public class CommandLineInput {
		List<char> consoleInput = new List<char>();
		private Func<string> prompt;
		string animation = "/-\\|";
		int animationIndex = 0, whenToAnimateNext = Environment.TickCount;
		int animationFrameMs = 200;
		private Dictionary<ConsoleKey, Action> keyBind = new Dictionary<ConsoleKey, Action>();
		private string currentPrompt;
		private bool enabled = true;

		public bool Enabled { get => enabled; set => enabled = value; }
		public Func<string> Prompt { get => prompt; set => prompt = value; }

		public CommandLineInput(Func<string> prompt) { this.prompt = prompt; }

		public CommandLineInput(Func<string> prompt, string animation, int animationFrameMs) {
			this.prompt = prompt; this.animation = animation; this.animationFrameMs = animationFrameMs;
		}

		public void BindKey(ConsoleKey key, Action action) { keyBind[key] = action; }
		public void UpdateAsciiAnimation() {
			if (!Enabled) { return; }
			if (consoleInput.Count == 0 && Environment.TickCount >= whenToAnimateNext) {
				whenToAnimateNext += animationFrameMs;
				if (consoleInput.Count == 0) {
					currentPrompt = prompt?.Invoke() + animation[animationIndex++] + "\r";
					Console.Write(currentPrompt);
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
			if (!Enabled || !Console.KeyAvailable) { return false; }
			if (consoleInput.Count == 0) {
				string clearPrompt = "";
				for (int i = 0; i < currentPrompt.Length; ++i) {
					clearPrompt += " ";
				}
				clearPrompt += "\r";
				Console.Write(clearPrompt);
			}
			ConsoleKeyInfo keyInfo = Console.ReadKey();
			if (keyBind.TryGetValue(keyInfo.Key, out Action action)) {
				action.Invoke();
				return false;
			}
			if (keyInfo.KeyChar != 0) {
				consoleInput.Add(keyInfo.KeyChar);
			}
			return true;
		}
	}

}
