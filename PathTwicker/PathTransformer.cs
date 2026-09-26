/*
 *

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.

 * */

using System.Text;

namespace SVGPathTwicker
{
    // Bakes the linear part of a transform (scale, rotation, skew, flip) into a path's geometry, returning
    // absolute path data. The transform's translation is left out on purpose: it is the element's position
    // on the map, reported separately.
    internal static class PathTransformer
    {
        public static bool TryTransform(string d, Affine t, out string result)
        {
            result = string.Empty;
            StringBuilder sb = new();
            int pos = 0;
            // pen and start of the current subpath, in the source's own (untransformed) absolute space
            double px = 0, py = 0, startX = 0, startY = 0;

            // the source's absolute point, once transformed
            void EmitPoint(double x, double y)
            {
                var (tx, ty) = t.ApplyLinear(x, y);
                sb.Append(' ').Append(SvgFileExtractor.Num(tx)).Append(',').Append(SvgFileExtractor.Num(ty));
            }

            bool HasNumber()
            {
                int p = pos;
                SvgPathMapElement.SkipCommaWsp(d, ref p);
                return p < d.Length && (char.IsAsciiDigit(d[p]) || d[p] is '+' or '-' or '.');
            }

            while (pos < d.Length)
            {
                char? read = SvgPathMapElement.ReadCommand(d, ref pos);
                if (read is null)
                {
                    // not a command, and nothing left to consume it as arguments of one: give up
                    return SkippedTrailingSeparators(d, pos);
                }
                char command = read.Value;
                bool relative = char.IsLower(command);
                char kind = char.ToUpperInvariant(command);
                if (kind == 'Z')
                {
                    sb.Append(" Z");
                    px = startX;
                    py = startY;
                    continue;
                }
                bool firstPair = true;
                do
                {
                    double x, y, x1, y1, x2, y2;
                    double ox = relative ? px : 0;
                    double oy = relative ? py : 0;
                    switch (kind)
                    {
                        case 'M':
                            if (!Pair(out x, out y)) { return false; }
                            x += ox; y += oy;
                            // the pairs after a moveto's first one are implicit linetos
                            sb.Append(firstPair ? " M" : " L");
                            EmitPoint(x, y);
                            if (firstPair) { startX = x; startY = y; }
                            px = x; py = y;
                            break;
                        case 'L':
                            if (!Pair(out x, out y)) { return false; }
                            x += ox; y += oy;
                            sb.Append(" L");
                            EmitPoint(x, y);
                            px = x; py = y;
                            break;
                        case 'H':
                            if (!Number(out x)) { return false; }
                            x += ox;
                            // no longer horizontal once rotated or skewed, so it is written as a lineto
                            sb.Append(" L");
                            EmitPoint(x, py);
                            px = x;
                            break;
                        case 'V':
                            if (!Number(out y)) { return false; }
                            y += oy;
                            sb.Append(" L");
                            EmitPoint(px, y);
                            py = y;
                            break;
                        case 'C':
                            if (!Pair(out x1, out y1) || !Pair(out x2, out y2) || !Pair(out x, out y)) { return false; }
                            sb.Append(" C");
                            EmitPoint(x1 + ox, y1 + oy);
                            EmitPoint(x2 + ox, y2 + oy);
                            EmitPoint(x + ox, y + oy);
                            px = x + ox; py = y + oy;
                            break;
                        case 'S':
                        case 'Q':
                            if (!Pair(out x1, out y1) || !Pair(out x, out y)) { return false; }
                            // an affine map commutes with the reflection a smooth curve implies, so 'S' stays 'S'
                            sb.Append(' ').Append(kind);
                            EmitPoint(x1 + ox, y1 + oy);
                            EmitPoint(x + ox, y + oy);
                            px = x + ox; py = y + oy;
                            break;
                        case 'T':
                            if (!Pair(out x, out y)) { return false; }
                            sb.Append(" T");
                            EmitPoint(x + ox, y + oy);
                            px = x + ox; py = y + oy;
                            break;
                        case 'A':
                            if (!Pair(out double rx, out double ry) ||
                                !Number(out double rotation) ||
                                !SvgPathMapElement.TryReadFlag(d, ref pos, out bool large) ||
                                !SvgPathMapElement.TryReadFlag(d, ref pos, out bool sweep) ||
                                !Pair(out x, out y))
                            { return false; }
                            x += ox; y += oy;
                            AppendArc(sb, t, Math.Abs(rx), Math.Abs(ry), rotation, large, sweep, x, y);
                            px = x; py = y;
                            break;
                    }
                    firstPair = false;
                } while (HasNumber());
            }
            result = sb.ToString().Trim();
            return true;

            bool Number(out double v) => SvgPathMapElement.TryReadNumber(d, ref pos, out v);
            bool Pair(out double a, out double b)
            {
                a = b = 0;
                return SvgPathMapElement.TryReadNumber(d, ref pos, out a) && SvgPathMapElement.TryReadNumber(d, ref pos, out b);
            }
        }

        private static bool SkippedTrailingSeparators(string d, int pos)
        {
            SvgPathMapElement.SkipCommaWsp(d, ref pos);
            return pos >= d.Length;
        }

        // The image of an ellipse under a linear map is an ellipse: with u and v the images of its two
        // half-axes, its shape matrix is u.uT + v.vT, and its new radii and tilt are that matrix's
        // eigenvalues and eigenvector. A mirror (negative determinant) reverses the sweep direction.
        private static void AppendArc(StringBuilder sb, Affine t, double rx, double ry, double rotation, bool large, bool sweep, double x, double y)
        {
            var (tx, ty) = t.ApplyLinear(x, y);
            if (rx == 0 || ry == 0)
            {
                // a zero radius makes the arc a straight line
                sb.Append(" L ").Append(SvgFileExtractor.Num(tx)).Append(',').Append(SvgFileExtractor.Num(ty));
                return;
            }
            double phi = rotation * Math.PI / 180;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);
            double ux = rx * (t.A * cos + t.C * sin), uy = rx * (t.B * cos + t.D * sin);
            double vx = ry * (-t.A * sin + t.C * cos), vy = ry * (-t.B * sin + t.D * cos);
            double p = ux * ux + vx * vx, q = ux * uy + vx * vy, r = uy * uy + vy * vy;
            double mean = (p + r) / 2;
            double spread = Math.Sqrt(((p - r) / 2) * ((p - r) / 2) + q * q);
            double newRx = Math.Sqrt(mean + spread);
            double newRy = Math.Sqrt(Math.Max(mean - spread, 0));
            double newRotation = 0.5 * Math.Atan2(2 * q, p - r) * 180 / Math.PI;
            bool newSweep = t.Determinant < 0 ? !sweep : sweep;
            sb.Append(" A ")
              .Append(SvgFileExtractor.Num(newRx)).Append(',').Append(SvgFileExtractor.Num(newRy)).Append(' ')
              .Append(SvgFileExtractor.Num(newRotation)).Append(' ')
              .Append(large ? 1 : 0).Append(' ').Append(newSweep ? 1 : 0).Append(' ')
              .Append(SvgFileExtractor.Num(tx)).Append(',').Append(SvgFileExtractor.Num(ty));
        }
    }
}
