﻿/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MapAssist.Types;
using MapAssist.Settings;

namespace MapAssist.Helpers
{
    public class Compositor
    {
        private readonly AreaData _areaData;
        private readonly Bitmap _background;
        public readonly Point CropOffset;
        private readonly IReadOnlyList<PointOfInterest> _pointsOfInterest;
        private readonly Dictionary<(string, int), Font> _fontCache = new Dictionary<(string, int), Font>();

        private readonly Dictionary<(Shape, int, Color, float), Bitmap> _iconCache =
            new Dictionary<(Shape, int, Color, float), Bitmap>();

        public Compositor(AreaData areaData, IReadOnlyList<PointOfInterest> pointOfInterest)
        {
            _areaData = areaData;
            _pointsOfInterest = pointOfInterest;
            (_background, CropOffset) = DrawBackground(areaData, pointOfInterest);
        }

        public Bitmap Compose(GameData gameData, bool scale = true)
        {
            if (gameData.Area != _areaData.Area)
            {
                throw new ApplicationException("Asked to compose an image for a different area." +
                                               $"Compositor area: {_areaData.Area}, Game data: {gameData.Area}");
            }

            var image = (Bitmap)_background.Clone();

            using (var imageGraphics = Graphics.FromImage(image))
            {
                imageGraphics.CompositingQuality = CompositingQuality.HighQuality;
                imageGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                imageGraphics.SmoothingMode = SmoothingMode.HighQuality;
                imageGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Point localPlayerPosition = gameData.PlayerPosition
                    .OffsetFrom(_areaData.Origin)
                    .OffsetFrom(CropOffset)
                    .OffsetFrom(GetIconOffset(Settings.Rendering.Player.IconSize));

                var playerIconRadius = GetIconRadius(Settings.Rendering.Player.IconSize);

                if (Rendering.Player.CanDrawIcon())
                {
                    Bitmap playerIcon = GetIcon(Settings.Rendering.Player);
                    imageGraphics.DrawImage(playerIcon, localPlayerPosition);
                }

                // The lines are dynamic, and follow the player, so have to be drawn here.
                // The rest can be done in DrawBackground.
                foreach (PointOfInterest poi in _pointsOfInterest)
                {
                    if (poi.RenderingSettings.CanDrawLine())
                    {
                        var pen = new Pen(poi.RenderingSettings.LineColor, poi.RenderingSettings.LineThickness);
                        if (poi.RenderingSettings.CanDrawArrowHead())
                        {
                            pen.CustomEndCap = new AdjustableArrowCap(poi.RenderingSettings.ArrowHeadSize,
                                poi.RenderingSettings.ArrowHeadSize);
                        }

                        var localPlayerCenterPosition = new Point(
                            localPlayerPosition.X + playerIconRadius,
                            localPlayerPosition.Y + playerIconRadius
                        );
                        var poiPosition = poi.Position.OffsetFrom(_areaData.Origin).OffsetFrom(CropOffset);

                        imageGraphics.DrawLine(pen, localPlayerCenterPosition, poiPosition);
                    }
                }
                MonsterRendering renderMonster = Utils.GetMonsterRendering();
                foreach (var unitAny in gameData.Monsters)
                {
                    var clr = unitAny.IsElite() ? renderMonster.EliteColor : renderMonster.NormalColor;
                    var pen = new Pen(clr, 1);
                    var sz = new Size(5, 5);
                    var sz2 = new Size(2, 2);
                    var pos = new Point(unitAny.Path.DynamicX, unitAny.Path.DynamicY);
                    var midPoint = pos.OffsetFrom(_areaData.Origin).OffsetFrom(CropOffset);
                    var rect = new Rectangle(midPoint, sz);
                    imageGraphics.DrawRectangle(pen, rect);
                    var i = 0;
                    foreach (var immunity in unitAny.Immunities)
                    {
                        var brush = new SolidBrush(ResistColors.ResistColor[immunity]);
                        //shove the point we're drawing the immunity at to the left to align based on number of immunities
                        var iPoint = new Point((i * -2) + (1 * (unitAny.Immunities.Count - 1)) - 1, 3);
                        var pen2 = new Pen(ResistColors.ResistColor[immunity], 1);
                        var rect2 = new Rectangle(midPoint.OffsetFrom(iPoint), sz2);
                        imageGraphics.FillRectangle(brush, rect2);
                        i++;
                    }
                }
            }

            double multiplier = 1;

            if (scale)
            {
                double biggestDimension = Math.Max(image.Width, image.Height);

                multiplier = Settings.Map.Size / biggestDimension;

                if (multiplier == 0)
                {
                    multiplier = 1;
                }
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (multiplier != 1)
            {
                image = ImageUtils.ResizeImage(image, (int)(image.Width * multiplier),
                    (int)(image.Height * multiplier));
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (scale && Settings.Map.Rotate)
            {
                image = ImageUtils.RotateImage(image, 53, true, false, Color.Transparent);
            }

            return image;
        }

        private (Bitmap, Point) DrawBackground(AreaData areaData, IReadOnlyList<PointOfInterest> pointOfInterest)
        {
            var background = new Bitmap(areaData.CollisionGrid[0].Length, areaData.CollisionGrid.Length,
                PixelFormat.Format32bppArgb);
            using (var backgroundGraphics = Graphics.FromImage(background))
            {
                backgroundGraphics.FillRectangle(new SolidBrush(Color.Transparent), 0, 0,
                    areaData.CollisionGrid[0].Length,
                    areaData.CollisionGrid.Length);
                backgroundGraphics.CompositingQuality = CompositingQuality.HighQuality;
                backgroundGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                backgroundGraphics.SmoothingMode = SmoothingMode.HighQuality;
                backgroundGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                for (var y = 0; y < areaData.CollisionGrid.Length; y++)
                {
                    for (var x = 0; x < areaData.CollisionGrid[y].Length; x++)
                    {
                        var type = areaData.CollisionGrid[y][x];
                        Color? typeColor = Settings.Map.MapColors[type];
                        if (typeColor != null)
                        {
                            background.SetPixel(x, y, (Color)typeColor);
                        }
                    }
                }

                foreach (PointOfInterest poi in pointOfInterest)
                {
                    if (poi.RenderingSettings.CanDrawIcon())
                    {
                        Bitmap icon = GetIcon(poi.RenderingSettings);
                        Point origin = poi.Position
                            .OffsetFrom(areaData.Origin)
                            .OffsetFrom(GetIconOffset(poi.RenderingSettings.IconSize));
                        backgroundGraphics.DrawImage(icon, origin);
                    }

                    if (!string.IsNullOrWhiteSpace(poi.Label) && poi.RenderingSettings.CanDrawLabel())
                    {
                        Font font = GetFont(poi.RenderingSettings);
                        backgroundGraphics.DrawString(poi.Label, font,
                            new SolidBrush(poi.RenderingSettings.LabelColor),
                            poi.Position.OffsetFrom(areaData.Origin));
                    }
                }

                return ImageUtils.CropBitmap(background);
            }
        }

        private Font GetFont(PointOfInterestRendering poiSettings)
        {
            (string LabelFont, int LabelFontSize) cacheKey = (poiSettings.LabelFont, poiSettings.LabelFontSize);
            if (!_fontCache.ContainsKey(cacheKey))
            {
                var font = new Font(poiSettings.LabelFont,
                    poiSettings.LabelFontSize);
                _fontCache[cacheKey] = font;
            }

            return _fontCache[cacheKey];
        }

        private Bitmap GetIcon(PointOfInterestRendering poiSettings)
        {
            (Shape IconShape, int IconSize, Color Color, float LineThickness) cacheKey = (
                poiSettings.IconShape,
                poiSettings.IconSize,
                poiSettings.IconColor,
                poiSettings.LineThickness
            );
            if (!_iconCache.ContainsKey(cacheKey))
            {
                var bitmap = new Bitmap(poiSettings.IconSize, poiSettings.IconSize, PixelFormat.Format32bppArgb);
                var pen = new Pen(poiSettings.IconColor, poiSettings.LineThickness);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    switch (poiSettings.IconShape)
                    {
                        case Shape.Ellipse:
                            g.FillEllipse(new SolidBrush(poiSettings.IconColor), 0, 0, poiSettings.IconSize,
                                poiSettings.IconSize);
                            break;
                        case Shape.Rectangle:
                            g.FillRectangle(new SolidBrush(poiSettings.IconColor), 0, 0, poiSettings.IconSize,
                                poiSettings.IconSize);
                            break;
                        case Shape.Polygon:
                            var halfSize = poiSettings.IconSize / 2;
                            var cutSize = poiSettings.IconSize / 10;
                            PointF[] curvePoints = {
                                new PointF(0, halfSize),
                                new PointF(halfSize - cutSize, halfSize - cutSize),
                                new PointF(halfSize, 0),
                                new PointF(halfSize + cutSize, halfSize - cutSize),
                                new PointF(poiSettings.IconSize, halfSize),
                                new PointF(halfSize + cutSize, halfSize + cutSize),
                                new PointF(halfSize, poiSettings.IconSize),
                                new PointF(halfSize - cutSize, halfSize + cutSize)
                            };
                            g.FillPolygon(new SolidBrush(poiSettings.IconColor), curvePoints);
                            break;
                        case Shape.Cross:
                            var a = poiSettings.IconSize * 0.0833333f;
                            var b = poiSettings.IconSize * 0.3333333f;
                            var c = poiSettings.IconSize * 0.6666666f;
                            var d = poiSettings.IconSize * 0.9166666f;
                            PointF[] crossLinePoints = {
                                new PointF(c, a), new PointF(c, b), new PointF(d, b),
                                new PointF(d, c), new PointF(c, c), new PointF(c, d),
                                new PointF(b, d), new PointF(b, c), new PointF(a, c),
                                new PointF(a, b), new PointF(b, b), new PointF(b, a),
                                new PointF(c, a)
                            };
                            for (var p = 0; p < crossLinePoints.Length - 1; p++)
                            {
                                g.DrawLine(pen, crossLinePoints[p], crossLinePoints[p + 1]);
                            }
                            break;
                    }
                }

                _iconCache[cacheKey] = bitmap;
            }

            return _iconCache[cacheKey];
        }

        private int GetIconRadius(int iconSize)
        {
            return (int)Math.Floor((decimal)iconSize / 2);
        }

        private Point GetIconOffset(int iconSize)
        {
            var radius = GetIconRadius(iconSize);
            return new Point(radius, radius);
        }
    }
}
