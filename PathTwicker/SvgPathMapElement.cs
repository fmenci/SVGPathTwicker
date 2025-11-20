/*
 * 

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.  

 * */

using System.Drawing;
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
        Arc = 16
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
            string[] arrInPath = OriginalPath.Split(' ');
            int arrLength = arrInPath.Length;
            int ipos = 0;
            int buffer_integer;
            bool buffer_arc_lg, buffer_sweep;
            Point buffer_point1, buffer_point_P2, buffer_point_P3;
            while (ipos < arrLength)
            {
                string plot = arrInPath[ipos];
                switch (plot)
                {
                    case "m":
                    case "M":
                        InAbsoluteCoord = (plot == "M");
                        PenMove = PenMovementType.Line;
                        PathNumber++;
                        buffer_point1 = ReadPlot(arrInPath, ref ipos);
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
                            ImprovedPath.Append(' ');
                        }
                        ImprovedPath.AppendFormat("m {0},{1}", buffer_point1.X, buffer_point1.Y);
                        break;
                    case "z":
                    case "Z": // all is converted to relative coordinate anyway
                        PenMove = PenMovementType.None;
                        PenOnPaper = PathStartPoint;
                        ImprovedPath.Append(" z");
                        break;
                    case "l":
                    case "L":
                        InAbsoluteCoord = (plot == "L");
                        PenMove = PenMovementType.Line;
                        buffer_point1 = ReadPlot(arrInPath, ref ipos);
                        if (InAbsoluteCoord)
                        {
                            buffer_point1 = ToRelativeCoord(buffer_point1);
                        }
                        MovePen(buffer_point1);
                        ImprovedPath.AppendFormat(" l {0},{1}", buffer_point1.X, buffer_point1.Y);
                        break;
                    case "c":
                    case "C":
                        InAbsoluteCoord = (plot == "C");
                        PenMove = PenMovementType.CubicBezier;
                        buffer_point1 = ReadPlot(arrInPath, ref ipos);
                        buffer_point_P2 = ReadPlot(arrInPath, ref ipos);
                        buffer_point_P3 = ReadPlot(arrInPath, ref ipos);
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
                    case "h":
                    case "H":
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        InAbsoluteCoord = (plot == "H");
                        PenMove = PenMovementType.Horizontal;
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
                    case "v":
                    case "V":
                        InAbsoluteCoord = (plot == "V");
                        PenMove = PenMovementType.Vertical;
                        buffer_integer = ReadValue(arrInPath[++ipos]);
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
                    case "a":
                    case "A":
                        InAbsoluteCoord = (plot == "A");
                        PenMove = PenMovementType.Arc;
                        buffer_point1 = ReadPlot(arrInPath, ref ipos);
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        buffer_arc_lg = ReadArcComponent(arrInPath[++ipos]);
                        buffer_sweep = ReadArcComponent(arrInPath[++ipos]);
                        buffer_point_P2 = ReadPlot(arrInPath, ref ipos);
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
                    default:
                        try
                        {
                            switch (PenMove)
                            {
                                case PenMovementType.Line:
                                    --ipos; // one step back
                                    buffer_point1 = ReadPlot(arrInPath, ref ipos);
                                    if (InAbsoluteCoord)
                                    {
                                        buffer_point1 = ToRelativeCoord(buffer_point1);
                                    }
                                    MovePen(buffer_point1);
                                    ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                    break;
                                case PenMovementType.CubicBezier:
                                    --ipos; // one step back
                                    buffer_point1 = ReadPlot(arrInPath, ref ipos);
                                    buffer_point_P2 = ReadPlot(arrInPath, ref ipos);
                                    buffer_point_P3 = ReadPlot(arrInPath, ref ipos);
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
                                    buffer_integer = ReadValue(plot);
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
                                    buffer_integer = ReadValue(plot);
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
                                    --ipos; // one step back
                                    buffer_point1 = ReadPlot(arrInPath, ref ipos);
                                    buffer_integer = ReadValue(arrInPath[++ipos]);
                                    buffer_arc_lg = ReadArcComponent(arrInPath[++ipos]);
                                    buffer_sweep = ReadArcComponent(arrInPath[++ipos]);
                                    buffer_point_P2 = ReadPlot(arrInPath, ref ipos);
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
                                default:
                                    Console.WriteLine("unrecognised plot {0}", plot);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            //
                            Console.WriteLine("this was not a point");
                            Console.WriteLine(ex.ToString());
                        }
                        break;
                }
                ipos++;
            }
        }

        private static Point ReadPlot(string plot)
        {
            string[] point = plot.Split(',');
            if (point.Length == 2)
            {
                bool setx = float.TryParse(point[0], out float readX);
                bool sety = float.TryParse(point[1], out float readY);
                if (setx && sety)
                {
                    return new Point((int)readX, (int)readY);
                }
            }
            Console.WriteLine($"Plot (',' not found): {plot}");
            throw new Exception("Point ',' not found");
        }

        private static Point ReadPlot(string[] arrInPath, ref int ipos)
        {
            string plot = arrInPath[++ipos];
            if(plot.IndexOf(',') > 0)
            {
                return ReadPlot(plot);
            }
            string[] pts = plot.Split('.');
            if (pts.Length <= 2) { 
                bool setx = int.TryParse(pts[0], out int readX);
                plot = arrInPath[++ipos];
                pts = plot.Split('.');
                bool sety = int.TryParse(pts[0], out int readY);
                if(setx && sety)
                {
                    return new Point(readX, readY);
                }
            }

            Console.WriteLine($"Plot (v2 not found): {plot}");
            throw new Exception("Point v2 not found");
        }

        private static int ReadValue(string plot)
        {
            bool setv = float.TryParse(plot, out float readv);
            if (setv)
            {
                return (int)readv;
            }
            Console.WriteLine($"Value (not found): {plot}");
            throw new Exception("Value not found");
        }

        private static bool ReadArcComponent(string plot)
        {
            if(plot == "1")
            {
                return true;
            }
            if (plot == "0")
            {
                return false;
            }
            Console.WriteLine($"Arc detail (not found): {plot}");
            throw new Exception("Arc detail error");
        }
    }
}
