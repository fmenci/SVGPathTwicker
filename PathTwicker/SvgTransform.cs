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
    // The 2D affine matrix [A C E; B D F; 0 0 1] an SVG transform list boils down to. Its translation (E,F) is
    // what the tool reports as the position on the map; its linear part (A B C D) is baked into the geometry.
    internal readonly record struct Affine(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Affine Identity = new(1, 0, 0, 1, 0, 0);

        public bool LinearIsIdentity =>
            Math.Abs(A - 1) < 1e-9 && Math.Abs(B) < 1e-9 && Math.Abs(C) < 1e-9 && Math.Abs(D - 1) < 1e-9;

        public double Determinant => A * D - B * C;

        // this * other: 'other' is applied to a point first
        public Affine Multiply(Affine o) => new(
            A * o.A + C * o.B, B * o.A + D * o.B,
            A * o.C + C * o.D, B * o.C + D * o.D,
            A * o.E + C * o.F + E, B * o.E + D * o.F + F);

        public (double X, double Y) ApplyLinear(double x, double y) => (A * x + C * y, B * x + D * y);

        // Splits the linear part as rotation * rest, where the remainder (scale, skew, flip) has no rotation left in it:
        // the angle is where the x axis ends up, in degrees. The translation stays with 'rest'.
        public (double RotationDegrees, Affine Remainder) SplitRotation()
        {
            double a = Math.Sqrt(A * A + B * B);
            if (a < 1e-12)
            {
                return (0, this);
            }
            double angle = Math.Atan2(B, A) * 180 / Math.PI;
            return (angle, new Affine(a, 0, (A * C + B * D) / a, Determinant / a, E, F));
        }

        // Reads a transform list: translate, scale, rotate, skewX, skewY and matrix, in the attribute form
        // ("rotate(45 10 10)") or the SVG2 CSS form ("rotate(45deg)", "translate(10px, 5px)").
        public static bool TryParse(string s, out Affine result)
        {
            result = Identity;
            Affine m = Identity;
            int pos = 0;
            while (true)
            {
                SvgPathMapElement.SkipCommaWsp(s, ref pos);
                if (pos >= s.Length)
                {
                    break;
                }
                int nameStart = pos;
                while (pos < s.Length && char.IsAsciiLetter(s[pos]))
                {
                    pos++;
                }
                string name = s.Substring(nameStart, pos - nameStart);
                while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                {
                    pos++;
                }
                if (name.Length == 0 || pos >= s.Length || s[pos] != '(')
                {
                    return false;
                }
                pos++;
                List<(double Value, string Unit)> args = [];
                while (true)
                {
                    SvgPathMapElement.SkipCommaWsp(s, ref pos);
                    if (pos < s.Length && s[pos] == ')')
                    {
                        pos++;
                        break;
                    }
                    if (!SvgPathMapElement.TryReadNumber(s, ref pos, out double v))
                    {
                        return false;
                    }
                    int unitStart = pos;
                    while (pos < s.Length && (char.IsAsciiLetter(s[pos]) || s[pos] == '%'))
                    {
                        pos++;
                    }
                    args.Add((v, s.Substring(unitStart, pos - unitStart)));
                }
                if (!TryFunction(name, args, out Affine f))
                {
                    return false;
                }
                m = m.Multiply(f);
            }
            result = m;
            return true;
        }

        private static bool TryFunction(string name, List<(double Value, string Unit)> a, out Affine f)
        {
            f = Identity;
            switch (name)
            {
                case "translate" when a.Count is 1 or 2:
                    f = new(1, 0, 0, 1, a[0].Value, a.Count == 2 ? a[1].Value : 0);
                    return true;
                case "scale" when a.Count is 1 or 2:
                    f = new(a[0].Value, 0, 0, a.Count == 2 ? a[1].Value : a[0].Value, 0, 0);
                    return true;
                case "rotate" when a.Count is 1 or 3:
                    double rad = Degrees(a[0]) * Math.PI / 180;
                    Affine rot = new(Math.Cos(rad), Math.Sin(rad), -Math.Sin(rad), Math.Cos(rad), 0, 0);
                    // rotate(angle cx cy) turns around that point: translate(cx cy) rotate(angle) translate(-cx -cy)
                    f = a.Count == 3
                        ? new Affine(1, 0, 0, 1, a[1].Value, a[2].Value).Multiply(rot).Multiply(new Affine(1, 0, 0, 1, -a[1].Value, -a[2].Value))
                        : rot;
                    return true;
                case "skewX" when a.Count == 1:
                    f = new(1, 0, Math.Tan(Degrees(a[0]) * Math.PI / 180), 1, 0, 0);
                    return true;
                case "skewY" when a.Count == 1:
                    f = new(1, Math.Tan(Degrees(a[0]) * Math.PI / 180), 0, 1, 0, 0);
                    return true;
                case "matrix" when a.Count == 6:
                    f = new(a[0].Value, a[1].Value, a[2].Value, a[3].Value, a[4].Value, a[5].Value);
                    return true;
                default:
                    return false;
            }
        }

        // an SVG attribute angle is in degrees; the CSS form may carry a unit
        private static double Degrees((double Value, string Unit) angle) => angle.Unit switch
        {
            "rad" => angle.Value * 180 / Math.PI,
            "grad" => angle.Value * 0.9,
            "turn" => angle.Value * 360,
            _ => angle.Value
        };
    }
}
