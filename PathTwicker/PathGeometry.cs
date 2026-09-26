/*
 *

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.

 * */

namespace SVGPathTwicker
{
    // The real extent of a drawing: its curves and arcs are sampled into points, whose convex hull gives the
    // smallest rectangle (of any orientation) that holds the whole drawing.
    internal static class PathGeometry
    {
        private const int CurveSteps = 24;

        // Points along an absolute path, as PathTransformer writes it (M L C S Q T A Z, one segment per command).
        public static bool TryFlatten(string d, out List<(double X, double Y)> points)
        {
            points = [];
            int pos = 0;
            double px = 0, py = 0, startX = 0, startY = 0;
            // last control point, for the reflection a smooth curve (S, T) implies
            double cx = 0, cy = 0;
            char lastKind = '\0';

            while (pos < d.Length)
            {
                char? read = SvgPathMapElement.ReadCommand(d, ref pos);
                if (read is null)
                {
                    SvgPathMapElement.SkipCommaWsp(d, ref pos);
                    return pos >= d.Length;
                }
                char kind = char.ToUpperInvariant(read.Value);
                switch (kind)
                {
                    case 'M':
                    case 'L':
                        if (!Pair(d, ref pos, out double x, out double y)) { return false; }
                        points.Add((x, y));
                        if (kind == 'M') { startX = x; startY = y; }
                        px = x; py = y;
                        break;
                    case 'C':
                    {
                        if (!Pair(d, ref pos, out double x1, out double y1) ||
                            !Pair(d, ref pos, out double x2, out double y2) ||
                            !Pair(d, ref pos, out x, out y)) { return false; }
                        AddCubic(points, px, py, x1, y1, x2, y2, x, y);
                        cx = x2; cy = y2; px = x; py = y;
                        break;
                    }
                    case 'S':
                    {
                        if (!Pair(d, ref pos, out double x2, out double y2) || !Pair(d, ref pos, out x, out y)) { return false; }
                        double x1 = lastKind is 'C' or 'S' ? 2 * px - cx : px;
                        double y1 = lastKind is 'C' or 'S' ? 2 * py - cy : py;
                        AddCubic(points, px, py, x1, y1, x2, y2, x, y);
                        cx = x2; cy = y2; px = x; py = y;
                        break;
                    }
                    case 'Q':
                    {
                        if (!Pair(d, ref pos, out double x1, out double y1) || !Pair(d, ref pos, out x, out y)) { return false; }
                        AddQuadratic(points, px, py, x1, y1, x, y);
                        cx = x1; cy = y1; px = x; py = y;
                        break;
                    }
                    case 'T':
                    {
                        if (!Pair(d, ref pos, out x, out y)) { return false; }
                        double x1 = lastKind is 'Q' or 'T' ? 2 * px - cx : px;
                        double y1 = lastKind is 'Q' or 'T' ? 2 * py - cy : py;
                        AddQuadratic(points, px, py, x1, y1, x, y);
                        cx = x1; cy = y1; px = x; py = y;
                        break;
                    }
                    case 'A':
                        if (!Pair(d, ref pos, out double rx, out double ry) ||
                            !SvgPathMapElement.TryReadNumber(d, ref pos, out double rotation) ||
                            !SvgPathMapElement.TryReadFlag(d, ref pos, out bool large) ||
                            !SvgPathMapElement.TryReadFlag(d, ref pos, out bool sweep) ||
                            !Pair(d, ref pos, out x, out y)) { return false; }
                        AddArc(points, px, py, rx, ry, rotation, large, sweep, x, y);
                        px = x; py = y;
                        break;
                    case 'Z':
                        px = startX; py = startY;
                        break;
                }
                lastKind = kind;
            }
            return true;
        }

        private static bool Pair(string d, ref int pos, out double x, out double y)
        {
            y = 0;
            return SvgPathMapElement.TryReadNumber(d, ref pos, out x) && SvgPathMapElement.TryReadNumber(d, ref pos, out y);
        }

        private static void AddCubic(List<(double X, double Y)> pts, double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
        {
            for (int i = 1; i <= CurveSteps; i++)
            {
                double t = (double)i / CurveSteps, u = 1 - t;
                pts.Add((u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
                         u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3));
            }
        }

        private static void AddQuadratic(List<(double X, double Y)> pts, double x0, double y0, double x1, double y1, double x2, double y2)
        {
            for (int i = 1; i <= CurveSteps; i++)
            {
                double t = (double)i / CurveSteps, u = 1 - t;
                pts.Add((u * u * x0 + 2 * u * t * x1 + t * t * x2, u * u * y0 + 2 * u * t * y1 + t * t * y2));
            }
        }

        // endpoint to centre parameterisation of an elliptical arc (SVG implementation notes, F.6.5)
        private static void AddArc(List<(double X, double Y)> pts, double x1, double y1, double rx, double ry, double rotation, bool large, bool sweep, double x2, double y2)
        {
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if (rx == 0 || ry == 0 || (x1 == x2 && y1 == y2))
            {
                pts.Add((x2, y2));
                return;
            }
            double phi = rotation * Math.PI / 180;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2;
            double x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
            // radii too small to span the endpoints are scaled up until they just do
            double lambda = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry);
            if (lambda > 1)
            {
                double s = Math.Sqrt(lambda);
                rx *= s;
                ry *= s;
            }
            double numerator = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
            double denominator = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
            double coefficient = Math.Sqrt(Math.Max(0, numerator / denominator)) * (large == sweep ? -1 : 1);
            double cxp = coefficient * rx * y1p / ry, cyp = -coefficient * ry * x1p / rx;
            double cx = cos * cxp - sin * cyp + (x1 + x2) / 2;
            double cy = sin * cxp + cos * cyp + (y1 + y2) / 2;
            double ux = (x1p - cxp) / rx, uy = (y1p - cyp) / ry;
            double vx = (-x1p - cxp) / rx, vy = (-y1p - cyp) / ry;
            double start = Math.Atan2(uy, ux);
            double delta = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            if (!sweep && delta > 0) { delta -= 2 * Math.PI; }
            if (sweep && delta < 0) { delta += 2 * Math.PI; }
            int steps = Math.Max(CurveSteps, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 36)));
            for (int i = 1; i <= steps; i++)
            {
                double t = start + delta * i / steps;
                pts.Add((cx + rx * cos * Math.Cos(t) - ry * sin * Math.Sin(t),
                         cy + rx * sin * Math.Cos(t) + ry * cos * Math.Sin(t)));
            }
        }

        // The angle (degrees, in (-45, 45]) the drawing must be turned by so that its smallest enclosing
        // rectangle becomes axis-aligned, or 0 when turning would not be worth it. A rectangle of minimal area
        // always has a side along one edge of the convex hull, so only those orientations are tried.
        public static double BestRotation(List<(double X, double Y)> points)
        {
            var hull = ConvexHull(points);
            if (hull.Count < 2)
            {
                return 0;
            }
            double axisArea = AreaAt(hull, 0);
            double bestArea = axisArea;
            double bestAngle = 0;
            for (int i = 0; i < hull.Count; i++)
            {
                var a = hull[i];
                var b = hull[(i + 1) % hull.Count];
                double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
                double area = AreaAt(hull, angle);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestAngle = angle;
                }
            }
            // a rectangle looks the same every quarter turn: keep the smallest turn
            double degrees = bestAngle * 180 / Math.PI;
            degrees -= 90 * Math.Round(degrees / 90);
            if (degrees <= -45) { degrees += 90; }
            // only worth it when the box really shrinks (this also keeps circles and axis-aligned shapes as they are)
            if (Math.Abs(degrees) < 0.5 || bestArea > axisArea * 0.99)
            {
                return 0;
            }
            return degrees;
        }

        // area of the hull's bounding box once turned by -angle (radians)
        private static double AreaAt(List<(double X, double Y)> hull, double angle)
        {
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var (x, y) in hull)
            {
                double rx = x * cos + y * sin, ry = -x * sin + y * cos;
                minX = Math.Min(minX, rx); maxX = Math.Max(maxX, rx);
                minY = Math.Min(minY, ry); maxY = Math.Max(maxY, ry);
            }
            return (maxX - minX) * (maxY - minY);
        }

        // Andrew's monotone chain
        private static List<(double X, double Y)> ConvexHull(List<(double X, double Y)> points)
        {
            var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            if (pts.Count < 3)
            {
                return pts;
            }
            static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
                (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
            var hull = new List<(double X, double Y)>();
            foreach (var p in pts)
            {
                while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(p);
            }
            int lower = hull.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= lower && Cross(hull[^2], hull[^1], pts[i]) <= 0) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(pts[i]);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }
    }
}
