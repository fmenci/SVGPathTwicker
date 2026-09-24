
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

        // a path element with no children may be self-closed, or written as an explicit open/close pair
        private readonly Regex rexGrabPaths = new(@"<path\s+(?<pathgrab>[^>]+?)\s*(?:/>|>\s*</path\s*>)", RegexOptions.Multiline);
        // '+' is a legal explicit sign in the SVG number grammar (e.g. "+50" or the exponent in "1e+3"),
        // and whitespace separators may also be tabs/newlines, not just plain spaces
        private readonly Regex rexDrawing = new(ATTR_START + @"d=" + QUOTE + @"(?<drawingdata>[0-9mMzZlLcChHvVaAqQtTsSeE ,.+\t\r\n-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexId = new(ATTR_START + @"id=" + QUOTE + @"(?<idattr>[0-9a-zA-Z_-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexCssClass = new(ATTR_START + @"class=" + QUOTE + @"(?<classattr>[0-9a-zA-Z_-]+)" + ENDQUOTE, RegexOptions.CultureInvariant);
        private readonly Regex rexCssStyle = new(ATTR_START + @"style=" + QUOTE + @"(?<styleattr>[^""']+)" + ENDQUOTE, RegexOptions.CultureInvariant);

        // a translate() offset: sign/decimals allowed (e.g. translate(139.99999,77.47527)), and the y component is
        // optional per the SVG spec (translate(x) implies y=0)
        private const string TRANSLATE_NUMBER = @"-?[0-9]*\.?[0-9]+";
        private readonly Regex rexTransformTranslate = new(
            ATTR_START + @"transform=" + QUOTE + @"translate\(\s*(?<translatex>" + TRANSLATE_NUMBER + @")(?:\s*[,\s]\s*(?<translatey>" + TRANSLATE_NUMBER + @"))?\s*\)" + ENDQUOTE,
            RegexOptions.CultureInvariant);
        // SVG2 also allows `transform` to be set as a CSS property (e.g. style="transform: translate(10px,20px)")
        private readonly Regex rexCssTransformTranslate = new(
            @"transform\s*:\s*translate\(\s*(?<translatex>" + TRANSLATE_NUMBER + @")(?:px)?(?:\s*[,\s]\s*(?<translatey>" + TRANSLATE_NUMBER + @")(?:px)?)?\s*\)",
            RegexOptions.CultureInvariant);

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
                string robotfile = await File.ReadAllTextAsync(file);
                var m = rexGrabPaths.Matches(robotfile);
                int autoNameIndex = 0;
                foreach (Match mGrab in m)
                {
                    //Console.WriteLine("***");
                    string pathElement = mGrab.Groups["pathgrab"].Value;
                    //Console.WriteLine(pathElement);
                    //Console.WriteLine("+++");
                    string drawingPath = string.Empty;
                    var mdrawing = rexDrawing.Match(pathElement);
                    if (mdrawing.Success)
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
                        int translatex = 0;
                        int translatey = 0;
                        var mtransform = rexTransformTranslate.Match(pathElement);
                        if (mtransform.Success)
                        {
                            ParseTranslate(mtransform.Groups["translatex"], mtransform.Groups["translatey"], out translatex, out translatey);
                        }
                        else if (!string.IsNullOrEmpty(cssStyle))
                        {
                            // no `transform` attribute: SVG2 also allows it as a CSS property in `style`
                            var mCssTransform = rexCssTransformTranslate.Match(cssStyle);
                            if (mCssTransform.Success)
                            {
                                ParseTranslate(mCssTransform.Groups["translatex"], mCssTransform.Groups["translatey"], out translatex, out translatey);
                            }
                        }
                        //Console.WriteLine("move pen to : {0}, {1}", translatex, translatey);

                        drawingPath = mdrawing.Groups["drawingdata"].Value;
                        SvgPathMapElement svgPathMap = new(translatex, translatey, drawingPath);

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
                        Console.WriteLine("no path ?");
                        //Console.WriteLine(pathElement);
                    }
                    //Console.WriteLine("===");
                }
            }

            using StreamWriter sw = new(Path.Combine(SourceDirectory, "export_svg.csv"));
            await sw.WriteAsync(strOutput.ToString());
            Console.WriteLine("SVG Path extract ready");
        }

        // translate() y is optional (translate(x) implies y=0), and values may carry decimals: round to
        // the nearest int, like every other coordinate in this tool.
        private static void ParseTranslate(Group xGroup, Group yGroup, out int translatex, out int translatey)
        {
            translatex = 0;
            translatey = 0;
            if (xGroup.Success && double.TryParse(xGroup.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double foundx))
            {
                translatex = SvgPathMapElement.RoundToInt(foundx);
            }
            if (yGroup.Success && double.TryParse(yGroup.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double foundy))
            {
                translatey = SvgPathMapElement.RoundToInt(foundy);
            }
        }
    }
}
