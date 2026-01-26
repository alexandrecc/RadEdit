namespace RadEdit
{
    internal readonly struct TextRange
    {
        public TextRange(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int Start { get; }
        public int Length { get; }
    }
}
