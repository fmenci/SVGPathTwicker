using SVGPathTwicker;

namespace SVGPathTweak.Tests
{
    // Whole-document tests: SVG files as each version of the specification (and Inkscape) would write them,
    // read through SvgFileExtractor, then the CSV rows checked.
    public class SvgVersionAndShapeTests
    {
        // CSV columns: Layer;Category;Name;Row;OriginalPath;Delta X;Delta Y;Rotation;Label X;Label Y;Box X;Box Y;Width;Height;ImprovedPath;CSS class;CSS style
        private const int Category = 1, Original = 4, DeltaX = 5, DeltaY = 6, Rotation = 7, BoxX = 10, BoxY = 11, Width = 12, Height = 13, Improved = 14, CssClass = 15;

        private static async Task<string> ExportCsv(string svg)
        {
            string dir = Path.Combine(Path.GetTempPath(), "SvgVersionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "doc.svg"), svg);
                await new SvgFileExtractor(dir).Init();
                return await File.ReadAllTextAsync(Path.Combine(dir, "export_svg.csv"));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        private static async Task<Dictionary<string, string[]>> ExtractRows(string svg)
        {
            string csv = await ExportCsv(svg);
            return csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(l => l.Split(';'))
                .ToDictionary(cols => cols[2].Trim('"'));
        }

        private static string Text(string[] row, int column) => row[column].Trim('"');

        [Test]
        public async Task ReadsAnSvg10Document()
        {
            // XML declaration, SVG 1.0 DOCTYPE, coordinates separated by spaces only (no commas), a commented-out element
            string svg = """
                <?xml version="1.0" standalone="no"?>
                <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.0//EN"
                  "http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd">
                <svg width="100" height="100" xmlns="http://www.w3.org/2000/svg">
                  <!-- <path id="Hidden" d="M 0 0 L 9 9 Z"/> -->
                  <g id="layer">
                    <rect id="R" x="10" y="20" width="30" height="40" style="fill:red"/>
                    <path id="P" d="M 10 10 L 30 10 L 30 30 Z"/>
                  </g>
                </svg>
                """;

            var rows = await ExtractRows(svg);

            Assert.Multiple(() =>
            {
                Assert.That(rows.Keys, Is.EquivalentTo(new[] { "R", "P" }), "the commented-out path is not read");
                Assert.That(Text(rows["R"], Improved), Is.EqualTo("m 0,0 h 30 v 40 h -30 z"));
                Assert.That(rows["R"][DeltaX], Is.EqualTo("10"));
                Assert.That(rows["R"][DeltaY], Is.EqualTo("20"));
                Assert.That(Text(rows["P"], Original), Is.EqualTo("M 10 10 L 30 10 L 30 30 Z"), "OriginalPath untouched");
                Assert.That(Text(rows["P"], Improved), Is.EqualTo("m 0,0 h 20 v 20 z"));
                Assert.That(rows["P"][DeltaX], Is.EqualTo("10"));
                Assert.That(rows["P"][DeltaY], Is.EqualTo("10"));
            });
        }

        [Test]
        public async Task ReadsAnSvg11InkscapeDocument()
        {
            // SVG 1.1 DOCTYPE, version attribute, xlink/inkscape/sodipodi namespaces, title, desc, defs with a
            // stylesheet, metadata, a layer group with a transform, and Inkscape-only attributes on the elements
            string svg = """
                <?xml version="1.0" encoding="UTF-8" standalone="no"?>
                <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd">
                <svg version="1.1" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                     xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"
                     xmlns:sodipodi="http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd"
                     width="200" height="200" viewBox="0 0 200 200">
                  <title>sample</title>
                  <desc>sample drawing</desc>
                  <defs id="defs1"><style type="text/css">.st0{fill:none}</style></defs>
                  <metadata id="m1"></metadata>
                  <g inkscape:groupmode="layer" id="layer1" transform="translate(10,20)">
                    <path id="cat_Item" class="st0" d="m 5,5 h 10 v 10 z" inkscape:label="x" sodipodi:nodetypes="ccc"/>
                    <polygon id="Poly" points="0,0 10,0 10,10 0,10" class="st1"/>
                    <circle id="Circ" cx="50" cy="50" r="10"/>
                    <line id="Ln" x1="0" y1="0" x2="20" y2="0" stroke-width="2"/>
                  </g>
                </svg>
                """;

            var rows = await ExtractRows(svg);

            Assert.Multiple(() =>
            {
                Assert.That(rows.Keys, Is.EquivalentTo(new[] { "Item", "Poly", "Circ", "Ln" }));

                Assert.That(Text(rows["Item"], Category), Is.EqualTo("cat"), "id split at the underscore");
                Assert.That(Text(rows["Item"], CssClass), Is.EqualTo("st0"));
                Assert.That(Text(rows["Item"], Improved), Is.EqualTo("m 0,0 h 10 v 10 z"));
                Assert.That(rows["Item"][DeltaX], Is.EqualTo("15"), "the layer's translate(10,20) + the path's own start (5,5)");
                Assert.That(rows["Item"][DeltaY], Is.EqualTo("25"));

                Assert.That(Text(rows["Poly"], Improved), Is.EqualTo("m 0,0 h 10 v 10 h -10 z"));
                Assert.That(Text(rows["Poly"], CssClass), Is.EqualTo("st1"));
                Assert.That(rows["Poly"][DeltaX], Is.EqualTo("10"));
                Assert.That(rows["Poly"][DeltaY], Is.EqualTo("20"));

                Assert.That(Text(rows["Circ"], Improved), Is.EqualTo("m 20,10 a 10,10 0 1 0 -20,0 a 10,10 0 1 0 20,0 z"));
                Assert.That(rows["Circ"][DeltaX], Is.EqualTo("50"), "layer x 10 + circle left edge 40");
                Assert.That(rows["Circ"][DeltaY], Is.EqualTo("60"), "layer y 20 + circle top edge 40");
                Assert.That(rows["Circ"][Width], Is.EqualTo("20"));
                Assert.That(rows["Circ"][Height], Is.EqualTo("20"));

                Assert.That(Text(rows["Ln"], Improved), Is.EqualTo("m 0,2 h 20 v -2 h -20 z"), "a 2-wide stroke is a 20x2 box");
                Assert.That(rows["Ln"][Width], Is.EqualTo("20"));
                Assert.That(rows["Ln"][Height], Is.EqualTo("2"));
            });
        }

        [Test]
        public async Task ReadsAnSvg2Document()
        {
            // no DOCTYPE and no version, href without xlink, CSS-property transform with units, CSS stroke-width,
            // and radii left 'auto' (only one given) on rect and ellipse
            string svg = """
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
                  <path id="Css" d="m 0,0 h 20 v 10 z" style="transform:translate(10px,20px) rotate(90deg)"/>
                  <ellipse id="AutoRy" cx="0" cy="0" rx="4"/>
                  <rect id="AutoRx" width="40" height="20" ry="5"/>
                  <use href="#Css" x="50"/>
                  <line id="Thick" x1="0" y1="0" x2="10" y2="0" style="stroke-width:2"/>
                </svg>
                """;

            var rows = await ExtractRows(svg);

            Assert.Multiple(() =>
            {
                Assert.That(rows.Keys, Is.EquivalentTo(new[] { "Css", "AutoRy", "AutoRx", "Thick" }), "<use> is not a shape of its own");

                Assert.That(rows["Css"][DeltaX], Is.EqualTo("10"));
                Assert.That(rows["Css"][DeltaY], Is.EqualTo("20"));
                Assert.That(rows["Css"][Rotation], Is.EqualTo("90"));
                Assert.That(Text(rows["Css"], Improved), Is.EqualTo("m 0,0 h 20 v 10 z"), "the drawing stays un-rotated");

                Assert.That(Text(rows["AutoRy"], Original), Is.EqualTo("m 4,0 a 4,4 0 1 0 -8,0 a 4,4 0 1 0 8,0 z"), "ry follows rx");
                Assert.That(Text(rows["AutoRx"], Original),
                    Is.EqualTo("m 5,0 h 30 a 5,5 0 0 1 5,5 v 10 a 5,5 0 0 1 -5,5 h -30 a 5,5 0 0 1 -5,-5 v -10 a 5,5 0 0 1 5,-5 z"),
                    "rx follows ry");
                Assert.That(Text(rows["Thick"], Improved), Is.EqualTo("m 0,2 h 10 v -2 h -10 z"));
            });
        }

        [Test]
        public async Task ReadsTheSameDrawingTheSameWhateverTheSvgVersion()
        {
            const string body = """
                  <rect id="R" x="10" y="20" width="30" height="40"/>
                  <path id="P" d="M 10 10 L 30 10 L 30 30 Z"/>
                  <circle id="C" cx="50" cy="50" r="10"/>
                  <polyline id="Pl" points="0,0 10,0 10,10"/>
                """;
            string svg10 = "<?xml version=\"1.0\" standalone=\"no\"?>\n" +
                "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.0//EN\" \"http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd\">\n" +
                "<svg width=\"100\" height=\"100\" xmlns=\"http://www.w3.org/2000/svg\">\n" + body + "\n</svg>";
            string svg11 = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n" +
                "<svg version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">\n" + body + "\n</svg>";
            string svg2 = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">\n" + body + "\n</svg>";

            string csv10 = await ExportCsv(svg10);
            string csv11 = await ExportCsv(svg11);
            string csv2 = await ExportCsv(svg2);

            Assert.Multiple(() =>
            {
                Assert.That(csv10.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(5), "header + 4 rows");
                Assert.That(csv11, Is.EqualTo(csv10), "SVG 1.1 reads as SVG 1.0");
                Assert.That(csv2, Is.EqualTo(csv10), "SVG 2 reads as SVG 1.0");
            });
        }

        // --- the simple shapes: what each becomes as path data, edge cases included ------------------------

        [TestCase("<rect id='S' x='1' y='2' width='3' height='4'/>", "m 1,2 h 3 v 4 h -3 z", TestName = "rect")]
        [TestCase("<rect id=\"S\" x=\"1\" y=\"2\" width=\"3\" height=\"4\"></rect>", "m 1,2 h 3 v 4 h -3 z", TestName = "rect, double quotes, open/close tags")]
        [TestCase("<rect id='S' width='5' height='6'/>", "m 0,0 h 5 v 6 h -5 z", TestName = "rect, x and y default to 0")]
        [TestCase("<rect id='S' width='40' height='20' rx='5'/>",
            "m 5,0 h 30 a 5,5 0 0 1 5,5 v 10 a 5,5 0 0 1 -5,5 h -30 a 5,5 0 0 1 -5,-5 v -10 a 5,5 0 0 1 5,-5 z", TestName = "rect, rx only")]
        [TestCase("<rect id='S' width='40' height='20' ry='5'/>",
            "m 5,0 h 30 a 5,5 0 0 1 5,5 v 10 a 5,5 0 0 1 -5,5 h -30 a 5,5 0 0 1 -5,-5 v -10 a 5,5 0 0 1 5,-5 z", TestName = "rect, ry only")]
        [TestCase("<rect id='S' width='40' height='20' rx='8' ry='4'/>",
            "m 8,0 h 24 a 8,4 0 0 1 8,4 v 12 a 8,4 0 0 1 -8,4 h -24 a 8,4 0 0 1 -8,-4 v -12 a 8,4 0 0 1 8,-4 z", TestName = "rect, rx and ry")]
        [TestCase("<rect id='S' width='10' height='10' rx='50'/>",
            "m 5,0 h 0 a 5,5 0 0 1 5,5 v 0 a 5,5 0 0 1 -5,5 h 0 a 5,5 0 0 1 -5,-5 v 0 a 5,5 0 0 1 5,-5 z", TestName = "rect, radius larger than half a side is clamped")]
        [TestCase("<rect id='S' height='4'/>", null, TestName = "rect without width is not drawn")]
        [TestCase("<rect id='S' width='3' height='0'/>", null, TestName = "rect with no height is not drawn")]
        [TestCase("<rect id='S' width='-3' height='4'/>", null, TestName = "rect with a negative width is not drawn")]

        [TestCase("<circle id='S' cx='10' cy='10' r='5'/>", "m 15,10 a 5,5 0 1 0 -10,0 a 5,5 0 1 0 10,0 z", TestName = "circle")]
        [TestCase("<circle id='S' cx='10' cy='10' r='5'></circle>", "m 15,10 a 5,5 0 1 0 -10,0 a 5,5 0 1 0 10,0 z", TestName = "circle, open/close tags")]
        [TestCase("<circle id='S' r='3'/>", "m 3,0 a 3,3 0 1 0 -6,0 a 3,3 0 1 0 6,0 z", TestName = "circle, centre defaults to 0,0")]
        [TestCase("<circle id='S' cx='10' cy='10' r='0'/>", null, TestName = "circle with a zero radius is not drawn")]
        [TestCase("<circle id='S' cx='10' cy='10'/>", null, TestName = "circle without a radius is not drawn")]

        [TestCase("<ellipse id='S' cx='0' cy='0' rx='8' ry='4'/>", "m 8,0 a 8,4 0 1 0 -16,0 a 8,4 0 1 0 16,0 z", TestName = "ellipse")]
        [TestCase("<ellipse id='S' rx='4'/>", "m 4,0 a 4,4 0 1 0 -8,0 a 4,4 0 1 0 8,0 z", TestName = "ellipse, ry follows rx")]
        [TestCase("<ellipse id='S' ry='4'/>", "m 4,0 a 4,4 0 1 0 -8,0 a 4,4 0 1 0 8,0 z", TestName = "ellipse, rx follows ry")]
        [TestCase("<ellipse id='S' rx='8' ry='0'/>", null, TestName = "ellipse with a zero radius is not drawn")]

        [TestCase("<polyline id='S' points='0,0 10,0 10,10'/>", "m 0,0 l 10,0 l 0,10", TestName = "polyline")]
        [TestCase("<polyline id='S' points='0 0 10 0 10 10'/>", "m 0,0 l 10,0 l 0,10", TestName = "polyline, coordinates separated by spaces only")]
        [TestCase("<polyline id='S' points='5-5 10-10'/>", "m 5,-5 l 5,-5", TestName = "polyline, packed numbers")]
        [TestCase("<polyline id='S' points='0,0  10,0\n 10,10'/>", "m 0,0 l 10,0 l 0,10", TestName = "polyline, mixed separators")]
        [TestCase("<polyline id='S' points=''/>", null, TestName = "polyline without points is not drawn")]

        [TestCase("<polygon id='S' points='0,0 10,0 10,10 0,10'/>", "m 0,0 l 10,0 l 0,10 l -10,0 z", TestName = "polygon")]
        [TestCase("<polygon id='S' points='3,4 13,4 8,14'></polygon>", "m 3,4 l 10,0 l -5,10 z", TestName = "polygon, open/close tags")]

        [TestCase("<line id='S' x1='0' y1='10' x2='100' y2='10' stroke-width='4'/>", "m 0,12 l 100,0 l 0,-4 l -100,0 z", TestName = "line, horizontal")]
        [TestCase("<line id='S' x1='5' y1='0' x2='5' y2='20' style='stroke-width:6'/>", "m 2,0 l 0,20 l 6,0 l 0,-20 z", TestName = "line, vertical, width from style")]
        [TestCase("<line id='S' x1='0' y1='0' x2='10' y2='10' stroke-width='2'/>",
            "m -0.707107,0.707107 l 10,10 l 1.414214,-1.414214 l -10,-10 z", TestName = "line, diagonal")]
        [TestCase("<line id='S' x1='0' y1='0' x2='10' y2='0' stroke-width='4' style='stroke-width:2'/>", "m 0,1 l 10,0 l 0,-2 l -10,0 z", TestName = "line, style wins over the attribute")]
        [TestCase("<line id='S' x1='0' y1='0' x2='10' y2='0'/>", "m 0,0.5 l 10,0 l 0,-1 l -10,0 z", TestName = "line, default stroke-width is 1")]
        [TestCase("<line id='S' x1='1' y1='1' x2='1' y2='1' stroke-width='4'/>", null, TestName = "line of zero length is not drawn")]
        [TestCase("<line id='S' x1='0' y1='0' x2='10' y2='0' stroke-width='0'/>", null, TestName = "line with a zero stroke-width is not drawn")]
        public async Task ShapeBecomesItsEquivalentPath(string element, string? expectedPath)
        {
            var rows = await ExtractRows($"<svg xmlns='http://www.w3.org/2000/svg'>{element}</svg>");

            if (expectedPath is null)
            {
                Assert.That(rows, Does.Not.ContainKey("S"));
            }
            else
            {
                Assert.That(rows, Contains.Key("S"));
                Assert.That(Text(rows["S"], Original), Is.EqualTo(expectedPath));
            }
        }

        [Test]
        public async Task EveryShapeIsClosedAndSitsInsideItsBoxAtTheOrigin()
        {
            string svg = """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <rect id="Rect" x="10" y="20" width="30" height="40"/>
                  <rect id="Round" x="5" y="5" width="40" height="20" rx="6"/>
                  <circle id="Circle" cx="50" cy="50" r="10"/>
                  <ellipse id="Ellipse" cx="30" cy="30" rx="20" ry="10"/>
                  <polyline id="Polyline" points="0,0 10,0 10,10"/>
                  <polygon id="Polygon" points="0,0 10,0 10,10 0,10"/>
                  <line id="Line" x1="0" y1="10" x2="100" y2="10" stroke-width="4"/>
                </svg>
                """;

            var rows = await ExtractRows(svg);

            Assert.Multiple(() =>
            {
                Assert.That(rows.Keys, Has.Count.EqualTo(7));
                foreach (var (name, row) in rows)
                {
                    Assert.That(Text(row, Improved), Does.EndWith(" z"), $"{name} is closed");
                    Assert.That(Text(row, Improved), Does.StartWith("m "), $"{name} starts with a relative moveto");
                    Assert.That(Text(row, Improved), Does.Not.Match("[A-Z]"), $"{name} is written with relative commands only");
                    Assert.That(row[BoxX], Is.EqualTo("0"), $"{name} box starts at the origin");
                    Assert.That(row[BoxY], Is.EqualTo("0"), $"{name} box starts at the origin");
                    Assert.That(row[Rotation], Is.EqualTo("0"), $"{name} is upright");
                }
                // the real extent of the round shapes, not just of the points the path passes through
                Assert.That(rows["Circle"][Width], Is.EqualTo("20"));
                Assert.That(rows["Circle"][Height], Is.EqualTo("20"));
                Assert.That(rows["Ellipse"][Width], Is.EqualTo("40"));
                Assert.That(rows["Ellipse"][Height], Is.EqualTo("20"));
                Assert.That(rows["Round"][Width], Is.EqualTo("40"));
                Assert.That(rows["Round"][Height], Is.EqualTo("20"));
                Assert.That(rows["Line"][Width], Is.EqualTo("100"));
                Assert.That(rows["Line"][Height], Is.EqualTo("4"));
                // where each one sits on the map: the top-left corner of its box
                Assert.That(rows["Ellipse"][DeltaX], Is.EqualTo("10"));
                Assert.That(rows["Ellipse"][DeltaY], Is.EqualTo("20"));
                Assert.That(rows["Line"][DeltaY], Is.EqualTo("8"));
            });
        }
    }
}
