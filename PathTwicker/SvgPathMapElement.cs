/*
 *

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.

 * */

using System.Drawing;
using System.Globalization;
using System.Text;

namespace SVGPathTwicker
{
    public enum PenMovementType
    {
        None = 0,
        Line = 1,
        CubicBezier = 2,
        Horizontal = 4,
        Vertical = 8,
        Arc = 16,
        QuadraticBezier = 32,
        SmoothCubic = 64,
        SmoothQuadratic = 128
    }

    public class SvgPathMapElement
    {
        public SvgPathMapElement(int translateX, int translateY, string originalPath)
        {
            PathOrigin.Offset(translateX, translateY);
            OriginalPath = originalPath;
            Init();
        }

        public static string CSVHeader {
            get
            {
                StringBuilder header = new ();
                header.Append(nameof(OriginalPath));
                header.Append(";Delta X;Delta Y;Label X;Label Y;Box X;Box Y;WIdth;Height;");
                header.Append(nameof(ImprovedPath));
                header.Append(";CSS class; CSS style;");
                header.Append(Environment.NewLine);
                return header.ToString();
            }
        }

        public void AddCsv(ref StringBuilder source)
        {
            source.Append('"');
            source.Append(OriginalPath);
            source.Append('"');
            source.Append(';');
            source.Append(PathOrigin.X);
            source.Append(';');
            source.Append(PathOrigin.Y);
            source.Append(';');
            int lx = (P2x - P1x) / 2 + P1x;
            source.Append(lx);
            source.Append(';');
            int ly = (P2y - P1y) / 2 + P1y;
            source.Append(ly);
            source.Append(';');
            source.Append(P1x);
            source.Append(';');
            source.Append(P1y);
            source.Append(';');
            int w = P2x - P1x;
            source.Append(w);
            source.Append(';');
            int h = P2y - P1y;
            source.Append(h);
            source.Append(';');
            source.Append('"');
            source.Append(ImprovedPath);
            source.Append('"');
            source.Append(';');
        }

        public string OriginalPath { get; private set; } = string.Empty;

        public void SetPath(string path)
        {
            OriginalPath = path;
        }

        public string ReadImprovedPath()
        {
            return ImprovedPath.ToString();
        }

        public override string ToString()
        {
            int lx = (P2x - P1x) / 2 + P1x;
            int ly = (P2y - P1y) / 2 + P1y;
            return $"{PathOrigin.X} {PathOrigin.Y} {lx} {ly} {P1x} {P1y} {P2x - P1x} {P2y - P1y}";
        }

        private Point PenOnPaper = Point.Empty;

        private Point PathStartPoint = Point.Empty;

        private int PathNumber = 0;

        private int P1x { get; set; } = 0;
        private int P1y { get; set; } = 0;
        private int P2x { get; set; } = 0;
        private int P2y { get; set; } = 0;

        private PenMovementType PenMove = PenMovementType.None;

        private bool SubpathClosed = true;

        private readonly StringBuilder ImprovedPath = new ();

        private void MovePen(Point p)
        {
            PenOnPaper.Offset(p.X, p.Y);
            P1x = Math.Min(P1x, PenOnPaper.X);
            P1y = Math.Min(P1y, PenOnPaper.Y);
            P2x = Math.Max(P2x, PenOnPaper.X);
            P2y = Math.Max(P2y, PenOnPaper.Y);
        }

        private Point ToRelativeCoord(Point p)
        {
            p.Offset(-PathOrigin.X, -PathOrigin.Y);
            p.Offset(-PenOnPaper.X, -PenOnPaper.Y);
            return p;
        }

        private Point PathOrigin = Point.Empty;

        private void Init()
        {
            bool InAbsoluteCoord = false;
            string path = OriginalPath;
            int pos = 0;
            int len = path.Length;
            int buffer_integer;
            bool buffer_arc_lg, buffer_sweep;
            Point buffer_point1, buffer_point_P2, buffer_point_P3;
            bool aborted = false;

            while (!aborted && pos < len)
            {
                char? command = ReadCommand(path, ref pos);
                switch (command)
                {
                    case 'm':
                    case 'M':
                        InAbsoluteCoord = (command == 'M');
                        PenMove = PenMovementType.Line;
                        PathNumber++;
                        if (!TryReadPoint(path, ref pos, out buffer_point1)) { ReportParseFailure(path, pos, "moveto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                        }
                        MovePen(buffer_point1);
                        if (PathNumber == 1)
                        {
                            PathStartPoint = PathOrigin;
                            PathOrigin.Offset(buffer_point1);
                            buffer_point1 = Point.Empty;
                        }
                        else
                        {
                            PathStartPoint = PenOnPaper;
                            if (!SubpathClosed)
                            {
                                // previous subpath never closed: force it before starting the next one
                                ImprovedPath.Append(" z");
                            }
                            ImprovedPath.Append(' ');
                        }
                        ImprovedPath.AppendFormat("m {0},{1}", buffer_point1.X, buffer_point1.Y);
                        SubpathClosed = false;
                        break;
                    case 'z':
                    case 'Z': // all is converted to relative coordinate anyway
                        PenMove = PenMovementType.None;
                        PenOnPaper = PathStartPoint;
                        ImprovedPath.Append(" z");
                        SubpathClosed = true;
                        break;
                    case 'l':
                    case 'L':
                        InAbsoluteCoord = (command == 'L');
                        PenMove = PenMovementType.Line;
                        if (!TryReadPoint(path, ref pos, out buffer_point1)) { ReportParseFailure(path, pos, "lineto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                        }
                        MovePen(buffer_point1);
                        ImprovedPath.AppendFormat(" l {0},{1}", buffer_point1.X, buffer_point1.Y);
                        break;
                    case 'c':
                    case 'C':
                        InAbsoluteCoord = (command == 'C');
                        PenMove = PenMovementType.CubicBezier;
                        if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                            !TryReadPoint(path, ref pos, out buffer_point_P2) ||
                            !TryReadPoint(path, ref pos, out buffer_point_P3))
                        { ReportParseFailure(path, pos, "curveto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            // convert to relative coords
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                            buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                            buffer_point_P3 = ToRelativeCoord(buffer_point_P3);
                        }
                        MovePen(buffer_point_P3);
                        ImprovedPath.Append(" c");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P3.X, buffer_point_P3.Y);
                        break;
                    case 'h':
                    case 'H':
                        InAbsoluteCoord = (command == 'H');
                        PenMove = PenMovementType.Horizontal;
                        if (!TryReadCoordinate(path, ref pos, out buffer_integer)) { ReportParseFailure(path, pos, "horizontal lineto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_integer -= PathOrigin.X;
                            buffer_integer -= PenOnPaper.X;
                        }
                        MovePen(new(buffer_integer, 0));
                        if(Math.Abs(buffer_integer) > 0)
                        {
                            ImprovedPath.Append(" h ");
                            ImprovedPath.Append(buffer_integer);
                        }
                        break;
                    case 'v':
                    case 'V':
                        InAbsoluteCoord = (command == 'V');
                        PenMove = PenMovementType.Vertical;
                        if (!TryReadCoordinate(path, ref pos, out buffer_integer)) { ReportParseFailure(path, pos, "vertical lineto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_integer -= PathOrigin.Y;
                            buffer_integer -= PenOnPaper.Y;
                        }
                        MovePen(new(0, buffer_integer));
                        if(Math.Abs(buffer_integer) > 0)
                        {
                            ImprovedPath.Append(" v ");
                            ImprovedPath.Append(buffer_integer);
                        }
                        break;
                    case 'a':
                    case 'A':
                        InAbsoluteCoord = (command == 'A');
                        PenMove = PenMovementType.Arc;
                        if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                            !TryReadNumber(path, ref pos, out double rotation) ||
                            !TryReadFlag(path, ref pos, out buffer_arc_lg) ||
                            !TryReadFlag(path, ref pos, out buffer_sweep) ||
                            !TryReadPoint(path, ref pos, out buffer_point_P2))
                        { ReportParseFailure(path, pos, "elliptical arc"); aborted = true; break; }
                        buffer_integer = RoundToInt(rotation);
                        if (InAbsoluteCoord)
                        {
                            buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                        }
                        MovePen(buffer_point_P2);
                        ImprovedPath.Append(" a");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0}", buffer_integer);
                        ImprovedPath.AppendFormat(" {0}", buffer_arc_lg ? 1 : 0);
                        ImprovedPath.AppendFormat(" {0}", buffer_sweep ? 1 : 0);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        break;
                    case 'q':
                    case 'Q':
                        InAbsoluteCoord = (command == 'Q');
                        PenMove = PenMovementType.QuadraticBezier;
                        if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                            !TryReadPoint(path, ref pos, out buffer_point_P2))
                        { ReportParseFailure(path, pos, "quadratic curveto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                            buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                        }
                        MovePen(buffer_point_P2);
                        ImprovedPath.Append(" q");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        break;
                    case 't':
                    case 'T':
                        InAbsoluteCoord = (command == 'T');
                        PenMove = PenMovementType.SmoothQuadratic;
                        if (!TryReadPoint(path, ref pos, out buffer_point1)) { ReportParseFailure(path, pos, "smooth quadratic curveto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                        }
                        MovePen(buffer_point1);
                        ImprovedPath.AppendFormat(" t {0},{1}", buffer_point1.X, buffer_point1.Y);
                        break;
                    case 's':
                    case 'S':
                        InAbsoluteCoord = (command == 'S');
                        PenMove = PenMovementType.SmoothCubic;
                        if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                            !TryReadPoint(path, ref pos, out buffer_point_P2))
                        { ReportParseFailure(path, pos, "smooth curveto"); aborted = true; break; }
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                            buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                        }
                        MovePen(buffer_point_P2);
                        ImprovedPath.Append(" s");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        break;
                    default:
                        // no explicit command letter here: an implicit repeat of the previous command's
                        // argument type, per the SVG path grammar (e.g. "m 0,0 10,10" repeats as lineto)
                        switch (PenMove)
                        {
                            case PenMovementType.Line:
                                if (!TryReadPoint(path, ref pos, out buffer_point1)) { ReportParseFailure(path, pos, "lineto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_point1 = ToRelativeCoord(buffer_point1);
                                }
                                MovePen(buffer_point1);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                break;
                            case PenMovementType.CubicBezier:
                                if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                                    !TryReadPoint(path, ref pos, out buffer_point_P2) ||
                                    !TryReadPoint(path, ref pos, out buffer_point_P3))
                                { ReportParseFailure(path, pos, "curveto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    // convert to relative coords
                                    buffer_point1 = ToRelativeCoord(buffer_point1);
                                    buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                                    buffer_point_P3 = ToRelativeCoord(buffer_point_P3);
                                }
                                MovePen(buffer_point_P3);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P3.X, buffer_point_P3.Y);
                                break;
                            case PenMovementType.Horizontal:
                                if (!TryReadCoordinate(path, ref pos, out buffer_integer)) { ReportParseFailure(path, pos, "horizontal lineto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_integer -= PathOrigin.X;
                                    buffer_integer -= PenOnPaper.X;
                                }
                                MovePen(new(buffer_integer, 0));
                                if(Math.Abs(buffer_integer) > 0)
                                {
                                    ImprovedPath.Append(' ');
                                    ImprovedPath.Append(buffer_integer);
                                }
                                break;
                            case PenMovementType.Vertical:
                                if (!TryReadCoordinate(path, ref pos, out buffer_integer)) { ReportParseFailure(path, pos, "vertical lineto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_integer -= PathOrigin.Y;
                                    buffer_integer -= PenOnPaper.Y;
                                }
                                MovePen(new(0, buffer_integer));
                                if(Math.Abs(buffer_integer) > 0)
                                {
                                    ImprovedPath.Append(' ');
                                    ImprovedPath.Append(buffer_integer);
                                }
                                break;
                            case PenMovementType.Arc:
                                if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                                    !TryReadNumber(path, ref pos, out double rotationImplicit) ||
                                    !TryReadFlag(path, ref pos, out buffer_arc_lg) ||
                                    !TryReadFlag(path, ref pos, out buffer_sweep) ||
                                    !TryReadPoint(path, ref pos, out buffer_point_P2))
                                { ReportParseFailure(path, pos, "elliptical arc (implicit)"); aborted = true; break; }
                                buffer_integer = RoundToInt(rotationImplicit);
                                if (InAbsoluteCoord)
                                {
                                    buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                                }
                                MovePen(buffer_point_P2);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                ImprovedPath.AppendFormat(" {0}", buffer_integer);
                                ImprovedPath.AppendFormat(" {0}", buffer_arc_lg ? 1 : 0);
                                ImprovedPath.AppendFormat(" {0}", buffer_sweep ? 1 : 0);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                                break;
                            case PenMovementType.QuadraticBezier:
                                if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                                    !TryReadPoint(path, ref pos, out buffer_point_P2))
                                { ReportParseFailure(path, pos, "quadratic curveto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_point1 = ToRelativeCoord(buffer_point1);
                                    buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                                }
                                MovePen(buffer_point_P2);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                                break;
                            case PenMovementType.SmoothQuadratic:
                                if (!TryReadPoint(path, ref pos, out buffer_point1)) { ReportParseFailure(path, pos, "smooth quadratic curveto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_point1 = ToRelativeCoord(buffer_point1);
                                }
                                MovePen(buffer_point1);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                break;
                            case PenMovementType.SmoothCubic:
                                if (!TryReadPoint(path, ref pos, out buffer_point1) ||
                                    !TryReadPoint(path, ref pos, out buffer_point_P2))
                                { ReportParseFailure(path, pos, "smooth curveto (implicit)"); aborted = true; break; }
                                if (InAbsoluteCoord)
                                {
                                    buffer_point1 = ToRelativeCoord(buffer_point1);
                                    buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                                }
                                MovePen(buffer_point_P2);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                                break;
                            default:
                                ReportParseFailure(path, pos, "no active command");
                                aborted = true;
                                break;
                        }
                        break;
                }
            }
            if (!SubpathClosed)
            {
                // every path shall close using 'z', even if the source omitted it
                ImprovedPath.Append(" z");
                SubpathClosed = true;
            }
        }

        private const string CommandLetters = "MmZzLlHhVvCcSsQqTtAa";

        // comma and whitespace are interchangeable separators in the SVG path grammar
        private static void SkipCommaWsp(string s, ref int pos)
        {
            while (pos < s.Length && (s[pos] is ' ' or '\t' or '\r' or '\n' or ','))
            {
                pos++;
            }
        }

        private static char? ReadCommand(string s, ref int pos)
        {
            SkipCommaWsp(s, ref pos);
            if (pos < s.Length && CommandLetters.IndexOf(s[pos]) >= 0)
            {
                return s[pos++];
            }
            return null;
        }

        // Reads one SVG path "number" per the spec grammar: optional sign, digits and/or a decimal
        // point, optional exponent. Numbers may run together with no separator whenever unambiguous,
        // e.g. "50-30" (sign starts a new number), ".5.5" (a second '.' starts a new number) or "1e-3"
        // (the exponent belongs to the same number it follows).
        private static bool TryReadNumber(string s, ref int pos, out double value)
        {
            SkipCommaWsp(s, ref pos);
            int start = pos;
            int p = pos;
            if (p < s.Length && (s[p] == '+' || s[p] == '-'))
            {
                p++;
            }
            int digits = 0;
            while (p < s.Length && char.IsAsciiDigit(s[p])) { p++; digits++; }
            if (p < s.Length && s[p] == '.')
            {
                p++;
                while (p < s.Length && char.IsAsciiDigit(s[p])) { p++; digits++; }
            }
            if (digits == 0)
            {
                value = 0;
                return false;
            }
            if (p < s.Length && (s[p] == 'e' || s[p] == 'E'))
            {
                int q = p + 1;
                if (q < s.Length && (s[q] == '+' || s[q] == '-')) { q++; }
                int expDigits = 0;
                while (q < s.Length && char.IsAsciiDigit(s[q])) { q++; expDigits++; }
                if (expDigits > 0)
                {
                    // valid exponent: it belongs to this number, not a separate token
                    p = q;
                }
            }
            bool ok = double.TryParse(s.AsSpan(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            pos = p;
            return ok;
        }

        private static bool TryReadCoordinate(string s, ref int pos, out int value)
        {
            if (TryReadNumber(s, ref pos, out double d))
            {
                value = RoundToInt(d);
                return true;
            }
            value = 0;
            return false;
        }

        private static bool TryReadPoint(string s, ref int pos, out Point point)
        {
            if (TryReadNumber(s, ref pos, out double x) && TryReadNumber(s, ref pos, out double y))
            {
                point = new Point(RoundToInt(x), RoundToInt(y));
                return true;
            }
            point = Point.Empty;
            return false;
        }

        // every coordinate in this tool simplifies to an integer (see README); round to the nearest
        // one instead of truncating, so e.g. 0.9 becomes 1 rather than disappearing to 0
        internal static int RoundToInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        // An elliptical-arc flag is always exactly one '0' or '1' character, so it is never ambiguous
        // even when packed directly against neighbouring numbers (e.g. "0125,25" = flag,flag,25,25).
        private static bool TryReadFlag(string s, ref int pos, out bool flag)
        {
            SkipCommaWsp(s, ref pos);
            if (pos < s.Length && (s[pos] == '0' || s[pos] == '1'))
            {
                flag = s[pos] == '1';
                pos++;
                return true;
            }
            flag = false;
            return false;
        }

        private static void ReportParseFailure(string s, int pos, string context)
        {
            string near = s.Substring(pos, Math.Min(20, s.Length - pos));
            Console.WriteLine($"path parse error ({context}) near position {pos}: \"{near}\"");
        }
    }
}
