﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace plottrBot
{
    class Plottr
    {

        public BitmapImage Img { get; private set; }
        private bool[,] pixelArray { get; set; }        //array used for storing white/black pixels as true/false
        public List<TraceLine> BlackLines { get; private set; }     //stores all black lines to be drawn
        public List<TraceLine> AllLines { get; private set; }       //stores all movements as straight lines
        public List<string> GeneratedGCODE { get; set; }            //GCODE commands to be sent to the robot
        public int ImgMoveX { get; set; }
        public int ImgMoveY { get; set; }
        private Bitmap TempImg { get; set; }
        static public double ToolDiameter { get; set; }
        public double GetImgWidth { get { return Img.Width * (25.4 / 96); } }      //gets width in mm
        public double GetImgHeight { get { return Img.Height * (25.4 / 96); } }    //gets height in mm
        public TraceLine BoundingCoordinates { get; set; }      //stores coordinates for bounding box around picture

        private double ratioWidthToPx { get; set; }
        private double ratioHeightToPx { get; set; }
        private int pxArrayWidth { get; set; }
        private int pxArrayHeight { get; set; }
        public string StartGCODE { get; set; }      //gcode that runs before the drawing starts
        public string EndGCODE { get; set; }        //gcode that runs when the drawing has finished

        static public bool TimedOut { get; set; }


        public Plottr(string filename)
        {
            Img = new BitmapImage(new Uri(filename, UriKind.Absolute));
            TempImg = new Bitmap(filename);
            BlackLines = new List<TraceLine>();
            AllLines = new List<TraceLine>();
            GeneratedGCODE = new List<string>();
            ImgMoveX = 0;
            ImgMoveY = 0;
            StartGCODE = "";
            EndGCODE = "";
        }

        private void imgToArray()
        {
            ratioWidthToPx = GetImgWidth / Img.PixelWidth;     //calculates the ratio between picture width in mm and number of pixels in width
            ratioHeightToPx = GetImgHeight / Img.PixelHeight;
            pxArrayWidth = (Int32)(TempImg.Width / ToolDiameter);
            pxArrayHeight = (Int32)(TempImg.Height / ToolDiameter);

            //double dpi = 25.4 / ToolDiameter;
            //pxArrayWidth = (Int32)(ratioWidthToPx * dpi);
            //pxArrayHeight = (Int32)(ratioHeightToPx * dpi);

            //Bitmap tempImg = new Bitmap(1, 1);
            //using (MemoryStream outStream = new MemoryStream())
            //{
            //    BitmapEncoder enc = new BmpBitmapEncoder();
            //    enc.Frames.Add(BitmapFrame.Create(Img));
            //    enc.Save(outStream);
            //    System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

            //    Bitmap tempImp = new Bitmap(bitmap);
            //}

            int blackPixelThreshold = 60;

            pixelArray = new bool[pxArrayWidth, pxArrayHeight];
            for (int x = 0; x < pxArrayWidth; x++)
            {
                for (int y = 0; y < pxArrayHeight; y++)
                {
                    Color pixelColor = TempImg.GetPixel(x, y);
                    //Img.CopyPixels()
                    //if (pixelColor.R <= blackPixelThreshold && pixelColor.G <= blackPixelThreshold && pixelColor.B <= blackPixelThreshold)
                    if (pixelColor.R == pixelColor.G && pixelColor.R == pixelColor.B && pixelColor.R <= blackPixelThreshold)
                        pixelArray[x, y] = true;        //true = black pixel
                    else
                        pixelArray[x, y] = false;       //false = white pixel
                }
            }
        }



        private void calcMovementSideToSide()
        {
            imgToArray();
            BlackLines.Clear();

            bool lineStarted = false;
            int x0 = 0, y0 = 0;
            bool leftToRight = true;

            for (int y = 0; y < pxArrayHeight; y++)
            {
                for (int i = 0; i < pxArrayWidth - 1; i++)
                {
                    int x;
                    if (leftToRight) x = i;
                    else x = pxArrayWidth - 1 - i;

                    if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
                    {
                        x0 = x;         //saves coordinates for start of new line
                        y0 = y;
                        lineStarted = true;
                    }

                    int endX = x;
                    if(lineStarted)
                    {
                        if(leftToRight && (!pixelArray[x + 1, y] || x >= pxArrayWidth - 2))
                        {
                            if (pixelArray[x + 1, y])    //check the very last pixel as well
                                endX = x + 1;
                            lineStarted = false;        //start a new line
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (endX * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                        if(!leftToRight && (!pixelArray[x - 1, y] || x <= 1))
                        {
                            if (pixelArray[x - 1, y])    //check the very last pixel as well
                                endX = x + 1;
                            lineStarted = false;
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (endX * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                    }
                }
                leftToRight = !leftToRight;
            } //for y
        }

        private void calcMovementUpDown()
        {
            imgToArray();
            BlackLines.Clear();

            bool lineStarted = false;
            int x0 = 0, y0 = 0;
            bool upToDown = true;

            for (int x = 0; x < pxArrayWidth; x++)
            {
                for (int i = 0; i < pxArrayHeight - 1; i++)
                {
                    int y;
                    if (upToDown) y = i;
                    else y = pxArrayHeight - 1 - i;

                    if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
                    {
                        x0 = x;         //saves coordinates for start of new line
                        y0 = y;
                        lineStarted = true;
                    }

                    int endY = y;
                    if (lineStarted)
                    {
                        if (upToDown && (!pixelArray[x, y + 1] || y >= pxArrayHeight - 2))
                        {
                            if (pixelArray[x, y + 1])    //check the very last pixel as well
                                endY = y + 1;
                            lineStarted = false;        //start a new line
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (endY * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                        if (!upToDown && (!pixelArray[x, y - 1] || y <= 1))
                        {
                            if (pixelArray[x, y - 1])    //check the very last pixel as well
                                endY = y + 1;
                            lineStarted = false;
                            BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (endY * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
                        }
                    }
                }
                upToDown = !upToDown;
            } //for x
        }


        public void GenerateGCODE()
        {
            //calcMovementSideToSide();
            calcMovementUpDown();
            AllLines.Clear();
            GeneratedGCODE.Clear();

            double xMinVal = BlackLines[0].X0;
            double xMaxVal = BlackLines[0].X1;
            double yMinVal = BlackLines[0].Y0;
            double yMaxVal = BlackLines[0].Y1;

            for (int i = 0; i < BlackLines.Count - 1; i++)
            {
                AllLines.Add(new TraceLine(BlackLines[i].X0, BlackLines[i].Y0, BlackLines[i].X1, BlackLines[i].Y1, true));
                AllLines.Add(new TraceLine(BlackLines[i].X1, BlackLines[i].Y1, BlackLines[i + 1].X0, BlackLines[i + 1].Y0, false));

                if (BlackLines[i].X0 < xMinVal || BlackLines[i].X1 < xMinVal)
                    xMinVal = Math.Min(BlackLines[i].X0, BlackLines[i].X1);
                if (BlackLines[i].X0 > xMaxVal || BlackLines[i].X1 > xMaxVal)
                    xMaxVal = Math.Max(BlackLines[i].X0, BlackLines[i].X1);
                if (BlackLines[i].Y0 < yMinVal || BlackLines[i].Y1 < yMinVal)
                    yMinVal = Math.Min(BlackLines[i].Y0, BlackLines[i].Y1);
                if (BlackLines[i].Y0 > yMaxVal || BlackLines[i].Y1 > yMaxVal)
                    yMaxVal = Math.Max(BlackLines[i].Y0, BlackLines[i].Y1);

            }

            BoundingCoordinates = new TraceLine(xMinVal, yMinVal, xMaxVal, yMaxVal);

            //GeneratedGCODE.Add("G1 Z1\n");        //starts with the pen not touching the canvas
            GeneratedGCODE.Add(StartGCODE + "\n");
            GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", AllLines[0].X0, AllLines[0].Y0));    //goes from home position to start of first line to draw
            foreach (TraceLine line in AllLines)
            {
                GeneratedGCODE.Add("G1 Z" + Convert.ToInt32(!line.Draw) + "\n");
                GeneratedGCODE.Add(string.Format("G1 X{0} Y{1}\n", line.X1, line.Y1));
            }
            GeneratedGCODE.Add(EndGCODE + "\n");

        }

        //public void RenderCircles()
        //{
        //    const int RADIUS = 40; //in pixels
        //}

        public void ImgToCSV()
        {
            using (StreamWriter outFile = new StreamWriter("imgArray.csv"))
            {
                for (int y = 0; y < Img.Height; y++)
                {
                    for (int x = 0; x < Img.Width; x++)
                        outFile.Write(pixelArray[x, y] + ",");
                    outFile.WriteLine();
                }
            }

        }

        //private void calMovementSideToSide()
        //{
        //    imgToArray();

        //    bool lineStarted = false;
        //    int x0 = 0, y0 = 0;
        //    bool leftToRight = true;

        //    for (int y = 0; y < pxArrayHeight; y++)
        //    {
        //        if (leftToRight)
        //        {
        //            for (int x = 0; x < pxArrayWidth - 1; x++)
        //            {
        //                if (!lineStarted && pixelArray[x, y])       //if this is the first black pixel in a new line
        //                {
        //                    x0 = x;         //saves coordinates for start of new line
        //                    y0 = y;
        //                    lineStarted = true;
        //                }
        //                if (lineStarted && (!pixelArray[x + 1, y] || x >= pxArrayWidth - 2))       //if next pixel is white or the edge of image
        //                {
        //                    if (pixelArray[x + 1, y])    //check the very last pixel as well
        //                        //BlackLines.Add(new TraceLine(x0 + ImgMoveX, y0 + ImgMoveY, (x + 1) + ImgMoveX, y + ImgMoveY));      //saves coordinates of last pixel in the line
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, ((x + 1) * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    else
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    lineStarted = false;        //start a new line
        //                }
        //            }
        //            leftToRight = false;
        //        }
        //        else
        //        {
        //            for (int x = pxArrayWidth - 1; x > 0; x--)
        //            {
        //                if (!lineStarted && pixelArray[x, y])
        //                {
        //                    x0 = x;
        //                    y0 = y;
        //                    lineStarted = true;
        //                }
        //                if (lineStarted && (!pixelArray[x - 1, y] || x <= 1))
        //                {
        //                    if (pixelArray[x - 1, y])    //check the very last pixel as well
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, ((x + 1) * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    else
        //                        BlackLines.Add(new TraceLine((x0 * ratioWidthToPx) + ImgMoveX, (y0 * ratioHeightToPx) + ImgMoveY, (x * ratioWidthToPx) + ImgMoveX, (y * ratioHeightToPx) + ImgMoveY));      //saves coordinates of last pixel in the line
        //                    lineStarted = false;
        //                }
        //            }
        //            leftToRight = true;
        //        }


        //    } //for y
        //}

    }


    class TraceLine
    {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public bool Draw { get; set; }

        public TraceLine(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public TraceLine(double x0, double y0, double x1, double y1, bool draw)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            Draw = draw;
        }

    }

}
