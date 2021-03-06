﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DTMEditor.FileHandling.ControllerData;

namespace pianokeys
{
    public class Frame
    {
        public bool ST { get; set; }
        public bool A { get; set; }
        public bool B { get; set; }
        public bool X { get; set; }
        public bool Y { get; set; }
        public bool DL { get; set; }
        public bool DR { get; set; }
        public bool DU { get; set; }
        public bool DD { get; set; }
        public bool Z { get; set; }
        public bool L { get; set; }
        public bool R { get; set; }
        public byte LP { get; set; }
        public byte RP { get; set; }

        public byte LX { get; set; }
        public byte LY { get; set; }
        public byte CX { get; set; }
        public byte CY { get; set; }

        private string note;
        public string Note { get { return note; } set {
                if (value == null)
                    this.note = "";
                else
                    this.note = value.Trim();
            }
        }

        public Frame()
        {
            A = false;
            B = false;
            X = false;
            Y = false;
            Z = false;
            DU = false;
            DD = false;
            DL = false;
            DR = false;
            ST = false;

            CX = 128;
            CY = 128;
            LX = 128;
            LY = 128;
            LP = 0;
            RP = 0;

            Note = "";
        }

        /** Make a grid frame from a controller datum. */
        public Frame (DTMControllerDatum datum)
        {
            A = datum.IsButtonPressed(GameCubeButton.A);
            B = datum.IsButtonPressed(GameCubeButton.B);
            L = datum.IsButtonPressed(GameCubeButton.L);
            R = datum.IsButtonPressed(GameCubeButton.R);
            X = datum.IsButtonPressed(GameCubeButton.X);
            Y = datum.IsButtonPressed(GameCubeButton.Y);
            Z = datum.IsButtonPressed(GameCubeButton.Z);
            DU = datum.IsButtonPressed(GameCubeButton.DPadUp);
            DD = datum.IsButtonPressed(GameCubeButton.DPadDown);
            DL = datum.IsButtonPressed(GameCubeButton.DPadLeft);
            DR = datum.IsButtonPressed(GameCubeButton.DPadRight);
            ST = datum.IsButtonPressed(GameCubeButton.Start);

            CX = (byte)datum.GetAxisValue(GameCubeAxis.CStickXAxis);
            CY = (byte)datum.GetAxisValue(GameCubeAxis.CStickYAxis);
            LX = (byte)datum.GetAxisValue(GameCubeAxis.AnalogXAxis);
            LY = (byte)datum.GetAxisValue(GameCubeAxis.AnalogYAxis);

            LP = (byte)datum.GetTriggerValue(GameCubeTrigger.L);
            RP = (byte)datum.GetTriggerValue(GameCubeTrigger.R);

            Note = "";
        }

        public Frame(Frame frameToCopy)
        {
            copyDataFromFrame(frameToCopy);
        }

        public void copyDataFromFrame(Frame frameToCopy)
        {
            this.A = frameToCopy.A;
            this.B = frameToCopy.B;
            this.L = frameToCopy.L;
            this.R = frameToCopy.R;
            this.X = frameToCopy.X;
            this.Y = frameToCopy.Y;
            this.Z = frameToCopy.Z;
            this.DU = frameToCopy.DU;
            this.DD = frameToCopy.DD;
            this.DL = frameToCopy.DL;
            this.DR = frameToCopy.DR;
            this.ST = frameToCopy.ST;
            this.CX = frameToCopy.CX;
            this.CY = frameToCopy.CY;
            this.LX = frameToCopy.LX;
            this.LY = frameToCopy.LY;
            this.LP = frameToCopy.LP;
            this.RP = frameToCopy.RP;

            Note = frameToCopy.Note;
        }

        /** Make a controller datum for writing from a grid frame. */
        public static DTMControllerDatum MakeFileDatum(Frame frame)
        {
            DTMControllerDatum dt = new DTMControllerDatum(0);

            dt.ModifyButton(GameCubeButton.Start, frame.ST);
            dt.ModifyButton(GameCubeButton.A, frame.A);
            dt.ModifyButton(GameCubeButton.B, frame.B);
            dt.ModifyButton(GameCubeButton.X, frame.X);
            dt.ModifyButton(GameCubeButton.Y, frame.Y);
            dt.ModifyButton(GameCubeButton.L, frame.L);
            dt.ModifyButton(GameCubeButton.R, frame.R);
            dt.ModifyButton(GameCubeButton.Z, frame.Z);
            dt.ModifyButton(GameCubeButton.DPadUp, frame.DU);
            dt.ModifyButton(GameCubeButton.DPadRight, frame.DR);
            dt.ModifyButton(GameCubeButton.DPadLeft, frame.DL);
            dt.ModifyButton(GameCubeButton.DPadDown, frame.DD);

            dt.ModifyAxis(GameCubeAxis.AnalogXAxis, frame.LX);
            dt.ModifyAxis(GameCubeAxis.AnalogYAxis, frame.LY);
            dt.ModifyAxis(GameCubeAxis.CStickXAxis, frame.CX);
            dt.ModifyAxis(GameCubeAxis.CStickYAxis, frame.CY);

            dt.ModifyTrigger(GameCubeTrigger.L, frame.LP);
            dt.ModifyTrigger(GameCubeTrigger.R, frame.RP);

            return dt;
        }

        public bool Equals(Frame other)
        {
            return
                this.Note == other.Note &&
                this.LX == other.LX &&
                this.LY == other.LY &&
                this.CX == other.CX &&
                this.CY == other.CY &&
                this.LP == other.LP &&
                this.RP == other.RP &&
                this.A == other.A &&
                this.B == other.B &&
                this.L == other.L &&
                this.R == other.R &&
                this.X == other.X &&
                this.Y == other.Y &&
                this.Z == other.Z &&
                this.DU == other.DU &&
                this.DD == other.DD &&
                this.DL == other.DL &&
                this.DR == other.DR &&
                this.ST == other.ST
            ;   
        }
    }
}
