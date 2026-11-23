using System;
using System.Drawing;
using System.Globalization;
using System.Text;

internal sealed class ConsoleFrame
{
    internal string Text, Rtf;
    internal Color Background;
    internal static readonly uint[] DefaultColours =
    {
        0x0C0C0C,
        0xDA3700,
        0x0EA113,
        0xDD963A,
        0x1F0FC5,
        0x981788,
        0x009CC1,
        0xCCCCCC,
        0x767676,
        0xFF783B,
        0x0CC616,
        0xD6D661,
        0x5648E7,
        0x9E00B4,
        0xA5F1F9,
        0xF2F2F2
    };
    private static int LastColumn(ConsoleCell[] cells, int start, int width)
    {
        for (int i = width - 1; i >= 0; i--)
        {
            var cell = cells[start + i];
            if ((cell.Character != '\0' && cell.Character != ' ' && cell.Character != '\uffff') || (cell.Attributes & 0x40F0) != 0)
                return i;
        }

        return -1;
    }

    private static void Escape(StringBuilder rtf, char value)
    {
        if (value == '\\' || value == '{' || value == '}')
            rtf.Append('\\').Append(value);
        else if (value > 127)
            rtf.Append("\\u").Append(unchecked((short)value).ToString(CultureInfo.InvariantCulture)).Append('?');
        else
            rtf.Append(value < 32 ? ' ' : value);
    }

    internal static ConsoleFrame FromCells(ConsoleCell[] cells, int columns, uint[] colours)
    {
        if (columns < 1 || cells.Length % columns != 0)
            throw new ArgumentException("Invalid console grid dimensions.");
        if (colours == null || colours.Length != 16)
            colours = DefaultColours;
        var text = new StringBuilder();
        var rtf = new StringBuilder(@"{\rtf1\ansi\ansicpg1252\deff0\uc1{\fonttbl{\f0\fmodern Consolas;}}{\colortbl;");
        foreach (uint colour in colours)
            rtf.Append("\\red").Append(colour & 255).Append("\\green").Append((colour >> 8) & 255).Append("\\blue").Append((colour >> 16) & 255).Append(';');
        rtf.Append(@"}\viewkind4\pard\ltrpar\f0\fs20\sa0\sb0 ");
        int rows = cells.Length / columns;
        while (rows > 0 && LastColumn(cells, (rows - 1) * columns, columns) < 0)
            rows--;
        int lastForeground = -1, lastBackground = -1;
        bool underline = false;
        for (int row = 0; row < rows; row++)
        {
            if (row != 0)
            {
                text.Append('\n');
                rtf.Append(@"\par ");
            }

            int start = row * columns, last = LastColumn(cells, start, columns);
            for (int col = 0; col <= last; col++)
            {
                var cell = cells[start + col];
                char value = cell.Character;
                // The rectangular grid is authoritative. Never split a linear
                // Unicode string at a cell count: wide glyphs change its length.
                bool pair = char.IsHighSurrogate(value) && col < last && char.IsLowSurrogate(cells[start + col + 1].Character);
                if (!pair && ((cell.Attributes & 0x0200) != 0 || value == '\uffff'))
                    continue;
                int foreground = cell.Attributes & 15, background = (cell.Attributes >> 4) & 15;
                if ((cell.Attributes & 0x4000) != 0)
                {
                    int swap = foreground;
                    foreground = background;
                    background = swap;
                }

                if (foreground != lastForeground)
                {
                    rtf.Append("\\cf").Append(foreground + 1).Append(' ');
                    lastForeground = foreground;
                }

                if (background != lastBackground)
                {
                    rtf.Append("\\highlight").Append(background + 1).Append(' ');
                    lastBackground = background;
                }

                bool nextUnderline = (cell.Attributes & 0x8000) != 0;
                if (nextUnderline != underline)
                {
                    rtf.Append(nextUnderline ? @"\ul " : @"\ulnone ");
                    underline = nextUnderline;
                }

                if (value == '\0')
                    value = ' ';
                text.Append(value);
                Escape(rtf, value);
                if (pair)
                {
                    char low = cells[start + ++col].Character;
                    text.Append(low);
                    Escape(rtf, low);
                }
            }
        }

        rtf.Append('}');
        uint back = colours[0];
        return new ConsoleFrame
        {
            Text = text.ToString(),
            Rtf = rtf.ToString(),
            Background = Color.FromArgb((int)(back & 255), (int)((back >> 8) & 255), (int)((back >> 16) & 255))
        };
    }
}
