using MrV;
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
		private bool inputCoordKnown = false;
		private Coord inputCoord;

		public bool Enabled { get => enabled; set => enabled = value; }
		public Func<string> Prompt { get => prompt; set => prompt = value; }
		public int CursorIndex => consoleInput.Count;
		public CommandLineInput(Func<string> prompt) : this(prompt, "/-\\|", 200) { }

		public CommandLineInput(Func<string> prompt, string animation, int animationFrameMs) {
			this.prompt = prompt; this.animation = animation; this.animationFrameMs = animationFrameMs;
			BindKey(ConsoleKey.Backspace, DoBackspaceKey);
		}

		public void BindKey(ConsoleKey key, Action action) { keyBind[key] = action; }
		public void UpdatePrompt() {
			if (!Enabled) { return; }
			if (!inputCoordKnown) {
				inputCoord = Coord.GetCursorPosition();
			}
			if (animation.Length > 0 && consoleInput.Count == 0 && Environment.TickCount >= whenToAnimateNext) {
				whenToAnimateNext += animationFrameMs;
				if (consoleInput.Count == 0) {
					animationIndex++;
					if (animationIndex >= animation.Length) { animationIndex = 0; }
					currentPrompt = prompt?.Invoke() + animation[animationIndex] + "\r";
					Console.Write(currentPrompt);
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
				ConsoleClearPrompt();
			}
			//Coord c = Coord.GetCursorPosition();
			//Coord.Down.SetCursorPosition();
			//Console.Write(prompt()+" "+Environment.TickCount+" \""+new string(consoleInput.ToArray())+"\"");
			//c.SetCursorPosition();
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
		public void DoBackspaceKey() {
			if (CursorIndex > 0) {
				consoleInput.RemoveAt(CursorIndex - 1);
				Coord p = Coord.GetCursorPosition();
				Console.Write(" ");
				p.SetCursorPosition();
			} else {
				inputCoord.SetCursorPosition();
			}
		}

		public void ConsoleClearPrompt() {
			string clearPrompt = "";
			for (int i = 0; i < currentPrompt.Length; ++i) { clearPrompt += " "; }
			clearPrompt += "\r";
			Console.Write(clearPrompt);
		}
	}
}
