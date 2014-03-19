#region Copyright (C) 2014 Team MediaPortal

// Copyright (C) 2014 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using MediaPortal.GUI.Library;
using System.Windows.Forms;

namespace MediaPortal.ProcessPlugins.MiniDisplayPlugin.Drivers
{
    /// <summary>
    /// SoundGraph iMON MiniDisplay implementation.
    /// </summary>
    public class SoundGraphDisplay : IDisplay
    {
        ISoundGraphImonDisplay iDisplay;

        public SoundGraphDisplay()
        {
            iDisplay = null;
            Disabled = null;
            ImonErrorMessage = string.Empty;
            Initialized = false;      
        }

        public static void LogDebug(string msg) { Log.Debug(msg); }
        public static void LogInfo(string msg) { Log.Info(msg); }
        public static void LogError(string msg) { Log.Error(msg); }

        protected bool? Disabled { get; set; }

        protected string ImonErrorMessage { get; set; }

        protected bool Initialized { get; set; }

        //From IDisplay
        public string Description { get { return "SoundGraph display for iMON Manager >= 8.01.0419"; } }

        //From IDisplay
        //Notably used when testing to put on the screen
        public string Name 
        {
            get
            {
                if (iDisplay != null)
                {
                    return iDisplay.Name();
                }
                else
                {
                    return "SoundGraph iMON";
                }
                
            }
        }

        //From IDisplay
        public bool SupportsGraphics { get { return false; } }

        //From IDisplay
        public bool SupportsText { get { return true; } }


        //
        protected string ClassErrorName { get; set; }

        protected string UnsupportedDeviceErrorMessage { get; set; }

        protected DSPType DisplayType { get; set; }

        public string ErrorMessage
        {
            get
            {
                if (IsDisabled)
                {
                    return ImonErrorMessage;
                }
                return string.Empty;
            }
        }

        //From IDisplay
        public bool IsDisabled
        {
            get
            {
                //To check if our display is enabled we need to initialize it.
                bool success = DoInit();
                DoUninit();
                return !success;
            }
        }

        //From IDisplay
        public virtual void Dispose()
        {
            CleanUp();
        }

        //From IDisplay
        public virtual void Initialize()
        {
            LogDebug("SoundGraphDisplay.Initialize(): called");
            //Init if not already initialized
            if (IDW_IsInitialized() != DSPResult.DSP_SUCCEEDED)
            {
                bool success=DoInit();
                if (!success)
                {
                    return;
                }
                //Instantiate LCD or VFD accordingly
                if (DisplayType == DSPType.DSPN_DSP_LCD)
                {
                    LogDebug("SoundGraphDisplay.Initialize(): LCD");
                    iDisplay = new SoundGraphImonLcd();
                }
                else if (DisplayType == DSPType.DSPN_DSP_VFD)
                {
                    LogDebug("SoundGraphDisplay.Initialize(): VFD");
                    iDisplay = new SoundGraphImonVfd();
                }

            }
            Log.Debug("SoundGraphDisplay.Initialize(): completed");
        }

        //From IDisplay
        public virtual void CleanUp()
        {
            LogDebug("SoundGraphDisplay.CleanUp(): called");
            iDisplay = null; //hopefully that should destroy it
            DoUninit();
            Log.Debug("SoundGraphDisplay.CleanUp(): completed");
        }

        //From IDisplay
        public virtual void SetLine(int line, string message)
        {
            //Pass on that call to our actual display
            iDisplay.SetLine(line,message);
        }

        //From IDisplay
        public virtual void Configure()
        {   
            //We need to have an initialized display to be able to configure it
            Initialize();
            if (iDisplay != null)
            {
                //Display something for fun
                SetLine(0,"Adv. Settings");
                SetLine(1, Name);
            }
            else
            {
                //No display currently connected/initialized
                //Ask the user which display she wants to configure.
                Form form = new SoundGraphImonChooseDisplayTypeForm();
                DialogResult res=form.ShowDialog();
                form.Dispose();
                if (res == DialogResult.Yes)
                {
                    //LCD
                    iDisplay = new SoundGraphImonLcd();

                }
                else if (res == DialogResult.No)
                {
                    //VFD
                    iDisplay = new SoundGraphImonVfd();
                }
            }
            //Launch advanced settings dialog for our display
            iDisplay.Configure();
            CleanUp();
        }

        //From IDisplay
        public virtual void DrawImage(Bitmap bitmap)
        {
            // Not supported
        }

        //From IDisplay
        public void SetCustomCharacters(int[][] customCharacters)
        {
            // Not supported
        }

        //From IDisplay
        public void Setup(string port,
          int lines, int cols, int delay,
          int linesG, int colsG, int timeG,
          bool backLight, int backLightLevel,
          bool contrast, int contrastLevel,
          bool BlankOnExit)
        {
            // iMON VFD/LCD cannot be setup
        }

        /// <summary>
        /// Do display initialization.
        /// Returns true on success
        /// </summary>
        private bool DoInit()
        {
            IDW_INITRESULT initResult = new IDW_INITRESULT();
            DSPResult ret = IDW_Init(initResult);
            //Check the API call worked
            if (ret != DSPResult.DSP_SUCCEEDED)
            {
                ImonErrorMessage = DSPResult2String(ret);
                LogError(string.Format("{0}.IsDisabled: {1}", ClassErrorName, ImonErrorMessage));
                return false;
            }
            //Check that the initialization was carried out properly
            else if (initResult.iInitResult != DSPNInitResult.DSPN_SUCCEEDED)
            {
                ImonErrorMessage = DSPNResult2String(initResult.iInitResult);
                LogError(string.Format("{0}.IsDisabled: {1}", ClassErrorName, ImonErrorMessage));
                return false;
            }
            //Check we that we have a display
            else if (initResult.iDspType == DSPType.DSPN_DSP_NONE)
            {                
                ImonErrorMessage = UnsupportedDeviceErrorMessage;
                LogError(string.Format("{0}.IsDisabled: {1}", ClassErrorName, ImonErrorMessage));
                return false;
            }

            DisplayType = initResult.iDspType;
            return true;
        }

        /// <summary>
        /// Do display de-initialization.
        /// Returns true on success
        /// </summary>
        private void DoUninit()
        {
            DisplayType = DSPType.DSPN_DSP_NONE;
            IDW_Uninit();
        }


        /// <summary>
        /// Provide a string corresponding to the given DSPResult.
        /// </summary>
        protected string DSPResult2String(DSPResult result)
        {
            switch (result)
            {
                case DSPResult.DSP_SUCCEEDED:
                    return "Success";
                case DSPResult.DSP_E_FAIL:
                    return "An unknown error occurred";
                case DSPResult.DSP_E_OUTOFMEMORY:
                    return "Out of memory";
                case DSPResult.DSP_E_INVALIDARG:
                    return "One or more arguments are invalid";
                case DSPResult.DSP_E_NOT_INITED:
                    return "API is not initialized";
                case DSPResult.DSP_E_POINTER:
                    return "Pointer is invalid";
                default:
                    return "An unknown error occurred";
            }
        }

        /// <summary>
        /// Provide a string corresponding to the given DSPNInitResult.
        /// </summary>
        protected string DSPNResult2String(DSPNInitResult result)
        {
            switch (result)
            {
                case DSPNInitResult.DSPN_SUCCEEDED:
                    return "Success";
                case DSPNInitResult.DSPN_ERR_IN_USE:
                    return "Display plug-in is already used by another application";
                case DSPNInitResult.DSPN_ERR_HW_DISCONNECTED:
                    return "iMON hardware is not connected";
                case DSPNInitResult.DSPN_ERR_NOT_SUPPORTED_HW:
                    return "The connected hardware does not support the plug-in mode";
                case DSPNInitResult.DSPN_ERR_PLUGIN_DISABLED:
                    return "The plug-in mode option is disabled";
                case DSPNInitResult.DSPN_ERR_IMON_NO_REPLY:
                    return "The latest iMON is not installed or iMON is not running";
                default:
                    return "An unknown error occurred";
            }
        }

        //Possible return values from iMON Display APIs
        public enum DSPResult : int
        {
            DSP_SUCCEEDED = 0,
            DSP_E_FAIL = 1,
            DSP_E_OUTOFMEMORY = 2,
            DSP_E_INVALIDARG = 3,
            DSP_E_NOT_INITED = 4,
            DSP_E_POINTER = 5,
            DSP_S_INITED = 0x1000,
            DSP_S_NOT_INITED = 0x1001,
            DSP_S_IN_PLUGIN_MODE = 0x1002,
            DSP_S_NOT_IN_PLUGIN_MODE = 0x1003
        }

        //Possible results from display initialization 
        public enum DSPNInitResult : int
        {
            DSPN_SUCCEEDED = 0,
            DSPN_ERR_IN_USE = 0x0100,
            DSPN_ERR_HW_DISCONNECTED = 0x0101,
            DSPN_ERR_NOT_SUPPORTED_HW = 0x0102,
            DSPN_ERR_PLUGIN_DISABLED = 0x0103,
            DSPN_ERR_IMON_NO_REPLY = 0x0104,
            DSPN_ERR_UNKNOWN = 0x0200
        }

        //Type of display
        public enum DSPType : int
        {
            DSPN_DSP_NONE = 0,
            DSPN_DSP_VFD = 0x01,
            DSPN_DSP_LCD = 0x02
        };

        //Notification code
        public enum DSPNotifyCode : int
        {
            DSPNM_PLUGIN_SUCCEED = 0,
            DSPNM_PLUGIN_FAILED,
            DSPNM_IMON_RESTARTED,
            DSPNM_IMON_CLOSED,
            DSPNM_HW_CONNECTED,
            DSPNM_HW_DISCONNECTED,
            DSPNM_LCD_TEXT_SCROLL_DONE = 0x1000
        };

        [Flags]
        public enum OrangeDiskPieces : byte
        {
            Piece1 = 0x80, // top piece
            Piece2 = 0x40, // next piece counter-clockwise
            Piece3 = 0x20, // and so on...
            Piece4 = 0x10,
            Piece5 = 0x8,
            Piece6 = 0x4,
            Piece7 = 0x2,
            Piece8 = 0x1,
            None = 0x0
        }

        public enum OrangeDiskIcon : byte
        {
            On = 0x80,
            Off = 0x0
        }

        [Flags]
        public enum MediaTypes : byte
        {
            Music = 0x80,
            Movie = 0x40,
            Photo = 0x20,
            Cd_Dvd = 0x10,
            Tv = 0x8,
            WebCasting = 0x4,
            News_Weather = 0x2,
            None = 0x0
        }

        [Flags]
        public enum Speakers : byte
        {
            L = 0x80,
            C = 0x40,
            R = 0x20,
            SL = 0x10,
            LFE = 0x8,
            SR = 0x4,
            RL = 0x2,
            SPDIF = 0x1,
            None = 0x0
        }

        public enum SpeakersRR : byte
        {
            RR = 0x80,
            Off = 0x0
        }

        [Flags]
        public enum VideoCodecs : byte
        {
            MPG = 0x80,
            DIVX = 0x40,
            XVID = 0x20,
            WMV = 0x10,
            MPG2 = 0x8,
            AC3 = 0x4,
            DTS = 0x2,
            WMA = 0x1,
            None = 0x0
        }

        [Flags]
        public enum AudioCodecs : byte
        {
            MP3 = 0x80,
            OGG = 0x40,
            WMA = 0x20,
            WAV = 0x10,
            None = 0x0
        }

        [Flags]
        public enum AspectRatios : byte
        {
            SRC = 0x80,
            FIT = 0x40,
            TV = 0x20,
            HDTV = 0x10,
            SCR1 = 0x8,
            SCR2 = 0x4,
            None = 0x0
        }

        [Flags]
        public enum EtcIcons : byte
        {
            Repeat = 0x80,
            Shuffle = 0x40,
            Alarm = 0x20,
            Recording = 0x10,
            Volume = 0x8,
            Time = 0x4,
            None = 0x0
        }

        //Provide results from our iMON Display initialization
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 8)]
        protected class IDW_INITRESULT
        {
            [MarshalAs(UnmanagedType.U4)]
            public DSPNotifyCode iNotification;
            [MarshalAs(UnmanagedType.U4)]
            public DSPNInitResult iInitResult;
            [MarshalAs(UnmanagedType.U4)]
            public DSPType iDspType;
        }

        //Provide result for our status query
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 8)]
        protected class IDW_STATUS
        {
            [MarshalAs(UnmanagedType.U4)]
            public DSPNotifyCode iNotification;
        }

        //Declare a struct to pass our EqData
        [StructLayout(LayoutKind.Sequential)]
        public class DSPEQDATA
        {
            public DSPEQDATA()
            {
                BandData = new int[16];
            }

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public readonly int[] BandData;
        }

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        protected static extern DSPResult IDW_Init(IDW_INITRESULT initResult);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        protected static extern DSPResult IDW_Uninit();

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        protected static extern DSPResult IDW_IsInitialized();

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        protected static extern DSPResult IDW_GetStatus(IDW_STATUS status);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetVfdText([MarshalAs(UnmanagedType.LPWStr)] string line1, [MarshalAs(UnmanagedType.LPWStr)] string line2);

        //Import function to set VFD EQDATA
        [DllImport("iMONDisplayWrapper.dll")]
        public static extern int IDW_SetVfdEqData(DSPEQDATA EqData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdText([MarshalAs(UnmanagedType.LPWStr)] string line);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdAllIcons([MarshalAs(UnmanagedType.Bool)] bool on);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdOrangeIcon(byte iconData1, byte iconData2);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdMediaTypeIcon(byte iconData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdSpeakerIcon(byte iconData1, byte iconData2);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdVideoCodecIcon(byte iconData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdAudioCodecIcon(byte iconData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdAspectRatioIcon(byte iconData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdEtcIcon(byte iconData);

        [DllImport("iMONDisplayWrapper.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        public static extern DSPResult IDW_SetLcdProgress(int currentPosition, int total);

    }
}
