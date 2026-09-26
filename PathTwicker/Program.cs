/*
 * 

Copyright (c) 2025 Franck Menci

This file is part of SVGPathTwicker.

SVGPathTwicker is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, version 3.

SVGPathTwicker is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Foobar. If not, see <https://www.gnu.org/licenses/>.  

 * */

using SVGPathTwicker;
Console.WriteLine("SVG path optimizer, by Franck Menci \u00A9 2025");
Console.WriteLine("read file from Inkscape and aims to improve SVG path `d` attribute");

// source directory: command-line argument, else SVGPATHTWICKER_SOURCE_DIR env var, else the built-in default
string? sourceDirectory = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("SVGPATHTWICKER_SOURCE_DIR");
SvgFileExtractor fileExtractor = new(sourceDirectory);

Task t = Task.Run(async () => { await fileExtractor.Init(); });
await t;

Console.WriteLine("you may press `enter` to quit");
Console.ReadLine();
