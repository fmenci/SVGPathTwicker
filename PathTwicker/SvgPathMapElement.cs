using System.Drawing;
using System.Text;

namespace PathTwicker
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
        public SvgPathMapElement(string originalPath)
        {
            OriginalPath = originalPath;
            Init();
        }

        public string ToCsv()
        {
            StringBuilder str = new();
            str.Append('"');
            str.Append(OriginalPath);
            str.Append('"');
            str.Append(';');
            str.Append(PathOrigin.X);
            str.Append(';');
            str.Append(PathOrigin.Y);
            str.Append(';');
            int lx = (P2x - P1x) / 2 + P1x;
            str.Append(lx);
            str.Append(';');
            int ly = (P2y - P1y) / 2 + P1y;
            str.Append(ly);
            str.Append(';');
            str.Append(P1x);
            str.Append(';');
            str.Append(P1y);
            str.Append(';');
            int w = P2x - P1x;
            str.Append(w);
            str.Append(';');
            int h = P2y - P1y;
            str.Append(h);
            str.Append(';');
            str.Append('"');
            str.Append(ImprovedPath);
            str.Append('"');
            str.Append(';');
            return str.ToString();
        }
        public string OriginalPath { get; private set; } = string.Empty;

        private Point PenOnPaper = Point.Empty;

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
                        //Console.Write("m");
                        PenMove = PenMovementType.Line;
                        InAbsoluteCoord = false;
                        PathNumber++;
                        if (PathNumber == 1)
                        {
                            PathOrigin = ReadPlot(arrInPath[++ipos]);
                            ImprovedPath.Append("m 0,0");
                        }
                        else
                        {
                            ImprovedPath.Append(" m");
                        }
                        break;
                    case "M":
                        //Console.Write("M");
                        PenMove = PenMovementType.Line;
                        InAbsoluteCoord = true;
                        PathNumber++;
                        if (PathNumber == 1)
                        {
                            PathOrigin = ReadPlot(arrInPath[++ipos]);
                            ImprovedPath.Append("m 0,0");
                        }
                        else
                        {
                            ImprovedPath.Append(" m");
                        }
                        break;
                    case "z":
                    case "Z": // all is converted to relative coordinate
                        //Console.Write(plot);
                        PenMove = PenMovementType.None;
                        ImprovedPath.Append(" z");
                        break;
                    case "l":
                        PenMove = PenMovementType.Line;
                        InAbsoluteCoord = false;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        MovePen(buffer_point1);
                        ImprovedPath.AppendFormat(" l {0},{1}", buffer_point1.X, buffer_point1.Y);
                        break;
                    case "L":
                        PenMove = PenMovementType.Line;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        buffer_point1 = ToRelativeCoord(buffer_point1);
                        InAbsoluteCoord = true;
                        MovePen(buffer_point1);
                        ImprovedPath.AppendFormat(" l {0},{1}", buffer_point1.X - PathOrigin.X, buffer_point1.Y - PathOrigin.Y);
                        break;
                    case "c":
                        PenMove = PenMovementType.CubicBezier;
                        InAbsoluteCoord = false;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
                        buffer_point_P3 = ReadPlot(arrInPath[++ipos]);
                        MovePen(buffer_point_P3);
                        ImprovedPath.Append(" c");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P3.X, buffer_point_P3.Y);
                        break;
                    case "C":
                        PenMove = PenMovementType.CubicBezier;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
                        buffer_point_P3 = ReadPlot(arrInPath[++ipos]);
                        InAbsoluteCoord = true;
                        // convert to relative coords
                        buffer_point1 = ToRelativeCoord(buffer_point1);
                        buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
                        buffer_point_P3 = ToRelativeCoord(buffer_point_P3);
                        MovePen(buffer_point_P3);
                        ImprovedPath.Append(" c");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P3.X, buffer_point_P3.Y);
                        break;
                    case "h":
                        PenMove = PenMovementType.Horizontal;
                        InAbsoluteCoord = false;
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        MovePen(new(buffer_integer, 0));
                        ImprovedPath.Append(" h ");
                        ImprovedPath.Append(buffer_integer);
                        break;
                    case "H":
                        //Console.Write("H");
                        PenMove = PenMovementType.Horizontal;
                        InAbsoluteCoord = true;
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        buffer_integer -= PathOrigin.X;
                        buffer_integer -= PenOnPaper.X;
                        MovePen(new(buffer_integer, 0));
                        ImprovedPath.Append(" h ");
                        ImprovedPath.Append(buffer_integer);
                        break;
                    case "v":
                        PenMove = PenMovementType.Vertical;
                        InAbsoluteCoord = false;
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        MovePen(new(0, buffer_integer));
                        ImprovedPath.Append(" v ");
                        ImprovedPath.Append(buffer_integer);
                        break;
                    case "V":
                        //Console.Write("V");
                        PenMove = PenMovementType.Vertical;
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        buffer_point1 = new(0, buffer_integer); 
                        buffer_point1 = ToRelativeCoord(buffer_point1);
                        InAbsoluteCoord = true;
                        MovePen(buffer_point1);
                        ImprovedPath.Append(" v ");
                        ImprovedPath.Append(buffer_point1.Y);
                        break;
                    case "a":
                        //Console.Write("a");
                        PenMove = PenMovementType.Arc;
                        InAbsoluteCoord = false;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        buffer_arc_lg = ReadArcComponent(arrInPath[++ipos]);
                        buffer_sweep = ReadArcComponent(arrInPath[++ipos]);
                        buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
                        MovePen(buffer_point_P2);
                        ImprovedPath.Append(" a");
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                        ImprovedPath.AppendFormat(" {0}", buffer_integer);
                        ImprovedPath.AppendFormat(" {0}", buffer_arc_lg ? 1 : 0);
                        ImprovedPath.AppendFormat(" {0}", buffer_sweep ? 1 : 0);
                        ImprovedPath.AppendFormat(" {0},{1}", buffer_point_P2.X, buffer_point_P2.Y);
                        break;
                    case "A":
                        //Console.Write("A");
                        PenMove = PenMovementType.Arc;
                        InAbsoluteCoord = false;
                        buffer_point1 = ReadPlot(arrInPath[++ipos]);
                        buffer_integer = ReadValue(arrInPath[++ipos]);
                        buffer_arc_lg = ReadArcComponent(arrInPath[++ipos]);
                        buffer_sweep = ReadArcComponent(arrInPath[++ipos]);
                        buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
                        buffer_point_P2 = ToRelativeCoord(buffer_point_P2);
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
                                    buffer_point1 = ReadPlot(plot);
                                    if (InAbsoluteCoord)
                                    {
                                        buffer_point1 = ToRelativeCoord(buffer_point1);
                                    }
                                    MovePen(buffer_point1);
                                    ImprovedPath.AppendFormat(" {0},{1}", buffer_point1.X, buffer_point1.Y);
                                    break;
                                case PenMovementType.CubicBezier:
                                    buffer_point1 = ReadPlot(plot);
                                    buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
                                    buffer_point_P3 = ReadPlot(arrInPath[++ipos]);
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
                                    ImprovedPath.Append(' ');
                                    ImprovedPath.Append(buffer_integer);
                                    break;
                                case PenMovementType.Vertical:
                                    buffer_integer = ReadValue(plot);
                                    if (InAbsoluteCoord)
                                    {
                                        buffer_integer -= PathOrigin.Y;
                                        buffer_integer -= PenOnPaper.Y;
                                    }
                                    MovePen(new(0, buffer_integer));
                                    ImprovedPath.Append(' ');
                                    ImprovedPath.Append(buffer_integer);
                                    break;
                                case PenMovementType.Arc:
                                    buffer_point1 = ReadPlot(plot);
                                    buffer_integer = ReadValue(arrInPath[++ipos]);
                                    buffer_arc_lg = ReadArcComponent(arrInPath[++ipos]);
                                    buffer_sweep = ReadArcComponent(arrInPath[++ipos]);
                                    buffer_point_P2 = ReadPlot(arrInPath[++ipos]);
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
                                    Console.WriteLine("unrecognised plot {0}");
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
            Console.WriteLine($"Plot (not found): {plot}");
            throw new Exception("Point not found");
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
