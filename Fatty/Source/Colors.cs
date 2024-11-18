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
            { 15, new RGB(210, 210, 210) }, // Light Grey

            // extended non standard colors
            { 16, new RGB(71, 0, 0) },       // Dark Red
            { 17, new RGB(71, 33, 0) },      // Dark Brown
            { 18, new RGB(71, 71, 0) },      // Olive
            { 19, new RGB(50, 71, 0) },      // Olive Green
            { 20, new RGB(0, 71, 0) },       // Dark Green
            { 21, new RGB(0, 71, 44) },      // Dark Teal
            { 22, new RGB(0, 71, 71) },      // Deep Cyan
            { 23, new RGB(0, 39, 71) },      // Navy Blue
            { 24, new RGB(0, 0, 71) },       // Dark Blue
            { 25, new RGB(46, 0, 71) },      // Purple
            { 26, new RGB(71, 0, 71) },      // Deep Purple
            { 27, new RGB(71, 0, 42) },      // Dark Magenta
            { 28, new RGB(116, 0, 0) },      // Red
            { 29, new RGB(116, 58, 0) },     // Brown
            { 30, new RGB(116, 116, 0) },    // Yellow-Green
            { 31, new RGB(81, 116, 0) },     // Green-Yellow
            { 32, new RGB(0, 116, 0) },      // Bright Green
            { 33, new RGB(0, 116, 73) },     // Teal
            { 34, new RGB(0, 116, 116) },    // Bright Cyan
            { 35, new RGB(0, 64, 116) },     // Cerulean
            { 36, new RGB(0, 0, 116) },      // Bright Blue
            { 37, new RGB(75, 0, 116) },     // Violet
            { 38, new RGB(116, 0, 116) },    // Magenta
            { 39, new RGB(116, 0, 69) },     // Deep Pink
            { 40, new RGB(181, 0, 0) },      // Bright Red
            { 41, new RGB(181, 99, 0) },     // Orange
            { 42, new RGB(181, 181, 0) },    // Bright Yellow
            { 43, new RGB(125, 181, 0) },    // Lime
            { 44, new RGB(0, 181, 0) },      // Light Green
            { 45, new RGB(0, 181, 113) },    // Aqua
            { 46, new RGB(0, 181, 181) },    // Light Cyan
            { 47, new RGB(0, 99, 181) },     // Sky Blue
            { 48, new RGB(0, 0, 181) },      // Royal Blue
            { 49, new RGB(117, 0, 181) },    // Purple
            { 50, new RGB(181, 0, 181) },    // Fuchsia
            { 51, new RGB(181, 0, 107) },    // Pink
            { 52, new RGB(255, 0, 0) },      // Pure Red
            { 53, new RGB(255, 140, 0) },    // Bright Orange
            { 54, new RGB(255, 255, 0) },    // Pure Yellow
            { 55, new RGB(178, 255, 0) },    // Lime Green
            { 56, new RGB(0, 255, 0) },      // Pure Green
            { 57, new RGB(0, 255, 160) },    // Mint Green
            { 58, new RGB(0, 255, 255) },    // Pure Cyan
            { 59, new RGB(0, 140, 255) },    // Light Blue
            { 60, new RGB(0, 0, 255) },      // Pure Blue
            { 61, new RGB(165, 0, 255) },    // Violet
            { 62, new RGB(255, 0, 255) },    // Pure Magenta
            { 63, new RGB(255, 0, 152) },    // Hot Pink
            { 64, new RGB(255, 89, 89) } ,    // Light Red
            { 65, new RGB(255, 180, 89) },   // Light Orange
            { 66, new RGB(255, 255, 113) },  // Light Yellow
            { 67, new RGB(207, 255, 96) },   // Pale Lime
            { 68, new RGB(111, 255, 111) },  // Light Green
            { 69, new RGB(101, 255, 201) },  // Mint Aqua
            { 70, new RGB(109, 255, 255) },  // Sky Cyan
            { 71, new RGB(89, 180, 255) },   // Soft Blue
            { 72, new RGB(89, 89, 255) },    // Blue
            { 73, new RGB(196, 89, 255) },   // Lavender
            { 74, new RGB(255, 102, 255) },  // Light Magenta
            { 75, new RGB(255, 89, 188) }    // Light Pink
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
