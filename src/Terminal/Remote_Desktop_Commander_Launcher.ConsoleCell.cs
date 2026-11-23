using System.Runtime.InteropServices;

// CHAR_INFO is a four-byte native cell, including its colour and wide-glyph flags.
[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 4)]
internal struct ConsoleCell
{
    [FieldOffset(0)]
    internal char Character;
    [FieldOffset(2)]
    internal ushort Attributes;
}
