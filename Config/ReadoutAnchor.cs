using UnityEngine;

namespace ScopeRangefinder
{
    public enum ReadoutAnchor
    {
        Center,
        TopLeft,
        Top,
        TopRight,
        Left,
        Right,
        BottomLeft,
        Bottom,
        BottomRight
    }

    internal static class ReadoutAnchorExtensions
    {
        public static Vector2 ToPivot(this ReadoutAnchor anchor)
        {
            switch (anchor)
            {
                case ReadoutAnchor.TopLeft: return new Vector2(0f, 1f);
                case ReadoutAnchor.Top: return new Vector2(0.5f, 1f);
                case ReadoutAnchor.TopRight: return new Vector2(1f, 1f);
                case ReadoutAnchor.Left: return new Vector2(0f, 0.5f);
                case ReadoutAnchor.Right: return new Vector2(1f, 0.5f);
                case ReadoutAnchor.BottomLeft: return new Vector2(0f, 0f);
                case ReadoutAnchor.Bottom: return new Vector2(0.5f, 0f);
                case ReadoutAnchor.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }
    }
}
