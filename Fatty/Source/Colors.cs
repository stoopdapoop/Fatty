using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fatty
{
    class Colors
    {
        public struct RGB
        {
            public int R;
            public int G;
            public int B;

            public RGB(int r, int g, int b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        // Irc Standard colors
        static Dictionary<int, RGB> ircColors = new Dictionary<int, RGB>()
        {
            { 0, new RGB(255, 255, 255) }, // White
            { 1, new RGB(0, 0, 0) },       // Black
            { 2, new RGB(0, 0, 127) },     // Blue (Navy)
            { 3, new RGB(0, 147, 0) },     // Green
            { 4, new RGB(255, 0, 0) },     // Red
            { 5, new RGB(127, 0, 0) },     // Brown (Maroon)
            { 6, new RGB(156, 0, 156) },   // Purple
            { 7, new RGB(252, 127, 0) },   // Orange (Olive)
            { 8, new RGB(255, 255, 0) },   // Yellow
            { 9, new RGB(0, 252, 0) },     // Light Green
            { 10, new RGB(0, 147, 147) },  // Cyan (Teal)
            { 11, new RGB(0, 255, 255) },  // Light Cyan (Aqua)
            { 12, new RGB(0, 0, 252) },    // Light Blue (Royal)
            { 13, new RGB(255, 0, 255) },  // Pink (Light Purple)
            { 14, new RGB(127, 127, 127) },// Grey
            { 15, new RGB(210, 210, 210) } // Light Grey
        };

        static Dictionary<string, int> ircColorCache = new Dictionary<string, int>();


        public static RGB HexToRGB(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length != 6) throw new ArgumentException("Invalid hex color");

            return new RGB(
                int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber)
            );
        }

        public static int GetNearestIrcColor(string hexColor)
        {
            hexColor = hexColor.Replace("#", "");
            if (ircColorCache.TryGetValue(hexColor, out int result))
            {
                return result;
            }

            try
            {
                RGB inputColor = HexToRGB(hexColor);
                int nearestColorCode = -1;
                double minDistance = double.MaxValue;

                foreach (var kvp in ircColors)
                {
                    double distance = ColorDistance(inputColor, kvp.Value);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestColorCode = kvp.Key;
                    }
                }

                ircColorCache[hexColor] = nearestColorCode;

                return nearestColorCode;
            }
            catch
            {
                return -1;
            }
        }

        public static double ColorDistance(RGB color1, RGB color2)
        {
            return Math.Sqrt(Math.Pow(color1.R - color2.R, 2) +
                             Math.Pow(color1.G - color2.G, 2) +
                             Math.Pow(color1.B - color2.B, 2));
        }
    }
}
