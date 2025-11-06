# SVG PathTwicker
Inkscape is a great tool to draw SVG elements.  In an application where many elements from many files come together and may be manipulated by external actions, 
stems the requirement to optimise "d" attribute and also extract a reference point which can be used to plot on a map.
This SVG Path Tweaker read from SVG files created using Inkscape, extract each d attribute found in path element, and process it to a CSV file containting 
a few key data relative to the drawing :
* file name where the path was found
* X and Y position for easier grabing in map reference
* ideal label centered X and Y
* boxing rectangle width and height
* original path, unmodified
* optimized (improved at least) path

## Licence
This software is provided under GNU GPL v3 licence.
Copyright (c) 2025 Franck Menci

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published 
by the Free Software Foundation, version 3.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or 
FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.

