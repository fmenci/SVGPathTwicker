
using System.Text.RegularExpressions;

namespace PathTwicker
{
    public class SvgFileExtractor
    {
        private const string STARTUPDIR = @"C:\Temp\Svg\";
        private List<SvgPathMapElement> AllPath = [];
        private Regex rexDrawing = new(@"\s+d=\u0022(?<pathdata>[0-9mMzZlLcChHvVaAqQtTsS ,.-]+)\u0022", RegexOptions.IgnoreCase);

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
            int irow = 0;
            string[] files = Directory.GetFiles(STARTUPDIR, "*.svg");
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file);
                //Console.WriteLine(filename);
                string robotfile = await File.ReadAllTextAsync(file);
                var m = rexDrawing.Matches(robotfile);
                foreach (Match m2 in m)
                {
                    Console.Write("\"{0}\";", filename);
                    Console.Write("{0};", ++irow);
                    string drawingPath = m2.Groups["pathdata"].Value;
                    SvgPathMapElement svgPathMap = new(drawingPath);
                    Console.Write(svgPathMap.ToCsv());
                    //AllPath.Add(svgPathMap);
                    Console.Write(Environment.NewLine);
                }
                //Console.WriteLine("==");
            }

            //irow = 0;
            //foreach (var pa in AllPath)
            //{
            //    Console.Write("{0} - ", ++irow);
            //    Console.WriteLine(pa.OriginalPath);
            //    Console.WriteLine(pa.ReadImprovedPath());
            //    Console.WriteLine(pa.ToString());
            //}
            //Console.WriteLine("==");
            //Console.Write(Environment.NewLine);
            //foreach (var pa in AllPath)
            //{
            //    Console.WriteLine(pa.ToCsv());
            //}
            //Console.WriteLine("==");
        }
    }
}
