using System;
using System.Collections.Generic;
using System.Text;

namespace _20221123_netcode4 {
	public class HomoglyphGraph {
		static string[] homoglyphs = {
" _",
"A4^HEVX",
"B813EP",
"Cc(<[{",
"D0OP)",
"E3BF",
"FETI",
"GCe6&0O",
"H#hA4B8KX",
"Il1|![])(][T",
"Jj7iL",
"KXHhY",
"L1i|",
"MmHNW",
"NMnWH",
"Oo0cCDQG@",
"PpqbR",
"QO0CDG@",
"RPpBbK",
"S5$",
"T+7F",
"UuVvY",
"VUuYyW",
"WMwVVv",
"Xx+Y",
"YVXy",
"Z2z",
"a@d",
"bdp6oh",
"c(C[{<oed",
"dqb9",
"eEco",
"ft+F",
"g9q",
"hHkb",
"il1|!:;j",
"jiJ;",
"kKHXhb",
"l1|I!",
"mnMN",
"nmMNu",
"o0Ocpdeu",
"pbdoq",
"qpdg",
"rcn",
"s5$z",
"t+Tf",
"uUvVno",
"vVuYy",
"wWVv",
"xX+yk*",
"yYVvu",
"zZ2s",
"0OQCDG@",
"1lI|!",
"2Zz7",
"3E8B",
"4A^H",
"5S$",
"6Gb",
"7Tt?",
"8B3H",
"9gq",
"@aQ0O",
"#H",
"$S5",
"%96o/",
"^A4",
"&Gg",
"*x+'",
"+xX*tf",
"-_=",
"_- ",
"=_",
"|l1I!][/\\",
"\\/|",
"/\\|%",
"`'",
"'`\"",
"\"',",
";:i,",
":;i.",
",. ",
".,",
"<([{",
">)]}",
"({[",
")]}",
"{[(",
"}])",
"[{(",
"]})",
"?7",
		};

		Dictionary<char, byte> indexOf = new Dictionary<char, byte>();

		private void Init() {
			for (byte i = 0; i < homoglyphs.Length; i++) {
				indexOf[homoglyphs[i][0]] = i;
			}
		}

		private byte[][] allPaths;
		public void CalculateAllPaths() {
			Init();
			allPaths = new byte[homoglyphs.Length][];
			for(byte i = 0; i < homoglyphs.Length; ++i) {
				byte[] paths = Djikstras(i);
				allPaths[i] = paths;
			}
		}

		public byte[] Djikstras(byte source) {
			int[] dist = new int[homoglyphs.Length];
			byte[] prev = new byte[homoglyphs.Length];
			List<byte> queue = new List<byte>();
			const byte UNDEFINED = 255;
			for(int i = 0; i < homoglyphs.Length; ++i) {
				dist[i] = -1;
				prev[i] = UNDEFINED;
				queue.Add((byte)i);
			}
			dist[source] = 0;
			while (queue.Count > 0) {
				byte u = minDist();
				queue.RemoveAt(u);
				char self = homoglyphs[u][0];
				string neighbors = homoglyphs[u].Substring(1);
				for (int i = 0; i < neighbors.Length; ++i) {
					byte v = indexOf[neighbors[i]];
					int alt = dist[u] + 1;
					if (dist[v] < 0 || alt < dist[v]) {
						dist[v] = alt;
						prev[v] = u;
					}
				}
			}
			return prev;
			byte minDist() {
				int bestDist = -1;
				byte bestVertex = 0;
				foreach(byte a in queue) {
					int d = dist[a];
					if (d >= 0 && d < bestDist) {
						bestVertex = a;
						bestDist = d;
					}
				}
				return bestVertex;
			}
		}
	}
}
