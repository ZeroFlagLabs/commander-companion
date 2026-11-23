using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// Pure data checks only: no windows, focus changes, console allocation, or input.
internal static class LauncherTests
{
    private static int Main()
    {
        var report = new List<string>();
        try
        {
            if (Marshal.SizeOf(typeof(ConsoleCell)) != 4)
                throw new Exception("Console cell layout is incorrect.");
            var cells = Enumerable.Repeat(new ConsoleCell { Character = ' ', Attributes = 7 }, 24).ToArray();
            cells[0] = new ConsoleCell
            {
                Character = 'A',
                Attributes = 7
            };
            cells[1] = new ConsoleCell
            {
                Character = '\ud83d',
                Attributes = 0x0107
            };
            cells[2] = new ConsoleCell
            {
                Character = '\ude80',
                Attributes = 0x0207
            };
            cells[3] = new ConsoleCell
            {
                Character = 'B',
                Attributes = 7
            };
            cells[8] = new ConsoleCell
            {
                Character = '\u754c',
                Attributes = 0x0107
            };
            cells[9] = new ConsoleCell
            {
                Character = '\u754c',
                Attributes = 0x0207
            };
            cells[10] = new ConsoleCell
            {
                Character = 'C',
                Attributes = 7
            };
            cells[16] = new ConsoleCell
            {
                Character = '\u2588',
                Attributes = 1
            };
            cells[17] = new ConsoleCell
            {
                Character = '\u2554',
                Attributes = 1
            };
            cells[18] = new ConsoleCell
            {
                Character = '\u2550',
                Attributes = 1
            };
            cells[19] = new ConsoleCell
            {
                Character = '\u2557',
                Attributes = 1
            };
            ConsoleFrame frame = ConsoleFrame.FromCells(cells, 8, ConsoleFrame.DefaultColours);
            if (frame.Text != "A\ud83d\ude80B\n\u754cC\n\u2588\u2554\u2550\u2557")
                throw new Exception("Unicode console rows were shifted or duplicated.");
            report.Add("PASS: rectangular rows remain aligned across emoji, wide characters and block lettering.");
            if (!frame.Rtf.Contains(@"\cf2 ") || !frame.Rtf.Contains(@"\blue218;") || !frame.Rtf.Contains(@"\u9608?"))
                throw new Exception("Console colour or block-character formatting was lost.");
            report.Add("PASS: blue foreground and Unicode box/block glyphs are retained in rich text.");
            var escaped = ConsoleFrame.FromCells(new[] { new ConsoleCell { Character = '{', Attributes = 7 }, new ConsoleCell { Character = '\\', Attributes = 7 }, new ConsoleCell { Character = '}', Attributes = 7 } }, 3, null);
            if (!escaped.Rtf.Contains(@"\{\\\}"))
                throw new Exception("Rich-text character escaping failed.");
            report.Add("PASS: console text is escaped as data, never interpreted as rich-text instructions.");
            string entry = @"C:\npm\node_modules\@wonderwhy-er\desktop-commander\dist\index.js";
            var cases = new[]
            {
                new
                {
                    Command = "node.exe \"" + entry + "\" remote",
                    Ancestor = false,
                    Expected = true
                },
                new
                {
                    Command = "\"C:\\Program Files\\nodejs\\node.exe\" \"" + entry.ToUpperInvariant() + "\" \"remote\" --debug",
                    Ancestor = false,
                    Expected = true
                },
                new
                {
                    Command = @"node.exe C:\tools\desktopcommander\index.js remote",
                    Ancestor = false,
                    Expected = true
                },
                new
                {
                    Command = "node.exe index.js remote",
                    Ancestor = true,
                    Expected = true
                },
                new
                {
                    Command = "node.exe index.js remote",
                    Ancestor = false,
                    Expected = false
                },
                new
                {
                    Command = @"node.exe C:\npm\npx-cli.js @wonderwhy-er/desktop-commander remote",
                    Ancestor = false,
                    Expected = false
                },
                new
                {
                    Command = "node.exe \"" + entry + "\"",
                    Ancestor = true,
                    Expected = false
                },
                new
                {
                    Command = "node.exe \"" + entry + "\" remote --help",
                    Ancestor = false,
                    Expected = false
                },
                new
                {
                    Command = "node.exe \"" + entry + "\" remote --logout",
                    Ancestor = false,
                    Expected = false
                },
                new
                {
                    Command = "node.exe other.js \"desktop-commander remote\"",
                    Ancestor = false,
                    Expected = false
                }
            };
            foreach (var test in cases)
                if (AgentMatcher.IsRemoteEntry(test.Command, test.Ancestor) != test.Expected)
                    throw new Exception("Agent matching failed a command-line case.");
            report.Add("PASS: external launch variants are recognised; npm wrappers, local MCP workers and non-running help/logout modes are excluded.");
            UsageTests.Run(report);
            ReleaseTests.Run(report);
            foreach (string line in report)
                Console.WriteLine(line);
            return 0;
        }
        catch (Exception ex)
        {
            report.Add("FAIL: " + ex.Message);
            foreach (string line in report)
                Console.WriteLine(line);
            return 1;
        }
    }
}
