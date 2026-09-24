
/*
 * 

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.  

 * */

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SVGPathTwicker
{
    public class SvgFileExtractor
    {
        public const string DefaultSourceDirectory = @"C:\Temp\Svg\";
        private readonly string SourceDirectory;
        private readonly List<SvgPathMapElement> AllPath = [];

        // attribute values may be single- or double-quoted (both are valid XML/SVG, any spec version)
        private const string QUOTE = @"(?<q>[""'])";
        private const string ENDQUOTE = @"\k<q>";
        // an attribute name never starts mid-identifier, so this guards e.g. "d=" from matching inside "id="
        private const string ATTR_START = @"(?<![A-Za-z0-9_:.-])";

        // path plus the basic shape elements: rect/circle/ellipse/polyline/polygon/line all have well-defined
        // equivalent path data (see BuildShapePath), so they are read the same way and converted on the fly.
        // An element with no children may be self-closed, or written as an explicit open/close pair.
        // The groups are matched too (opening, self-closed and closing tags), in document order, because a
        // group's transform applies to everything it contains.
        private readonly Regex rexGrabElements = new(
            @"<(?<tag>path|rect|circle|ellipse|polyline|polygon|line)\s+(?<attrs>[^>]+?)\s*(?:/>|>\s*</\k<tag>\s*>)" +
            @"|(?<gopen><g(?:\s(?<gattrs>[^>]*?))?\s*(?<gself>/)?>)" +
            @"|(?<gclose></g\s*>)",
            RegexOptions.Multiline);
        private readonly Regex rexComment = new(@"<!--.*?-->", RegexOptions.Singleline);
        private readonly Regex rexPoints = new(ATTR_START + @"points=" + QUOTE + @"(?<pts>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        // '+' is a legal explicit sign in the SVG number grammar (e.g. "+50" or the exponent in "1e+3"),
        // and whitespace separators may also be tabs/newlines, not just plain spaces
        private readonly Regex rexDrawing = new(ATTR_START + @"d=" + QUOTE + @"(?<drawingdata>[0-9mMzZlLcChHvVaAqQtTsSeE ,.+\t\r\n-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexId = new(ATTR_START + @"id=" + QUOTE + @"(?<idattr>[0-9a-zA-Z_-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexCssClass = new(ATTR_START + @"class=" + QUOTE + @"(?<classattr>[0-9a-zA-Z_-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexCssStyle = new(ATTR_START + @"style=" + QUOTE + @"(?<styleattr>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);

        private const string TRANSLATE_NUMBER = @"-?[0-9]*\.?[0-9]+";
        // the whole transform list (see Affine.TryParse), as an attribute...
        private readonly Regex rexTransform = new(ATTR_START + @"transform=" + QUOTE + @"(?<list>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        // ...or, in SVG2, as a CSS property (e.g. style="transform: translate(10px,20px) rotate(45deg)")
        private readonly Regex rexCssTransform = new(@"(?<![A-Za-z0-9_-])transform\s*:\s*(?<list>[^;]+)", RegexOptions.CultureInvariant);

        public SvgFileExtractor(string? sourceDirectory = null)
        {
            SourceDirectory = string.IsNullOrWhiteSpace(sourceDirectory) ? DefaultSourceDirectory : sourceDirectory;
        }

        public async Task Init()
        {
            await ReadAsync();
        }

        public List<SvgPathMapElement> GetPaths()
        {
            return AllPath;
        }

        private async Task ReadAsync()
        {
            StringBuilder strOutput = new ();
            strOutput.Append("Layer;Category;Name;Row;");
            strOutput.Append(SvgPathMapElement.CSVHeader);
            int irow = 0;
            if (!Directory.Exists(SourceDirectory))
            {
                Console.WriteLine("source directory not found: {0}", SourceDirectory);
                return;
            }
            string[] files = Directory.GetFiles(SourceDirectory, "*.svg");
            Console.WriteLine("fetching all *.svg files from {0}", SourceDirectory);
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                // a commented-out element must not be read
                string robotfile = rexComment.Replace(await File.ReadAllTextAsync(file), string.Empty);
                var m = rexGrabElements.Matches(robotfile);
                int autoNameIndex = 0;
                // the transform each open group has accumulated with its ancestors' (innermost on top)
                Stack<Affine> groupStack = new();
                foreach (Match mGrab in m)
                {
                    if (mGrab.Groups["gclose"].Success)
                    {
                        if (groupStack.Count > 0)
                        {
                            groupStack.Pop();
                        }
                        continue;
                    }
                    if (mGrab.Groups["gopen"].Success)
                    {
                        if (!mGrab.Groups["gself"].Success)
                        {
                            // an empty <g/> holds nothing, so only a real opening tag starts a scope
                            Affine parent = groupStack.Count > 0 ? groupStack.Peek() : Affine.Identity;
                            groupStack.Push(parent.Multiply(ReadTransform(mGrab.Groups["gattrs"].Value, "g")));
                        }
                        continue;
                    }
                    //Console.WriteLine("***");
                    string tag = mGrab.Groups["tag"].Value;
                    string pathElement = mGrab.Groups["attrs"].Value;
                    //Console.WriteLine(pathElement);
                    //Console.WriteLine("+++");
                    // for a real <path>, use its own 'd'; for the other shapes, synthesize an equivalent one
                    bool haveDrawing = tag == "path"
                        ? TryGetPathDrawing(pathElement, out string drawingPath)
                        : TryBuildShapePath(tag, pathElement, out drawingPath);
                    if (haveDrawing)
                    {
                        string pathid = string.Empty;
                        string category = string.Empty;
                        var mpathid = rexId.Match(pathElement);
                        if (mpathid.Success)
                        {
                            pathid = mpathid.Groups["idattr"].Value;
                            //Console.WriteLine($"{pathid}");
                            int indUnderscore = pathid.IndexOf('_');
                            if ((indUnderscore > 0) && (indUnderscore < pathid.Length))
                            {
                                category = pathid.Substring(0, indUnderscore);
                                pathid = pathid.Substring(indUnderscore+1);
                            }
                        }
                        if (string.IsNullOrEmpty(pathid))
                        {
                            // element has no usable id: autogenerate a name unique within the file
                            pathid = $"path{++autoNameIndex}";
                        }
                        //Console.WriteLine("category : {0} - name : {1}", category, pathid);
                        string classname = string.Empty;
                        var mCssClass = rexCssClass.Match(pathElement);
                        if (mCssClass.Success)
                        {
                            classname = mCssClass.Groups["classattr"].Value;
                            //Console.WriteLine(classname);
                        }
                        string cssStyle = string.Empty;
                        var mCssStyle = rexCssStyle.Match(pathElement);
                        if (mCssStyle.Success)
                        {
                            cssStyle = mCssStyle.Groups["styleattr"].Value;
                            //Console.WriteLine(style);
                        }
                        // the element's own transform, merged with those of the groups around it: the element's
                        // is applied first, then its parent group's, and so on outwards
                        Affine parentTransform = groupStack.Count > 0 ? groupStack.Peek() : Affine.Identity;
                        Affine transform = parentTransform.Multiply(ReadTransform(pathElement, tag));
                        // the translation (position on the map) and the rotation angle are reported apart; the
                        // rest of the transform (scale, skew, flip) is applied to the drawing itself
                        var (rotation, rest) = transform.SplitRotation();
                        string? transformedPath = null;
                        if (!rest.LinearIsIdentity)
                        {
                            if (PathTransformer.TryTransform(drawingPath, rest, out string transformedData))
                            {
                                transformedPath = transformedData;
                            }
                            else
                            {
                                Console.WriteLine("path of <{0}> not understood, its transform ignored", tag);
                            }
                        }
                        //Console.WriteLine("move pen to : {0}, {1}", translatex, translatey);

                        SvgPathMapElement svgPathMap = new(SvgPathMapElement.RoundToInt(rest.E), SvgPathMapElement.RoundToInt(rest.F), drawingPath, transformedPath, rotation);

                        //  write CSV 
                        strOutput.Append('"');
                        strOutput.Append(filename);
                        strOutput.Append('"');
                        strOutput.Append(';');
                        strOutput.Append('"');
                        strOutput.Append(category);
                        strOutput.Append('"');
                        strOutput.Append(';');
                        strOutput.Append('"');
                        strOutput.Append(pathid);
                        strOutput.Append('"');
                        strOutput.Append(';');
                        strOutput.Append(++irow);
                        strOutput.Append(';');
                        svgPathMap.AddCsv(ref strOutput);
                        strOutput.Append('"');
                        strOutput.Append(classname);
                        strOutput.Append('"');
                        strOutput.Append(';');
                        strOutput.Append('"');
                        strOutput.Append(cssStyle);
                        strOutput.Append('"');
                        strOutput.Append(';');
                        strOutput.Append(Environment.NewLine);
                    }
                    else
                    {
                        Console.WriteLine("no usable drawing data on <{0}> ?", tag);
                        //Console.WriteLine(pathElement);
                    }
                    //Console.WriteLine("===");
                }
            }

            using StreamWriter sw = new(Path.Combine(SourceDirectory, "export_svg.csv"));
            await sw.WriteAsync(strOutput.ToString());
            Console.WriteLine("SVG Path extract ready");
        }

        // an element's own transform: the `transform` attribute, else (SVG2) the CSS property in `style`.
        // One that cannot be understood is reported and left out (identity).
        private Affine ReadTransform(string attrs, string tag)
        {
            string transformList = string.Empty;
            var mtransform = rexTransform.Match(attrs);
            if (mtransform.Success)
            {
                transformList = mtransform.Groups["list"].Value;
            }
            else
            {
                var mStyle = rexCssStyle.Match(attrs);
                var mCssTransform = mStyle.Success ? rexCssTransform.Match(mStyle.Groups["styleattr"].Value) : Match.Empty;
                if (mCssTransform.Success)
                {
                    transformList = mCssTransform.Groups["list"].Value;
                }
            }
            if (transformList.Length == 0)
            {
                return Affine.Identity;
            }
            if (!Affine.TryParse(transformList, out Affine transform))
            {
                Console.WriteLine("transform not understood on <{0}>, ignored: {1}", tag, transformList);
                return Affine.Identity;
            }
            return transform;
        }

        private bool TryGetPathDrawing(string pathElement, out string drawingPath)
        {
            var mdrawing = rexDrawing.Match(pathElement);
            drawingPath = mdrawing.Success ? mdrawing.Groups["drawingdata"].Value : string.Empty;
            return mdrawing.Success;
        }

        // rect/circle/ellipse/polyline/polygon each have a well-defined equivalent path, so they are
        // converted to synthetic 'd' data here and processed exactly like a real <path> from then on.
        // The synthesized data (not the original SVG source) is what ends up in the CSV's OriginalPath
        // column for these shapes, since they never had a literal 'd' attribute to preserve.
        private bool TryBuildShapePath(string tag, string element, out string d)
        {
            switch (tag)
            {
                case "rect": return TryBuildRectPath(element, out d);
                case "circle": return TryBuildCirclePath(element, out d);
                case "ellipse": return TryBuildEllipsePath(element, out d);
                case "polyline": return TryBuildPolyPath(element, closed: false, out d);
                case "polygon": return TryBuildPolyPath(element, closed: true, out d);
                case "line": return TryBuildLinePath(element, out d);
                default:
                    d = string.Empty;
                    return false;
            }
        }

        internal static string Num(double v)
        {
            string s = v.ToString("0.######", CultureInfo.InvariantCulture);
            return s == "-0" ? "0" : s;
        }

        private static bool TryGetNumericAttribute(string element, string name, out double value)
        {
            var m = Regex.Match(element, ATTR_START + name + "=" + QUOTE + @"(?<val>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);
            if (m.Success)
            {
                int pos = 0;
                if (SvgPathMapElement.TryReadNumber(m.Groups["val"].Value.Trim(), ref pos, out value))
                {
                    return true;
                }
            }
            value = 0;
            return false;
        }

        private static double GetNumericAttribute(string element, string name, double defaultValue)
        {
            return TryGetNumericAttribute(element, name, out double value) ? value : defaultValue;
        }

        private static bool TryBuildRectPath(string element, out string d)
        {
            // per spec, a rect with no (or non-positive) width/height is not rendered
            if (!TryGetNumericAttribute(element, "width", out double width) || width <= 0 ||
                !TryGetNumericAttribute(element, "height", out double height) || height <= 0)
            {
                d = string.Empty;
                return false;
            }
            double x = GetNumericAttribute(element, "x", 0);
            double y = GetNumericAttribute(element, "y", 0);
            bool haveRx = TryGetNumericAttribute(element, "rx", out double rx);
            bool haveRy = TryGetNumericAttribute(element, "ry", out double ry);
            if (haveRx && !haveRy) { ry = rx; haveRy = true; }
            if (haveRy && !haveRx) { rx = ry; haveRx = true; }
            if (haveRx && haveRy && rx > 0 && ry > 0)
            {
                // clamp per spec: a corner radius can never exceed half of the side it rounds
                rx = Math.Min(rx, width / 2);
                ry = Math.Min(ry, height / 2);
                double straightW = width - 2 * rx;
                double straightH = height - 2 * ry;
                d = $"m {Num(x + rx)},{Num(y)}" +
                    $" h {Num(straightW)} a {Num(rx)},{Num(ry)} 0 0 1 {Num(rx)},{Num(ry)}" +
                    $" v {Num(straightH)} a {Num(rx)},{Num(ry)} 0 0 1 {Num(-rx)},{Num(ry)}" +
                    $" h {Num(-straightW)} a {Num(rx)},{Num(ry)} 0 0 1 {Num(-rx)},{Num(-ry)}" +
                    $" v {Num(-straightH)} a {Num(rx)},{Num(ry)} 0 0 1 {Num(rx)},{Num(-ry)} z";
            }
            else
            {
                d = $"m {Num(x)},{Num(y)} h {Num(width)} v {Num(height)} h {Num(-width)} z";
            }
            return true;
        }

        private static bool TryBuildCirclePath(string element, out string d)
        {
            // per spec, a circle with no (or non-positive) radius is not rendered
            if (!TryGetNumericAttribute(element, "r", out double r) || r <= 0)
            {
                d = string.Empty;
                return false;
            }
            double cx = GetNumericAttribute(element, "cx", 0);
            double cy = GetNumericAttribute(element, "cy", 0);
            // a single arc cannot describe a full circle unambiguously: use two half-circle arcs instead
            d = $"m {Num(cx + r)},{Num(cy)} a {Num(r)},{Num(r)} 0 1 0 {Num(-2 * r)},0 a {Num(r)},{Num(r)} 0 1 0 {Num(2 * r)},0 z";
            return true;
        }

        private static bool TryBuildEllipsePath(string element, out string d)
        {
            bool haveRx = TryGetNumericAttribute(element, "rx", out double rx);
            bool haveRy = TryGetNumericAttribute(element, "ry", out double ry);
            if (haveRx && !haveRy) { ry = rx; haveRy = true; }
            if (haveRy && !haveRx) { rx = ry; haveRx = true; }
            // per spec, an ellipse with no (or non-positive) radius is not rendered
            if (!haveRx || !haveRy || rx <= 0 || ry <= 0)
            {
                d = string.Empty;
                return false;
            }
            double cx = GetNumericAttribute(element, "cx", 0);
            double cy = GetNumericAttribute(element, "cy", 0);
            d = $"m {Num(cx + rx)},{Num(cy)} a {Num(rx)},{Num(ry)} 0 1 0 {Num(-2 * rx)},0 a {Num(rx)},{Num(ry)} 0 1 0 {Num(2 * rx)},0 z";
            return true;
        }

        // a line has no area of its own: the pen's stroke-width gives its second dimension, so it becomes the
        // rectangle the stroke covers (butt caps), centred on the line: p1+n, p2+n, p2-n, p1-n
        // where n is the half-width normal to the line
        private static bool TryBuildLinePath(string element, out string d)
        {
            double x1 = GetNumericAttribute(element, "x1", 0);
            double y1 = GetNumericAttribute(element, "y1", 0);
            double x2 = GetNumericAttribute(element, "x2", 0);
            double y2 = GetNumericAttribute(element, "y2", 0);
            double dx = x2 - x1;
            double dy = y2 - y1;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double width = GetStrokeWidth(element);
            // a zero-length line or a zero-width pen paints nothing
            if (length <= 0 || width <= 0)
            {
                d = string.Empty;
                return false;
            }
            double nx = -dy / length * width / 2;
            double ny = dx / length * width / 2;
            d = $"m {Num(x1 + nx)},{Num(y1 + ny)} l {Num(dx)},{Num(dy)} l {Num(-2 * nx)},{Num(-2 * ny)} l {Num(-dx)},{Num(-dy)} z";
            return true;
        }

        // CSS (style) wins over the presentation attribute; SVG's default stroke-width is 1
        private static double GetStrokeWidth(string element)
        {
            var mStyle = Regex.Match(element, ATTR_START + @"style=" + QUOTE + @"(?<val>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);
            if (mStyle.Success)
            {
                var mWidth = Regex.Match(mStyle.Groups["val"].Value, @"(?<![A-Za-z0-9_-])stroke-width\s*:\s*(?<w>" + TRANSLATE_NUMBER + ")", RegexOptions.CultureInvariant);
                if (mWidth.Success && double.TryParse(mWidth.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double styled))
                {
                    return styled;
                }
            }
            return GetNumericAttribute(element, "stroke-width", 1);
        }

        // polyline stays open (no 'z'): the tool's own "every path shall close" rule (see
        // SvgPathMapElement) still closes it once fed through, the same as a <path> missing 'z'
        private bool TryBuildPolyPath(string element, bool closed, out string d)
        {
            var mPoints = rexPoints.Match(element);
            if (!mPoints.Success)
            {
                d = string.Empty;
                return false;
            }
            string pointsAttr = mPoints.Groups["pts"].Value;
            StringBuilder sb = new();
            int pos = 0;
            bool first = true;
            double prevX = 0, prevY = 0;
            while (SvgPathMapElement.TryReadNumber(pointsAttr, ref pos, out double x) &&
                   SvgPathMapElement.TryReadNumber(pointsAttr, ref pos, out double y))
            {
                // first point starts the path; every other one is the move from the previous point
                sb.Append(first ? "m " : " l ");
                sb.Append(Num(first ? x : x - prevX)).Append(',').Append(Num(first ? y : y - prevY));
                prevX = x;
                prevY = y;
                first = false;
            }
            if (first)
            {
                // not a single coordinate pair could be read
                d = string.Empty;
                return false;
            }
            if (closed)
            {
                sb.Append(" z");
            }
            d = sb.ToString();
            return true;
        }
    }
}
