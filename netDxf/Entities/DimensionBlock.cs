﻿#region netDxf library, Copyright (C) 2009-2017 Daniel Carvajal (haplokuon@gmail.com)

//                        netDxf library
// Copyright (C) 2009-2017 Daniel Carvajal (haplokuon@gmail.com)
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using netDxf.Blocks;
using netDxf.Objects;
using netDxf.Tables;
using netDxf.Units;

namespace netDxf.Entities
{
    /// <summary>
    /// Holds methods to build the dimension blocks.
    /// </summary>
    internal static class DimensionBlock
    {      
        #region private methods

        private static List<string> FormatDimensionText(double measure, DimensionType dimType, string userText, DimensionStyle style, Block owner)
        {
            List<string> texts = new List<string>();
            if (userText == " ")
                return texts;

            string dimText = string.Empty;

            UnitStyleFormat unitFormat = new UnitStyleFormat
            {
                LinearDecimalPlaces = style.LengthPrecision,
                AngularDecimalPlaces = style.AngularPrecision == -1 ? style.LengthPrecision : style.AngularPrecision,
                DecimalSeparator = style.DecimalSeparator.ToString(),
                FractionType = style.FractionalType,
                FractionalHeightScale = style.FractionalHeightScale,
                SupressLinearLeadingZeros = style.SuppressLinearLeadingZeros,
                SupressLinearTrailingZeros = style.SuppressLinearTrailingZeros,
                SupressAngularLeadingZeros = style.SuppressAngularLeadingZeros,
                SupressAngularTrailingZeros = style.SuppressAngularTrailingZeros,
                SupressZeroFeet = style.SuppressZeroFeet,
                SupressZeroInches = style.SuppressZeroInches
            };

            if (dimType== DimensionType.Angular || dimType == DimensionType.Angular3Point)
            {
                switch (style.DimAngularUnits)
                {
                    case AngleUnitType.DecimalDegrees:
                        dimText = AngleUnitFormat.ToDecimal(measure, unitFormat);
                        break;
                    case AngleUnitType.DegreesMinutesSeconds:
                        dimText = AngleUnitFormat.ToDegreesMinutesSeconds(measure, unitFormat);
                        break;
                    case AngleUnitType.Gradians:
                        dimText = AngleUnitFormat.ToGradians(measure, unitFormat);
                        break;
                    case AngleUnitType.Radians:
                        dimText = AngleUnitFormat.ToRadians(measure, unitFormat);
                        break;
                    case AngleUnitType.SurveyorUnits:
                        dimText = AngleUnitFormat.ToDecimal(measure, unitFormat);
                        break;
                }
            }
            else
            {
                double scale = Math.Abs(style.DimScaleLinear);
                if (owner != null)
                {
                    Layout layout = owner.Record.Layout;
                    if (layout != null)
                    {
                        // if DIMLFAC is negative the scale value is only applied to dimensions in PaperSpace
                        if (style.DimScaleLinear < 0 && !layout.IsPaperSpace)
                            scale = 1.0;
                    }
                }
                
                if (style.DimRoundoff > 0.0)
                    measure = MathHelper.RoundToNearest(measure*scale, style.DimRoundoff);
                else
                    measure *= scale;

                switch (style.DimLengthUnits)
                {
                    case LinearUnitType.Architectural:
                        dimText = LinearUnitFormat.ToArchitectural(measure, unitFormat);
                        break;
                    case LinearUnitType.Decimal:
                        dimText = LinearUnitFormat.ToDecimal(measure, unitFormat);
                        break;
                    case LinearUnitType.Engineering:
                        dimText = LinearUnitFormat.ToEngineering(measure, unitFormat);
                        break;
                    case LinearUnitType.Fractional:
                        dimText = LinearUnitFormat.ToFractional(measure, unitFormat);
                        break;
                    case LinearUnitType.Scientific:
                        dimText = LinearUnitFormat.ToScientific(measure, unitFormat);
                        break;
                    case LinearUnitType.WindowsDesktop:
                        unitFormat.LinearDecimalPlaces = (short) Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalDigits;
                        unitFormat.DecimalSeparator = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                        dimText = LinearUnitFormat.ToDecimal(measure*style.DimScaleLinear, unitFormat);
                        break;
                }
            }

            string prefix = string.Empty;
            if (dimType == DimensionType.Diameter)
                prefix = string.IsNullOrEmpty(style.DimPrefix) ? "Ø" : style.DimPrefix;
            if (dimType == DimensionType.Radius)
                prefix = string.IsNullOrEmpty(style.DimPrefix) ? "R" : style.DimPrefix;
            dimText = string.Format("{0}{1}{2}", prefix, dimText, style.DimSuffix);

            if (!string.IsNullOrEmpty(userText))
            {
                int splitPos = 0;

                // check for the first appearance of \X
                // it will break the dimension text into two one over the dimension line and the other under
                for (int i = 0; i < userText.Length; i++)
                {
                    if (userText[i].Equals('\\'))
                    {
                        int j = i + 1;
                        if (j >= userText.Length)
                            break;

                        if (userText[j].Equals('X'))
                        {
                            splitPos = i;
                            break;
                        }
                    }
                }

                if (splitPos > 0)
                {
                    texts.Add(userText.Substring(0, splitPos).Replace("<>", dimText));
                    texts.Add(userText.Substring(splitPos + 2, userText.Length - (splitPos + 2)).Replace("<>", dimText));
                }
                else
                {
                    texts.Add(userText.Replace("<>", dimText));
                }
            }
            else
            {
                texts.Add(dimText);
            }

            return texts;
        }

        private static Line DimensionLine(Vector2 start, Vector2 end, double rotation, DimensionStyle style, Boolean arrowsoutside)
        {
            double ext1 = (arrowsoutside ? -2 : 1) * style.ArrowSize*style.DimScaleOverall;
            double ext2 = (arrowsoutside ? -2 : 1) * -style.ArrowSize*style.DimScaleOverall;

            Block block;

            //start arrow
            block = style.DimArrow1;
            if (block != null)
            {
                if (block.Name.Equals("_OBLIQUE", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_ARCHTICK", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_INTEGRAL", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_NONE", StringComparison.OrdinalIgnoreCase))
                    ext1 = (arrowsoutside ? -2 : 1) * -style.DimLineExtend*style.DimScaleOverall;
            }

            //end arrow
            block = style.DimArrow2;
            if (block != null)
            {
                if (block.Name.Equals("_OBLIQUE", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_ARCHTICK", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_INTEGRAL", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_NONE", StringComparison.OrdinalIgnoreCase))
                    ext2 = (arrowsoutside ? -2 : 1) * style.DimLineExtend*style.DimScaleOverall;
            }

            start = Vector2.Polar(start, ext1, rotation);
            end = Vector2.Polar(end, ext2, rotation);

            return new Line(start, end)
            {
                Color = style.DimLineColor,
                Linetype = style.DimLineLinetype,
                Lineweight = style.DimLineLineweight
            };
        }

        private static Arc DimensionArc(Vector2 center, Vector2 start, Vector2 end, double startAngle, double endAngle, double radius, DimensionStyle style, out double e1, out double e2)
        {
            double ext1 = style.ArrowSize*style.DimScaleOverall;
            double ext2 = -style.ArrowSize*style.DimScaleOverall;
            e1 = ext1;
            e2 = ext2;

            Block block;

            block = style.DimArrow1;
            if (block != null)
            {
                if (block.Name.Equals("_OBLIQUE", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_ARCHTICK", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_INTEGRAL", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_NONE", StringComparison.OrdinalIgnoreCase))
                {
                    ext1 = 0.0;
                    e1 = 0.0;
                }
            }

            block = style.DimArrow2;
            if (block != null)
            {
                if (block.Name.Equals("_OBLIQUE", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_ARCHTICK", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_INTEGRAL", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_NONE", StringComparison.OrdinalIgnoreCase))
                {
                    ext2 = 0.0;
                    e2 = 0.0;
                }
            }

            start = Vector2.Polar(start, ext1, startAngle + MathHelper.HalfPI);
            end = Vector2.Polar(end, ext2, endAngle + MathHelper.HalfPI);
            return new Arc(center, radius, Vector2.Angle(center, start)*MathHelper.RadToDeg, Vector2.Angle(center, end)*MathHelper.RadToDeg)
            {
                Color = style.DimLineColor,
                Linetype = style.DimLineLinetype,
                Lineweight = style.DimLineLineweight
            };
        }

        private static Line DimensionRadialLine(Vector2 start, Vector2 end, double rotation, short reversed, DimensionStyle style)
        {
            double ext = -style.ArrowSize*style.DimScaleOverall;
            Block block;

            // the radial dimension only has an arrowhead at its end
            block = style.DimArrow2;
            if (block != null)
            {
                if (block.Name.Equals("_OBLIQUE", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_ARCHTICK", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_INTEGRAL", StringComparison.OrdinalIgnoreCase) ||
                    block.Name.Equals("_NONE", StringComparison.OrdinalIgnoreCase))
                    ext = style.DimLineExtend*style.DimScaleOverall;
            }

            //start = Vector2.Polar(start, reversed * ext1, rotation);
            end = Vector2.Polar(end, reversed*ext, rotation);

            return new Line(start, end)
            {
                Color = style.DimLineColor,
                Linetype = style.DimLineLinetype,
                Lineweight = style.DimLineLineweight
            };
        }

        private static Line ExtensionLine(Vector2 start, Vector2 end, DimensionStyle style, Linetype linetype)
        {
            return new Line(start, end)
            {
                Color = style.ExtLineColor,
                Linetype = linetype,
                Lineweight = style.ExtLineLineweight
            };
        }

        private static EntityObject StartArrowHead(Vector2 position, double rotation, DimensionStyle style)
        {
            Block block = style.DimArrow1;

            if (block == null)
            {
                Vector2 arrowRef = Vector2.Polar(position, -style.ArrowSize*style.DimScaleOverall, rotation);
                Solid arrow = new Solid(position,
                    Vector2.Polar(arrowRef, -(style.ArrowSize/6)*style.DimScaleOverall, rotation + MathHelper.HalfPI),
                    Vector2.Polar(arrowRef, (style.ArrowSize/6)*style.DimScaleOverall, rotation + MathHelper.HalfPI))
                {
                    Color = style.DimLineColor
                };
                return arrow;
            }
            else
            {
                Insert arrow = new Insert(block, position)
                {
                    Color = style.DimLineColor,
                    Scale = new Vector3(style.ArrowSize*style.DimScaleOverall),
                    Rotation = rotation*MathHelper.RadToDeg,
                    Lineweight = style.DimLineLineweight
                };
                return arrow;
            }
        }

        private static EntityObject EndArrowHead(Vector2 position, double rotation, DimensionStyle style)
        {
            Block block = style.DimArrow2;

            if (block == null)
            {
                Vector2 arrowRef = Vector2.Polar(position, -style.ArrowSize*style.DimScaleOverall, rotation);
                Solid arrow = new Solid(position,
                    Vector2.Polar(arrowRef, -(style.ArrowSize/6)*style.DimScaleOverall, rotation + MathHelper.HalfPI),
                    Vector2.Polar(arrowRef, (style.ArrowSize/6)*style.DimScaleOverall, rotation + MathHelper.HalfPI))
                {
                    Color = style.DimLineColor
                };
                return arrow;
            }
            else
            {
                Insert arrow = new Insert(block, position)
                {
                    Color = style.DimLineColor,
                    Scale = new Vector3(style.ArrowSize*style.DimScaleOverall),
                    Rotation = rotation*MathHelper.RadToDeg,
                    Lineweight = style.DimLineLineweight
                };
                return arrow;
            }
        }

        private static MText DimensionText(Vector2 position, MTextAttachmentPoint attachmentPoint, double rotation, string text, DimensionStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            MText mText = new MText(text, position, style.TextHeight*style.DimScaleOverall, 0.0, style.TextStyle)
            {
                Color = style.TextColor,
                AttachmentPoint = attachmentPoint,
                Rotation = rotation*MathHelper.RadToDeg
            };

            return mText;
        }

        private static List<EntityObject> CenterCross(Vector2 center, double radius, DimensionStyle style)
        {
            List<EntityObject> lines = new List<EntityObject>();
            if (MathHelper.IsZero(style.CenterMarkSize))
                return lines;

            Vector2 c1;
            Vector2 c2;
            double dist = Math.Abs(style.CenterMarkSize*style.DimScaleOverall);

            // center mark
            c1 = new Vector2(0.0, -dist) + center;
            c2 = new Vector2(0.0, dist) + center;
            lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});
            c1 = new Vector2(-dist, 0.0) + center;
            c2 = new Vector2(dist, 0.0) + center;
            lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});

            // center lines
            if (style.CenterMarkSize < 0)
            {
                c1 = new Vector2(2*dist, 0.0) + center;
                c2 = new Vector2(radius + dist, 0.0) + center;
                lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});

                c1 = new Vector2(-2*dist, 0.0) + center;
                c2 = new Vector2(-radius - dist, 0.0) + center;
                lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});

                c1 = new Vector2(0.0, 2*dist) + center;
                c2 = new Vector2(0.0, radius + dist) + center;
                lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});

                c1 = new Vector2(0.0, -2*dist) + center;
                c2 = new Vector2(0.0, -radius - dist) + center;
                lines.Add(new Line(c1, c2) {Color = style.ExtLineColor, Lineweight = style.ExtLineLineweight});
            }
            return lines;
        }

        private static DimensionStyle BuildDimensionStyleOverride(Dimension dim)
        {
            // to avoid the cloning, return the actual dimension style if there are no overrides
            if (dim.StyleOverrides.Count == 0)
                return dim.Style;

            // make a shallow copy of the actual dimension style, there is no need of the full copy that the Clone method does
            DimensionStyle copy = new DimensionStyle(dim.Style.Name)
            {
                // dimension lines
                DimLineColor = dim.Style.DimLineColor,
                DimLineLinetype = dim.Style.DimLineLinetype,
                DimLineLineweight = dim.Style.DimLineLineweight,
                DimLineOff = dim.Style.DimLineOff,
                DimBaselineSpacing = dim.Style.DimBaselineSpacing,
                DimLineExtend = dim.Style.DimLineExtend,

                // extension lines
                ExtLineColor = dim.Style.ExtLineColor,
                ExtLine1Linetype = dim.Style.ExtLine1Linetype,
                ExtLine2Linetype = dim.Style.ExtLine2Linetype,
                ExtLineLineweight = dim.Style.ExtLineLineweight,
                ExtLine1Off = dim.Style.ExtLine1Off,
                ExtLine2Off = dim.Style.ExtLine2Off,
                ExtLineOffset = dim.Style.ExtLineOffset,
                ExtLineExtend = dim.Style.ExtLineExtend,

                // symbols and arrows
                ArrowSize = dim.Style.ArrowSize,
                CenterMarkSize = dim.Style.CenterMarkSize,

                // fit
                DIMTIH = dim.Style.DIMTIH,
                DIMTOH = dim.Style.DIMTOH,

                // text appearance
                TextStyle = dim.Style.TextStyle,
                TextColor = dim.Style.TextColor,
                TextHeight = dim.Style.TextHeight,
                DIMJUST = dim.Style.DIMJUST,
                DIMTAD = dim.Style.DIMTAD,
                TextOffset = dim.Style.TextOffset,

                // primary units
                AngularPrecision = dim.Style.AngularPrecision,
                LengthPrecision = dim.Style.LengthPrecision,
                DecimalSeparator = dim.Style.DecimalSeparator,
                DimAngularUnits = dim.Style.DimAngularUnits,
                LeaderArrow = dim.Style.LeaderArrow,
                DimArrow1 = dim.Style.DimArrow1,
                DimArrow2 = dim.Style.DimArrow2
            };

            foreach (DimensionStyleOverride styleOverride in dim.StyleOverrides.Values)
            {
                switch (styleOverride.Type)
                {
                    case DimensionStyleOverrideType.DimLineColor:
                        copy.DimLineColor = (AciColor) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimLineLinetype:
                        copy.DimLineLinetype = (Linetype) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimLineLineweight:
                        copy.DimLineLineweight = (Lineweight) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimLineOff:
                        copy.DimLineOff = (bool)styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimLineExtend:
                        copy.DimLineExtend = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLineColor:
                        copy.ExtLineColor = (AciColor) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLine1Linetype:
                        copy.ExtLine1Linetype = (Linetype) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLine2Linetype:
                        copy.ExtLine2Linetype = (Linetype) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLineLineweight:
                        copy.ExtLineLineweight = (Lineweight) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLine1Off:
                        copy.ExtLine1Off = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLine2Off:
                        copy.ExtLine2Off = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLineOffset:
                        copy.ExtLineOffset = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ExtLineExtend:
                        copy.ExtLineExtend = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.ArrowSize:
                        copy.ArrowSize = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.CenterMarkSize:
                        copy.CenterMarkSize = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.LeaderArrow:
                        copy.LeaderArrow = (Block) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimArrow1:
                        copy.DimArrow1 = (Block) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimArrow2:
                        copy.DimArrow2 = (Block) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.TextStyle:
                        copy.TextStyle = (TextStyle) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.TextColor:
                        copy.TextColor = (AciColor) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.TextHeight:
                        copy.TextHeight = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.TextOffset:
                        copy.TextOffset = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimScaleOverall:
                        copy.DimScaleOverall = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.AngularPrecision:
                        copy.AngularPrecision = (short) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.LengthPrecision:
                        copy.LengthPrecision = (short) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimPrefix:
                        copy.DimPrefix = (string) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimSuffix:
                        copy.DimSuffix = (string) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DecimalSeparator:
                        copy.DecimalSeparator = (char) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimScaleLinear:
                        copy.DimScaleLinear = (double) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimLengthUnits:
                        copy.DimLengthUnits = (LinearUnitType) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimAngularUnits:
                        copy.DimAngularUnits = (AngleUnitType) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.FractionalType:
                        copy.FractionalType = (FractionFormatType) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressLinearLeadingZeros:
                        copy.SuppressLinearLeadingZeros = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressLinearTrailingZeros:
                        copy.SuppressLinearTrailingZeros = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressAngularLeadingZeros:
                        copy.SuppressAngularLeadingZeros = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressAngularTrailingZeros:
                        copy.SuppressAngularTrailingZeros = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressZeroFeet:
                        copy.SuppressZeroFeet = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.SuppressZeroInches:
                        copy.SuppressZeroInches = (bool) styleOverride.Value;
                        break;
                    case DimensionStyleOverrideType.DimRoundoff:
                        copy.DimRoundoff = (double) styleOverride.Value;
                        break;
                }
            }

            return copy;
        }

        #endregion

        #region public methods

        public static Block Build(LinearDimension dim, string name)
        {
            double measure = dim.Measurement;
            measure = Math.Round(measure, dim.Style.LengthPrecision);
            bool reversed = false;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 ref1 = dim.FirstReferencePoint;
            Vector2 ref2 = dim.SecondReferencePoint;
            Vector2 midRef = Vector2.MidPoint(ref1, ref2);

            double dimRotation = dim.Rotation*MathHelper.DegToRad;
            Vector2 dimRef1tmp = new Vector2(-measure*0.5, dim.Offset);
            Vector2 dimRef2tmp = new Vector2(measure*0.5, dim.Offset);
            Vector2 dimRef1 = MathHelper.Transform(dimRef1tmp, dimRotation, CoordinateSystem.Object, CoordinateSystem.World) + midRef;
            Vector2 dimRef2 = MathHelper.Transform(dimRef2tmp, dimRotation, CoordinateSystem.Object, CoordinateSystem.World) + midRef;
            Vector2 midDim = Vector2.MidPoint(dimRef1, dimRef2);
            double relativeAngle = Vector2.AngleBetween(ref2 - ref1, dimRef2 - dimRef1);
            if (relativeAngle > MathHelper.HalfPI && relativeAngle <= MathHelper.ThreeHalfPI)
            {
                Vector2 tmp = ref1;
                ref1 = ref2;
                ref2 = tmp;
                reversed = true;
            }

            // reference points
            Layer defPointLayer = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1) {Layer = defPointLayer});
            entities.Add(new Point(ref2) {Layer = defPointLayer});
            entities.Add(reversed
                ? new Point(dimRef1) {Layer = defPointLayer}
                : new Point(dimRef2) {Layer = defPointLayer});

            if (!style.DimLineOff)
            {
                //We'll need to set if the arrowheads are outside.
                Boolean outside = Vector2.Distance(dimRef1, dimRef2) < style.ArrowSize * 3 * style.DimScaleOverall;

                // dimension line - ALWAYS DRAWN INITIALLY (we may want to update later)
                entities.Add(DimensionLine(dimRef1, dimRef2, dimRotation, style, outside));
         

                // dimension arrowheads
                double ExtraRotate = outside ? MathHelper.PI  : 0;
                if (reversed)
                {
                    entities.Add(StartArrowHead(dimRef2, dimRotation + ExtraRotate, style));
                    entities.Add(EndArrowHead(dimRef1, dimRotation + MathHelper.PI + ExtraRotate, style));
                }
                else
                {
                    entities.Add(StartArrowHead(dimRef1, dimRotation + MathHelper.PI + ExtraRotate, style));
                    entities.Add(EndArrowHead(dimRef2, dimRotation + ExtraRotate, style));
                }
            }

            // extension lines
            Vector2 dirDimRef = Vector2.Normalize(dimRef2 - dimRef1);
            Vector2 perpDimRef = Vector2.Perpendicular(dirDimRef);

            Vector2 v;
            v = dimRef1 - ref1;
            Vector2 dirRef1 = MathHelper.IsZero(Vector2.DotProduct(v, v)) ? perpDimRef : Vector2.Normalize(dimRef1 - ref1);

            v = dimRef2 - ref2;
            Vector2 dirRef2 = MathHelper.IsZero(Vector2.DotProduct(v, v)) ? -perpDimRef : Vector2.Normalize(dimRef2 - ref2);

            double dimexo = style.ExtLineOffset*style.DimScaleOverall;
            double dimexe = style.ExtLineExtend*style.DimScaleOverall;
            if (!style.ExtLine1Off)
                entities.Add(ExtensionLine(ref1 + dimexo*dirRef1, dimRef1 + dimexe*dirRef1, style, style.ExtLine1Linetype));
            if (!style.ExtLine2Off)
                entities.Add(ExtensionLine(ref2 + dimexo*dirRef2, dimRef2 + dimexe*dirRef2, style, style.ExtLine2Linetype));


            // dimension text
            double textRot = dimRotation;
            if (textRot > MathHelper.HalfPI && textRot <= MathHelper.ThreeHalfPI)
                textRot += MathHelper.PI;

            //We'll now get the string that we'll want to use, as we'll need to see how the text fits.
            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner); 

            //We will draw text differently based on if it is outside or not.
            Boolean forcetextout = ((perpDimRef.X == 1) || (perpDimRef.Y == -1)) &&
                                   Vector2.Distance(dimRef1, dimRef2) < style.ArrowSize * 5 * style.DimScaleOverall;
            if (forcetextout)
            {
                MText mText = DimensionText(Vector2.Polar(midDim, -style.TextOffset * style.DimScaleOverall, textRot + MathHelper.HalfPI), MTextAttachmentPoint.TopCenter, textRot, texts[0], style);
                if (mText != null) entities.Add(mText);
            }
            else
            {
                MText mText = DimensionText(Vector2.Polar(midDim, style.TextOffset * style.DimScaleOverall, textRot + MathHelper.HalfPI), MTextAttachmentPoint.BottomCenter, textRot, texts[0], style);
                if (mText != null) entities.Add(mText);
            }

            // there might be an additional text if the code \X has been used in the dimension UserText 
            // this additional text appears under the dimension line
            if (texts.Count > 1)
            {
                string text = texts[1];
                MText mText2 = DimensionText(Vector2.Polar(midDim, -style.TextOffset * style.DimScaleOverall, textRot + MathHelper.HalfPI), MTextAttachmentPoint.TopCenter, textRot, text, style);
                if (mText2 != null)
                    entities.Add(mText2);
            }

            Vector3 defPoint = reversed ? new Vector3(dimRef1.X, dimRef1.Y, dim.Elevation) : new Vector3(dimRef2.X, dimRef2.Y, dim.Elevation);
            dim.DefinitionPoint = MathHelper.Transform(defPoint, dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(midDim.X, midDim.Y, dim.Elevation); // this value is in OCS

            // drawing block
            return new Block(name, false, entities, null) {Flags = BlockTypeFlags.AnonymousBlock};
        }

        public static Block Build(AlignedDimension dim, string name)
        {
            bool reversed = false;
            double measure = dim.Measurement;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 ref1 = dim.FirstReferencePoint;
            Vector2 ref2 = dim.SecondReferencePoint;

            double refAngle = Vector2.Angle(ref1, ref2);

            if (refAngle > MathHelper.HalfPI && refAngle <= MathHelper.ThreeHalfPI)
            {
                Vector2 tmp = ref1;
                ref1 = ref2;
                ref2 = tmp;
                refAngle += MathHelper.PI;
                reversed = true;
            }

            Vector2 dimRef1 = Vector2.Polar(ref1, dim.Offset, refAngle + MathHelper.HalfPI);
            Vector2 dimRef2 = Vector2.Polar(ref2, dim.Offset, refAngle + MathHelper.HalfPI);
            Vector2 midDim = Vector2.MidPoint(dimRef1, dimRef2);

            // reference points
            Layer defPointLayer = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1) {Layer = defPointLayer});
            entities.Add(new Point(ref2) {Layer = defPointLayer});
            entities.Add(reversed
                ? new Point(dimRef1) {Layer = defPointLayer}
                : new Point(dimRef2) {Layer = defPointLayer});

            if (!style.DimLineOff)
            {
                // dimension lines
                entities.Add(DimensionLine(dimRef1, dimRef2, refAngle, style, false));
                // dimension arrowheads
                if (reversed)
                {
                    entities.Add(StartArrowHead(dimRef2, refAngle, style));
                    entities.Add(EndArrowHead(dimRef1, refAngle + MathHelper.PI, style));
                }
                else
                {
                    entities.Add(StartArrowHead(dimRef1, refAngle + MathHelper.PI, style));
                    entities.Add(EndArrowHead(dimRef2, refAngle, style));
                }
            }

            // extension lines
            double dimexo = style.ExtLineOffset*style.DimScaleOverall;
            double dimexe = style.ExtLineExtend*style.DimScaleOverall;

            if (dim.Offset < 0)
            {
                dimexo *= -1;
                dimexe *= -1;
            }

            if (!style.ExtLine1Off)
                entities.Add(ExtensionLine(Vector2.Polar(ref1, dimexo, refAngle + MathHelper.HalfPI), Vector2.Polar(dimRef1, dimexe, refAngle + MathHelper.HalfPI), style, style.ExtLine1Linetype));
            if (!style.ExtLine2Off)
                entities.Add(ExtensionLine(Vector2.Polar(ref2, dimexo, refAngle + MathHelper.HalfPI), Vector2.Polar(dimRef2, dimexe, refAngle + MathHelper.HalfPI), style, style.ExtLine2Linetype));

            // dimension text
            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);

            MText mText = DimensionText(Vector2.Polar(midDim, style.TextOffset*style.DimScaleOverall, refAngle + MathHelper.HalfPI), MTextAttachmentPoint.BottomCenter, refAngle, texts[0], style);
            if (mText != null)
                entities.Add(mText);

            // there might be an additional text if the code \X has been used in the dimension UserText 
            // this additional text appears under the dimension line
            if (texts.Count > 1)
            {
                string text = texts[1];
                MText mText2 = DimensionText(Vector2.Polar(midDim, -style.TextOffset * style.DimScaleOverall, refAngle + MathHelper.HalfPI), MTextAttachmentPoint.TopCenter, refAngle, text, style);
                if (mText2 != null)
                    entities.Add(mText2);
            }

            Vector3 defPoint = reversed ? new Vector3(dimRef1.X, dimRef1.Y, dim.Elevation) : new Vector3(dimRef2.X, dimRef2.Y, dim.Elevation);
            dim.DefinitionPoint = MathHelper.Transform(defPoint, dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(midDim.X, midDim.Y, dim.Elevation); // this value is in OCS

            // drawing block
            return new Block(name, false, entities, null) {Flags = BlockTypeFlags.AnonymousBlock};
        }

        public static Block Build(Angular2LineDimension dim, string name)
        {
            double offset = Math.Abs(dim.Offset);
            double side = Math.Sign(dim.Offset);
            double measure = dim.Measurement;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 ref1Start = dim.StartFirstLine;
            Vector2 ref1End = dim.EndFirstLine;
            Vector2 ref2Start = dim.StartSecondLine;
            Vector2 ref2End = dim.EndSecondLine;

            if (side < 0)
            {
                Vector2 tmp1 = ref1Start;
                Vector2 tmp2 = ref2Start;
                ref1Start = ref1End;
                ref1End = tmp1;
                ref2Start = ref2End;
                ref2End = tmp2;
            }

            Vector2 dirRef1 = ref1End - ref1Start;
            Vector2 dirRef2 = ref2End - ref2Start;
            Vector2 center = MathHelper.FindIntersection(ref1Start, dirRef1, ref2Start, dirRef2);
            if (Vector2.IsNaN(center))
                throw new ArgumentException("The two lines that define the dimension are parallel.");

            double startAngle = Vector2.Angle(center, ref1End);
            double endAngle = Vector2.Angle(center, ref2End);
            double cross = Vector2.CrossProduct(dirRef1, dirRef2);
            if (cross < 0)
            {
                Vector2 tmp1 = ref1Start;
                Vector2 tmp2 = ref1End;
                ref1Start = ref2Start;
                ref1End = ref2End;
                ref2Start = tmp1;
                ref2End = tmp2;
                double tmp = startAngle;
                startAngle = endAngle;
                endAngle = tmp;
            }

            double midRot = startAngle + measure*MathHelper.DegToRad*0.5;
            //if (midRot > MathHelper.TwoPI) midRot -= MathHelper.TwoPI;
            Vector2 dimRef1 = Vector2.Polar(center, offset, startAngle);
            Vector2 dimRef2 = Vector2.Polar(center, offset, endAngle);
            Vector2 midDim = Vector2.Polar(center, offset, midRot);

            // reference points
            Layer defPoints = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1Start) {Layer = defPoints});
            entities.Add(new Point(ref1End) {Layer = defPoints});
            entities.Add(new Point(ref2Start) {Layer = defPoints});
            entities.Add(new Point(ref2End) {Layer = defPoints});

            if (!style.DimLineOff)
            {
                // dimension lines
                double ext1;
                double ext2;
                entities.Add(DimensionArc(center, dimRef1, dimRef2, startAngle, endAngle, offset, style, out ext1, out ext2));

                // dimension arrows
                double angle1 = Math.Asin(ext1*0.5/offset);
                double angle2 = Math.Asin(ext2*0.5/offset);
                entities.Add(StartArrowHead(dimRef1, angle1 + startAngle - MathHelper.HalfPI, style));
                entities.Add(EndArrowHead(dimRef2, angle2 + endAngle + MathHelper.HalfPI, style));
            }

            // dimension lines         
            double dimexo = style.ExtLineOffset*style.DimScaleOverall;
            double dimexe = style.ExtLineExtend*style.DimScaleOverall;

            // the dimension line is only drawn if the end of the extension line is outside the line segment
            int t;
            t = MathHelper.PointInSegment(dimRef1, ref1Start, ref1End);
            if (!style.ExtLine1Off && t != 0)
            {
                Vector2 s = Vector2.Polar(t < 0 ? ref1Start : ref1End, t*dimexo, startAngle);
                entities.Add(ExtensionLine(s, Vector2.Polar(dimRef1, t*dimexe, startAngle), style, style.ExtLine1Linetype));
            }

            t = MathHelper.PointInSegment(dimRef2, ref2Start, ref2End);
            if (!style.ExtLine2Off && t != 0)
            {
                Vector2 s = Vector2.Polar(t < 0 ? ref2Start : ref2End, t*dimexo, endAngle);
                entities.Add(ExtensionLine(s, Vector2.Polar(dimRef2, t*dimexe, endAngle), style, style.ExtLine1Linetype));
            }

            double textRot = midRot - MathHelper.HalfPI;
            double gap = style.TextOffset*style.DimScaleOverall;
            if (textRot > MathHelper.HalfPI && textRot <= MathHelper.ThreeHalfPI)
            {
                textRot += MathHelper.PI;
                gap *= -1;
            }

            #region MText build as the Linear dimensions, two MTexts if the code "\X" is present

            //List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner.Record.Layout);
            //MText mText = DimensionText(Vector2.Polar(midDim, gap, midRot), MTextAttachmentPoint.BottomCenter, textRot, texts[0], style);
            //if (mText != null)
            //    entities.Add(mText);

            //// there might be an additional text if the code \X has been used in the dimension UserText 
            //// this additional text appears under the dimension line
            //if (texts.Count > 1)
            //{
            //    string text = texts[1];
            //    MText mText2 = DimensionText(Vector2.Polar(midDim, -gap, midRot), MTextAttachmentPoint.TopCenter, textRot, text, style);
            //    if (mText2 != null)
            //        entities.Add(mText2);
            //}

            #endregion

            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);
            string dimText;
            Vector2 position;
            MTextAttachmentPoint attachmentPoint;
            if (texts.Count > 1)
            {
                position = midDim;
                dimText = texts[0] + "\\P" + texts[1];
                attachmentPoint = MTextAttachmentPoint.MiddleCenter;
            }
            else
            {
                position = Vector2.Polar(midDim, gap, midRot);
                dimText = texts[0];
                attachmentPoint = MTextAttachmentPoint.TopCenter;
            }
            MText mText = DimensionText(position, attachmentPoint, textRot, dimText, style);
            if (mText != null)
                entities.Add(mText);

            dim.DefinitionPoint =
                MathHelper.Transform(
                    new Vector3(dim.EndSecondLine.X, dim.EndSecondLine.Y, dim.Elevation),
                    dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(midDim.X, midDim.Y, dim.Elevation); // this value is in OCS
            dim.ArcDefinitionPoint = dim.MidTextPoint; // this value is in OCS

            // drawing block
            return new Block(name, false, entities, null) {Flags = BlockTypeFlags.AnonymousBlock};
        }

        public static Block Build(Angular3PointDimension dim, string name)
        {
            double offset = Math.Abs(dim.Offset);
            double side = Math.Sign(dim.Offset);
            double measure = dim.Measurement;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 refCenter = dim.CenterPoint;
            Vector2 ref1 = dim.StartPoint;
            Vector2 ref2 = dim.EndPoint;

            if (side < 0)
            {
                Vector2 tmp = ref1;
                ref1 = ref2;
                ref2 = tmp;
            }

            double startAngle = Vector2.Angle(refCenter, ref1);
            double endAngle = Vector2.Angle(refCenter, ref2);
            double midRot = startAngle + measure*MathHelper.DegToRad*0.5;
            //if (midRot > MathHelper.TwoPI) midRot -= MathHelper.TwoPI;
            Vector2 dimRef1 = Vector2.Polar(refCenter, offset, startAngle);
            Vector2 dimRef2 = Vector2.Polar(refCenter, offset, endAngle);
            Vector2 midDim = Vector2.Polar(refCenter, offset, midRot);

            // reference points
            Layer defPoints = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1) {Layer = defPoints});
            entities.Add(new Point(ref2) {Layer = defPoints});
            entities.Add(new Point(refCenter) {Layer = defPoints});

            if (!style.DimLineOff)
            {
                // dimension lines
                double ext1;
                double ext2;
                entities.Add(DimensionArc(refCenter, dimRef1, dimRef2, startAngle, endAngle, offset, style, out ext1, out ext2));

                // dimension arrows
                double angle1 = Math.Asin(ext1*0.5/offset);
                double angle2 = Math.Asin(ext2*0.5/offset);
                entities.Add(StartArrowHead(dimRef1, angle1 + startAngle - MathHelper.HalfPI, style));
                entities.Add(EndArrowHead(dimRef2, angle2 + endAngle + MathHelper.HalfPI, style));
            }
                
            // extension lines
            double dimexo = style.ExtLineOffset*style.DimScaleOverall;
            double dimexe = style.ExtLineExtend*style.DimScaleOverall;
            if (!style.ExtLine1Off)
                entities.Add(ExtensionLine(Vector2.Polar(ref1, dimexo, startAngle), Vector2.Polar(dimRef1, dimexe, startAngle), style, style.ExtLine1Linetype));
            if (!style.ExtLine2Off)
                entities.Add(ExtensionLine(Vector2.Polar(ref2, dimexo, endAngle), Vector2.Polar(dimRef2, dimexe, endAngle), style, style.ExtLine1Linetype));

            // dimension text
            double textRot = midRot - MathHelper.HalfPI;
            double gap = style.TextOffset*style.DimScaleOverall;
            if (textRot > MathHelper.HalfPI && textRot <= MathHelper.ThreeHalfPI)
            {
                textRot += MathHelper.PI;
                gap *= -1;
            }

            #region MText build as the Linear dimensions, two MTexts if the code "\X" is present

            //List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner.Record.Layout);
            //MText mText = DimensionText(Vector2.Polar(midDim, gap, midRot), MTextAttachmentPoint.BottomCenter, textRot, texts[0], style);
            //if (mText != null)
            //    entities.Add(mText);

            //// there might be an additional text if the code \X has been used in the dimension UserText 
            //// this additional text appears under the dimension line
            //if (texts.Count > 1)
            //{
            //    string text = texts[1];
            //    MText mText2 = DimensionText(Vector2.Polar(midDim, -gap, midRot), MTextAttachmentPoint.TopCenter, textRot, text, style);
            //    if (mText2 != null)
            //        entities.Add(mText2);
            //}

            #endregion

            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);
            string dimText;
            Vector2 position;
            MTextAttachmentPoint attachmentPoint;
            if (texts.Count > 1)
            {
                position = midDim;
                dimText = texts[0] + "\\P" + texts[1];
                attachmentPoint = MTextAttachmentPoint.MiddleCenter;
            }
            else
            {
                position = Vector2.Polar(midDim, gap, midRot);
                dimText = texts[0];
                attachmentPoint = MTextAttachmentPoint.TopCenter;
            }
            MText mText = DimensionText(position, attachmentPoint, textRot, dimText, style);
            if (mText != null)
                entities.Add(mText);

            dim.DefinitionPoint = MathHelper.Transform(new Vector3(midDim.X, midDim.Y, dim.Elevation), dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(midDim.X, midDim.Y, dim.Elevation); // this value is in OCS

            // drawing block
            return new Block(name, false, entities, null);
        }

        public static Block Build(DiametricDimension dim, string name)
        {
            double offset = dim.Offset;
            double measure = dim.Measurement;
            double radius = measure*0.5;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 centerRef = dim.CenterPoint;
            Vector2 ref1 = dim.ReferencePoint;

            double angleRef = Vector2.Angle(centerRef, ref1);
            Vector2 ref2 = Vector2.Polar(ref1, -measure, angleRef);

            short side;
            double minOffset = 2*style.ArrowSize + style.TextOffset*style.DimScaleOverall;

            if (offset >= radius && offset <= radius + minOffset)
            {
                offset = radius + minOffset;
                side = -1;
            }
            else if (offset >= radius - minOffset && offset <= radius)
            {
                offset = radius - minOffset;
                side = 1;
            }
            else if (offset > radius)
                side = -1;
            else
                side = 1;

            Vector2 dimRef = Vector2.Polar(centerRef, offset, angleRef);

            // reference points
            Layer defPoints = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1) {Layer = defPoints});

            if (!style.DimLineOff)
            {
                // dimension lines
                entities.Add(DimensionRadialLine(dimRef, ref1, angleRef, side, style));
                // dimension arrows
                entities.Add(EndArrowHead(ref1, (1 - side)*MathHelper.HalfPI + angleRef, style));
            }

            // center cross
            entities.AddRange(CenterCross(centerRef, radius, style));

            // dimension text
            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);
            string dimText ;
            if (texts.Count > 1)
                dimText = texts[0] + "\\P" + texts[1];
            else
                dimText = texts[0];

            double textRot = angleRef;
            short reverse = 1;
            if (textRot > MathHelper.HalfPI && textRot <= MathHelper.ThreeHalfPI)
            {
                textRot += MathHelper.PI;
                reverse = -1;
            }

            MTextAttachmentPoint attachmentPoint = reverse*side < 0 ? MTextAttachmentPoint.MiddleLeft : MTextAttachmentPoint.MiddleRight;
            MText mText = DimensionText(Vector2.Polar(dimRef, -reverse*side*style.TextOffset*style.DimScaleOverall, textRot), attachmentPoint, textRot, dimText, style);
            if (mText != null)
                entities.Add(mText);

            dim.DefinitionPoint = MathHelper.Transform(new Vector3(ref2.X, ref2.Y, dim.Elevation), dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(dimRef.X, dimRef.Y, dim.Elevation); // this value is in OCS

            return new Block(name, false, entities, null);
        }

        public static Block Build(RadialDimension dim, string name)
        {
            double offset = dim.Offset;
            double measure = dim.Measurement;
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            Vector2 centerRef = dim.CenterPoint;
            Vector2 ref1 = dim.ReferencePoint;

            double angleRef = Vector2.Angle(centerRef, ref1);

            short side;
            double minOffset = 2*style.ArrowSize + style.TextOffset*style.DimScaleOverall;
            if (offset >= measure && offset <= measure + minOffset)
            {
                offset = measure + minOffset;
                side = -1;
            }
            else if (offset >= measure - minOffset && offset <= measure)
            {
                offset = measure - minOffset;
                side = 1;
            }
            else if (offset > measure)
                side = -1;
            else
                side = 1;

            Vector2 dimRef = Vector2.Polar(centerRef, offset, angleRef);

            // reference points
            Layer defPoints = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(ref1) {Layer = defPoints});

            if (!style.DimLineOff)
            {
                // dimension line
                entities.Add(DimensionRadialLine(dimRef, ref1, angleRef, side, style));

                // dimension arrows
                entities.Add(EndArrowHead(ref1, (1 - side)*MathHelper.HalfPI + angleRef, style));
            }

            // center cross
            entities.AddRange(CenterCross(centerRef, measure, style));

            // dimension text
            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);

            string dimText;
            if (texts.Count > 1)
                dimText = texts[0] + "\\P" + texts[1];
            else
                dimText = texts[0];

            double textRot = angleRef;
            short reverse = 1;
            if (angleRef > MathHelper.HalfPI && angleRef <= MathHelper.ThreeHalfPI)
            {
                textRot += MathHelper.PI;
                reverse = -1;
            }

            MTextAttachmentPoint attachmentPoint = reverse*side < 0 ? MTextAttachmentPoint.MiddleLeft : MTextAttachmentPoint.MiddleRight;
            MText mText = DimensionText(Vector2.Polar(dimRef, -reverse*side*style.TextOffset*style.DimScaleOverall, textRot), attachmentPoint, textRot, dimText, style);
            if (mText != null)
                entities.Add(mText);

            dim.DefinitionPoint = MathHelper.Transform(new Vector3(centerRef.X, centerRef.Y, dim.Elevation), dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.MidTextPoint = new Vector3(dimRef.X, dimRef.Y, dim.Elevation); // this value is in OCS

            return new Block(name, false, entities, null);
        }

        public static Block Build(OrdinateDimension dim, string name)
        {
            DimensionStyle style = BuildDimensionStyleOverride(dim);
            List<EntityObject> entities = new List<EntityObject>();

            double measure = dim.Measurement;

            double angle = dim.Rotation*MathHelper.DegToRad;
            Vector2 refCenter = dim.Origin;

            Vector2 startPoint = refCenter + MathHelper.Transform(dim.ReferencePoint, angle, CoordinateSystem.Object, CoordinateSystem.World);

            if (dim.Axis == OrdinateDimensionAxis.X)
                angle += MathHelper.HalfPI;
            Vector2 endPoint = Vector2.Polar(startPoint, dim.Length, angle);

            // reference points
            Layer defPoints = new Layer("Defpoints") {Plot = false};
            entities.Add(new Point(refCenter) {Layer = defPoints});
            entities.Add(new Point(startPoint) {Layer = defPoints});

            short side = 1;
            if (dim.Length < 0)
                side = -1;

            // dimension lines
            entities.Add(new Line(Vector2.Polar(startPoint, side*style.ExtLineOffset*style.DimScaleOverall, angle), endPoint));

            // dimension text
            Vector2 midText = Vector2.Polar(startPoint, dim.Length + side*style.TextOffset*style.DimScaleOverall, angle);

            List<string> texts = FormatDimensionText(measure, dim.DimensionType, dim.UserText, style, dim.Owner);
            string dimText;
            if (texts.Count > 1)
                dimText = texts[0] + "\\P" + texts[1];
            else
                dimText = texts[0];

            MTextAttachmentPoint attachmentPoint = side < 0 ? MTextAttachmentPoint.MiddleRight : MTextAttachmentPoint.MiddleLeft;
            MText mText = DimensionText(midText, attachmentPoint, angle, dimText, style);
            if (mText != null)
                entities.Add(mText);

            IList<Vector3> wcsPoints = MathHelper.Transform(
                new[]
                {
                    new Vector3(startPoint.X, startPoint.Y, dim.Elevation),
                    new Vector3(endPoint.X, endPoint.Y, dim.Elevation),
                    new Vector3(dim.Origin.X, dim.Origin.Y, dim.Elevation)
                },
                dim.Normal, CoordinateSystem.Object, CoordinateSystem.World);
            dim.FirstPoint = wcsPoints[0];
            dim.SecondPoint = wcsPoints[1];
            dim.DefinitionPoint = wcsPoints[2];
            dim.MidTextPoint = new Vector3(midText.X, midText.Y, dim.Elevation); // this value is in OCS

            // drawing block
            return new Block(name, false, entities, null);
        }

        #endregion
    }
}