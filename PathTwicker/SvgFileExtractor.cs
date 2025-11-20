
/*
 * 

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.  

 * */

using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace SVGPathTwicker
{
    public class SvgFileExtractor
    {
        private const string STARTUPDIR = @"C:\Temp\Svg\";
        private readonly List<SvgPathMapElement> AllPath = [];
        private readonly Regex rexDrawing = new(@"\s*d=\u0022(?<drawingdata>[0-9mMzZlLcChHvVaAqQtTsSeE ,.-]+)\u0022", RegexOptions.CultureInvariant);
        private readonly Regex rexGrabPaths = new(@"<path\s+(?<pathgrab>[^>]+)/>", RegexOptions.Multiline);
        private readonly Regex rexId = new(@"\s*id=\u0022(?<idattr>[0-9a-zA-Z_-]+)\u0022", RegexOptions.CultureInvariant);
        private readonly Regex rexCssClass = new(@"\s*class=\u0022(?<classattr>[0-9a-zA-Z_-]+)\u0022", RegexOptions.CultureInvariant);
        private readonly Regex rexCssStyle = new(@"\s*style=\u0022(?<styleattr>[^\u0022*]+)\u0022", RegexOptions.CultureInvariant);
        private readonly Regex rexTransformTranslate = new(@"\s*transform=\u0022translate\((?<translatex>[0-9-]+),(?<translatey>[0-9-]+)\)\u0022", RegexOptions.CultureInvariant);
        public SvgFileExtractor() { 
            
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
            string[] files = Directory.GetFiles(STARTUPDIR, "*.svg");
            Console.WriteLine("fetching all *.svg files from {0}", STARTUPDIR);
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string robotfile = await File.ReadAllTextAsync(file);
                var m = rexGrabPaths.Matches(robotfile);
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
                            if (int.TryParse(mtransform.Groups["translatex"].Value, out int foundx))
                            {
                                translatex = foundx;
                            }
                            if (int.TryParse(mtransform.Groups["translatey"].Value, out int foundy))
                            {
                                translatey = foundy;
                            }
                            //Console.WriteLine("move pen to : {0}, {1}", translatex, translatey);
                        }

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

            using StreamWriter sw = new(Path.Combine(STARTUPDIR, "export_svg.csv"));
            await sw.WriteAsync(strOutput.ToString());
            Console.WriteLine("SVG Path extract ready");
        }
    }
}
