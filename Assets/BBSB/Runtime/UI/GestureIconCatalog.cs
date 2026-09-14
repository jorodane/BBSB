using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    /// <summary>One identity for sockets, pattern notation, help, and live Response prompts.</summary>
    public static class GestureIconCatalog
    {
        public const string Root = "BBSB/GestureIcons/";
        public static string Name(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Tap: return "TAP";
                case GestureKind.Hold: return "HOLD";
                case GestureKind.Dive: return "DIVE";
                case GestureKind.Flick: return "FLICK";
                case GestureKind.Shake: return "SHAKE";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static string ColorHex(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Tap: return "DE424B";
                case GestureKind.Hold: return "287DD1";
                case GestureKind.Flick: return "27985A";
                case GestureKind.Dive: return "E28222";
                case GestureKind.Shake: return "9256C9";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static string ResourcePath(GestureKind kind, bool large) =>
            Root + (large ? "Large/" : "Small/") + Name(kind).ToLowerInvariant();
    }
}
