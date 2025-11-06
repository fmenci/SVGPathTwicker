
/*
 * 

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.  

 * */

using System.Text;
using System.Text.RegularExpressions;

namespace SVGPathTwicker
{
    public class SvgFileExtractor
    {
        private const string STARTUPDIR = @"C:\Temp\Svg\";
        private readonly List<SvgPathMapElement> AllPath = [];
        private readonly Regex rexDrawing = new(@"\s+d=\u0022(?<pathdata>[0-9mMzZlLcChHvVaAqQtTsSeE ,.-]+)\u0022", RegexOptions.IgnoreCase);

        public SvgFileExtractor() { 
            
        }

        public void Init()
        {
            Task.Run(async () => { await ReadAsync(); });
        }

        public List<SvgPathMapElement> GetPaths()
        {
            return AllPath;
        }

        private async Task ReadAsync()
        {
            StringBuilder strOutput = new ();
            strOutput.Append("Name;Row;");
            strOutput.Append(SvgPathMapElement.CSVHeader);
            int irow = 0;
            string[] files = Directory.GetFiles(STARTUPDIR, "*.svg");
            Console.WriteLine("fetching all *.svg files from {0}", STARTUPDIR);
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                string robotfile = await File.ReadAllTextAsync(file);
                var m = rexDrawing.Matches(robotfile);
                foreach (Match m2 in m)
                {
                    strOutput.Append('"');
                    strOutput.Append(filename);
                    strOutput.Append('"');
                    strOutput.Append(';');
                    strOutput.Append(++irow);
                    strOutput.Append(';');
                    string drawingPath = m2.Groups["pathdata"].Value;
                    SvgPathMapElement svgPathMap = new(drawingPath);
                    svgPathMap.AddCsv(ref strOutput);
                }
            }

            using StreamWriter sw = new(Path.Combine(STARTUPDIR, "export_svg.csv"));
            await sw.WriteAsync(strOutput.ToString());
            Console.WriteLine("SVG Path extract ready");
            Console.WriteLine("you may press `enter` to quit");
        }
    }
}
