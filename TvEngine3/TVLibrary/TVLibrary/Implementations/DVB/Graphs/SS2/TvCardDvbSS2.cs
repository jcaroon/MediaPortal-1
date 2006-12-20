/* 
 *	Copyright (C) 2005-2006 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
//#define FORM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using DirectShowLib;
using DirectShowLib.SBE;
using DirectShowLib.BDA;
using TvLibrary.Interfaces;
using TvLibrary.Interfaces.Analyzer;
using TvLibrary.Epg;
using TvLibrary.Channels;
using TvLibrary.Implementations.DVB.Structures;
using TvLibrary.Teletext;
using TvLibrary.Helper;

namespace TvLibrary.Implementations.DVB
{
  /// <summary>
  /// Implementation of <see cref="T:TvLibrary.Interfaces.ITVCard"/> which handles the SkyStar 2 DVB-S card
  /// </summary>
  public class TvCardDvbSS2 : TvCardDvbBase, IDisposable, ITVCard
  {
    #region imports

    [ComImport, Guid("fc50bed6-fe38-42d3-b831-771690091a6e")]
    class MpTsAnalyzer { }

    #endregion

    #region enums

    enum TunerType
    {
      ttSat = 0,
      ttCable = 1,
      ttTerrestrial = 2,
      ttATSC = 3,
      ttUnknown = -1
    }
    enum eModulationTAG
    {
      QAM_4 = 2,
      QAM_16,
      QAM_32,
      QAM_64,
      QAM_128,
      QAM_256,
      MODE_UNKNOWN = -1
    }
    enum GuardIntervalType
    {
      Interval_1_32 = 0,
      Interval_1_16,
      Interval_1_8,
      Interval_1_4,
      Interval_Auto
    }
    enum BandWidthType
    {
      MHz_6 = 6,
      MHz_7 = 7,
      MHz_8 = 8,
    }
    enum SS2DisEqcType
    {
      None = 0,
      Simple_A,
      Simple_B,
      Level_1_A_A,
      Level_1_B_A,
      Level_1_A_B,
      Level_1_B_B
    }
    enum FecType
    {
      Fec_1_2 = 1,
      Fec_2_3,
      Fec_3_4,
      Fec_5_6,
      Fec_7_8,
      Fec_Auto
    }

    enum LNBSelectionType
    {
      Lnb0 = 0,
      Lnb22kHz,
      Lnb33kHz,
      Lnb44kHz,
    }

    enum PolarityType
    {
      Horizontal = 0,
      Vertical,
    }

    enum CardType
    {
      Analog,
      DvbS,
      DvbT,
      DvbC,
      Atsc
    }

    #endregion

    #region Structs
    //
    //	Structure completedy by GetTunerCapabilities() to return tuner capabilities
    //
    private struct tTunerCapabilities
    {
      public TunerType eModulation;
      public int dwConstellationSupported;       // Show if SetModulation() is supported
      public int dwFECSupported;                 // Show if SetFec() is suppoted
      public int dwMinTransponderFreqInKHz;
      public int dwMaxTransponderFreqInKHz;
      public int dwMinTunerFreqInKHz;
      public int dwMaxTunerFreqInKHz;
      public int dwMinSymbolRateInBaud;
      public int dwMaxSymbolRateInBaud;
      public int bAutoSymbolRate;				// Obsolte		
      public int dwPerformanceMonitoring;        // See bitmask definitions below
      public int dwLockTimeInMilliSecond;		// lock time in millisecond
      public int dwKernelLockTimeInMilliSecond;	// lock time for kernel
      public int dwAcquisitionCapabilities;
    }

    #endregion

    #region variables
    CardType _cardType;
    IBaseFilter _filterB2C2Adapter;
    DVBSkyStar2Helper.IB2C2MPEG2DataCtrl3 _interfaceB2C2DataCtrl;
    DVBSkyStar2Helper.IB2C2MPEG2TunerCtrl2 _interfaceB2C2TunerCtrl;


    #endregion

    #region imports
    [DllImport("advapi32", CharSet = CharSet.Auto)]
    static extern ulong RegOpenKeyEx(IntPtr key, string subKey, uint ulOptions, uint sam, out IntPtr resultKey);

    [DllImport("dvblib.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    static extern int SetPidToPin(DVBSkyStar2Helper.IB2C2MPEG2DataCtrl3 dataCtrl, UInt16 pin, UInt16 pid);

    [DllImport("dvblib.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool DeleteAllPIDs(DVBSkyStar2Helper.IB2C2MPEG2DataCtrl3 dataCtrl, UInt16 pin);

    [DllImport("dvblib.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetSNR(DVBSkyStar2Helper.IB2C2MPEG2TunerCtrl2 tunerCtrl, [Out] out int a, [Out] out int b);

    #endregion

    #region ctor
    /// <summary>
    /// Initializes a new instance of the <see cref="TvCardDvbSS2"/> class.
    /// </summary>
    /// <param name="device">The device.</param>
    public TvCardDvbSS2(DsDevice device)
    {
      _conditionalAccess = new ConditionalAccess(null, null);
      _tunerDevice = device;
      GetTunerCapabilities();
    }
    #endregion


    #region properties
    /// <summary>
    /// Gets/sets the card name
    /// </summary>
    public string Name
    {
      get
      {
        return _tunerDevice.Name;
      }
      set
      {
      }
    }
    /// <summary>
    /// gets the current filename used for recording
    /// </summary>
    /// <value></value>
    public string FileName
    {
      get
      {
        return _recordingFileName;
      }
    }
    /// <summary>
    /// returns true if card is currently recording
    /// </summary>
    /// <value></value>
    public bool IsRecording
    {
      get
      {
        return (_graphState == GraphState.Recording);
      }
    }
    /// <summary>
    /// returns true if card is currently timeshifting
    /// </summary>
    /// <value></value>
    public bool IsTimeShifting
    {
      get
      {
        return (_graphState == GraphState.TimeShifting);
      }
    }

    /// <summary>
    /// Gets/sets the card cardType
    /// </summary>
    public int cardType
    {
      get
      {
        return (int)_cardType;
      }
      set
      {
      }
    }


    /// <summary>
    /// Gets/sets the card device
    /// </summary>
    public override string DevicePath
    {
      get
      {
        return _tunerDevice.DevicePath;
      }
    }

    #endregion

    #region tuning & recording
    /// <summary>
    /// tune the card to the channel specified by IChannel
    /// </summary>
    /// <param name="channel">channel to tune</param>
    /// <returns></returns>
    public bool TuneScan(IChannel channel)
    {
      Log.Log.WriteFile("ss2:TuneScan({0})", channel);
      bool result = Tune(channel);
      RunGraph();
      return result;
    }
    /// <summary>
    /// Tunes the specified channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>true if succeeded else false</returns>
    public bool Tune(IChannel channel)
    {
      int frequency = 0;
      int symbolRate = 0;
      int modulation = (int)eModulationTAG.QAM_64;
      int bandWidth = 0;
      int lnbFrequency = 10600000;
      bool hiBand = true;
      LNBSelectionType lnbSelection = LNBSelectionType.Lnb0;
      int lnbKhzTone = 22;
      int fec = (int)FecType.Fec_Auto;
      int polarity = 0;
      SS2DisEqcType disType = SS2DisEqcType.None;
      int switchFreq = 0;
      int pmtPid = 0;

      Log.Log.WriteFile("ss2:Tune({0})", channel);
      if (_epgGrabbing)
      {
        _epgGrabbing = false;
        if (_epgGrabberCallback != null && _epgGrabbing)
        {
          _epgGrabberCallback.OnEpgCancelled();
        }
      }
      switch (_cardType)
      {
        case CardType.DvbS:
          DVBSChannel dvbsChannel = channel as DVBSChannel;
          if (dvbsChannel == null)
          {
            Log.Log.Error("Channel is not a DVBS channel!!! {0}", channel.GetType().ToString());
            return false;
          }
          DVBSChannel oldChannels = _currentChannel as DVBSChannel;
          if (_currentChannel != null)
          {
            if (oldChannels.Equals(channel))
            {
              Log.Log.WriteFile("ss2:already tuned on this channel");
              return true;
            }
          }
          frequency = (int)dvbsChannel.Frequency;
          symbolRate = dvbsChannel.SymbolRate;

          switch (dvbsChannel.BandType)
          {
            case BandType.Universal:
              if (dvbsChannel.Frequency >= 11700000)
              {
                lnbFrequency = 10600000;
                hiBand = true;
              }
              else
              {
                lnbFrequency = 9750000;
                hiBand = false;
              }
              if (lnbFrequency >= frequency)
              {
                Log.Log.Error("ss2:  Error: LNB Frequency must be less than Transponder frequency");
              }
              break;
            case BandType.Circular:
              hiBand = false;
              lnbFrequency = 11250000;
              break;
            case BandType.Linear:
              hiBand = false;
              lnbFrequency = 10750000;
              break;
            case BandType.CBand:
              hiBand = false;
              lnbFrequency = 5150000;
              break;
          }

          //0=horizontal or left, 1=vertical or right
          polarity = 0;
          if (dvbsChannel.Polarisation == Polarisation.LinearV) polarity = 1;
          if (dvbsChannel.Polarisation == Polarisation.CircularR) polarity = 1;
          Log.Log.WriteFile("ss2:  Polarity:{0} {1}", dvbsChannel.Polarisation, polarity);

          lnbSelection = LNBSelectionType.Lnb0;
          if (dvbsChannel.BandType == BandType.Universal)
          {
            //only set the LNB (22,33,44) Khz tone when we use ku-band and are in hi-band
            switch (lnbKhzTone)
            {
              case 0:
                lnbSelection = LNBSelectionType.Lnb0;
                break;
              case 22:
                lnbSelection = LNBSelectionType.Lnb22kHz;
                break;
              case 33:
                lnbSelection = LNBSelectionType.Lnb33kHz;
                break;
              case 44:
                lnbSelection = LNBSelectionType.Lnb44kHz;
                break;
            }
            if (hiBand == false)
            {
              lnbSelection = LNBSelectionType.Lnb0;
            }
          }
          switch (dvbsChannel.DisEqc)
          {
            case DisEqcType.None: // none
              disType = SS2DisEqcType.None;
              break;
            case DisEqcType.SimpleA: // Simple A
              disType = SS2DisEqcType.Simple_A;
              break;
            case DisEqcType.SimpleB: // Simple B
              disType = SS2DisEqcType.Simple_B;
              break;
            case DisEqcType.Level1AA: // Level 1 A/A
              disType = SS2DisEqcType.Level_1_A_A;
              break;
            case DisEqcType.Level1BA: // Level 1 B/A
              disType = SS2DisEqcType.Level_1_B_A;
              break;
            case DisEqcType.Level1AB: // Level 1 A/B
              disType = SS2DisEqcType.Level_1_A_B;
              break;
            case DisEqcType.Level1BB: // Level 1 B/B
              disType = SS2DisEqcType.Level_1_B_B;
              break;
          }
          switchFreq = lnbFrequency / 1000;//in MHz
          pmtPid = dvbsChannel.PmtPid;
          break;
        case CardType.DvbT:
          DVBTChannel dvbtChannel = channel as DVBTChannel;
          if (dvbtChannel == null)
          {
            Log.Log.Error("Channel is not a DVBT channel!!! {0}", channel.GetType().ToString());
            return false;
          }
          DVBTChannel oldChannelt = _currentChannel as DVBTChannel;
          if (_currentChannel != null)
          {
            if (oldChannelt.Equals(channel))
            {
              Log.Log.WriteFile("ss2:already tuned on this channel");
              return true;
            }
          }
          frequency = (int)dvbtChannel.Frequency;
          bandWidth = dvbtChannel.BandWidth;
          pmtPid = dvbtChannel.PmtPid;
          break;
        case CardType.DvbC:
          DVBCChannel dvbcChannel = channel as DVBCChannel;
          if (dvbcChannel == null)
          {
            Log.Log.Error("Channel is not a DVBC channel!!! {0}", channel.GetType().ToString());
            return false;
          }
          DVBCChannel oldChannelc = _currentChannel as DVBCChannel;
          if (_currentChannel != null)
          {
            if (oldChannelc.Equals(channel))
            {
              Log.Log.WriteFile("ss2:already tuned on this channel");
              return true;
            }
          }
          frequency = (int)dvbcChannel.Frequency;
          symbolRate = dvbcChannel.SymbolRate;
          switch (dvbcChannel.ModulationType)
          {
            case ModulationType.Mod16Qam:
              modulation = (int)eModulationTAG.QAM_16;
              break;
            case ModulationType.Mod32Qam:
              modulation = (int)eModulationTAG.QAM_32;
              break;
            case ModulationType.Mod64Qam:
              modulation = (int)eModulationTAG.QAM_64;
              break;
            case ModulationType.Mod128Qam:
              modulation = (int)eModulationTAG.QAM_128;
              break;
            case ModulationType.Mod256Qam:
              modulation = (int)eModulationTAG.QAM_256;
              break;
          }
          pmtPid = dvbcChannel.PmtPid;
          break;
        case CardType.Atsc:
          ATSCChannel dvbaChannel = channel as ATSCChannel;
          if (dvbaChannel == null)
          {
            Log.Log.Error("Channel is not a ATSC channel!!! {0}", channel.GetType().ToString());
            return false;
          }
          ATSCChannel oldChannela = _currentChannel as ATSCChannel;
          if (_currentChannel != null)
          {
            if (oldChannela.Equals(channel))
            {
              Log.Log.WriteFile("ss2:already tuned on this channel");
              return true;
            }
          }
          Log.Log.WriteFile("DVBGraphSkyStar2:  ATSC Channel:{0}", dvbaChannel.PhysicalChannel);
          //#DM B2C2 SDK says ATSC is tuned by frequency. Here we work the OTA frequency by channel number#
          int atscfreq = 0;
          if (dvbaChannel.PhysicalChannel <= 6) atscfreq = 45 + (dvbaChannel.PhysicalChannel * 6);
          if (dvbaChannel.PhysicalChannel >= 7 && dvbaChannel.PhysicalChannel <= 13) atscfreq = 177 + ((dvbaChannel.PhysicalChannel - 7) * 6);
          if (dvbaChannel.PhysicalChannel >= 14) atscfreq = 473 + ((dvbaChannel.PhysicalChannel - 14) * 6);
          //#DM changed tuning parameter from physical channel to calculated frequency above.
          frequency = atscfreq;
          Log.Log.WriteFile("ss2:  ATSC Frequency:{0} MHz", frequency);
          pmtPid = dvbaChannel.PmtPid;
          break;
      }
      _currentChannel = channel;
      if (_graphState == GraphState.Idle)
      {
        BuildGraph();
      }
      //from submittunerequest
      if (_graphState == GraphState.TimeShifting)
      {
        if (_filterTsAnalyzer != null)
        {
          ITsTimeShift timeshift = _filterTsAnalyzer as ITsTimeShift;
          if (timeshift != null)
          {
            timeshift.Pause(1);
          }
        }
      }
      Log.Log.WriteFile("dvb:SubmitTuneRequest");
      _startTimeShifting = false;
      _channelInfo = new ChannelInfo();
      _pmtTimer.Enabled = false;
      _hasTeletext = false;
      _currentAudioStream = null;

      //Log.Log.WriteFile("dvb:SubmitTuneRequest");
      if (_interfaceEpgGrabber != null)
      {
        _interfaceEpgGrabber.Reset();
      }
      //from submittunerequest


      if (frequency > 13000)
        frequency /= 1000;
      Log.Log.WriteFile("ss2:  Transponder Frequency:{0} MHz", frequency);
      int hr = _interfaceB2C2TunerCtrl.SetFrequency(frequency);
      if (hr != 0)
      {
        Log.Log.Error("ss2:SetFrequencyKHz() failed:0x{0:X}", hr);
        return false;
      }

      switch (_cardType)
      {
        case CardType.DvbC:
          Log.Log.WriteFile("ss2:  SymbolRate:{0} KS/s", symbolRate);
          hr = _interfaceB2C2TunerCtrl.SetSymbolRate(symbolRate);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetSymbolRate() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  Modulation:{0}", ((eModulationTAG)modulation));
          hr = _interfaceB2C2TunerCtrl.SetModulation(modulation);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetModulation() failed:0x{0:X}", hr);
            return false;
          }
          break;
        case CardType.DvbT:
          Log.Log.WriteFile("ss2:  GuardInterval:auto");
          hr = _interfaceB2C2TunerCtrl.SetGuardInterval((int)GuardIntervalType.Interval_Auto);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetGuardInterval() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  Bandwidth:{0} MHz", bandWidth);
          //hr = _interfaceB2C2TunerCtrl.SetBandwidth((int)dvbtChannel.BandWidth);
          // Set Channel Bandwidth (NOTE: Temporarily use polarity function to avoid having to 
          // change SDK interface for SetBandwidth)
          // from Technisat SDK 03/2006
          hr = _interfaceB2C2TunerCtrl.SetPolarity(bandWidth);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetBandwidth() failed:0x{0:X}", hr);
            return false;
          }
          break;
        case CardType.DvbS:
          Log.Log.WriteFile("ss2:  SymbolRate:{0} KS/s", symbolRate);
          hr = _interfaceB2C2TunerCtrl.SetSymbolRate(symbolRate);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetSymbolRate() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  Fec:{0} {1}", ((FecType)fec), fec);
          hr = _interfaceB2C2TunerCtrl.SetFec(fec);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetFec() failed:0x{0:X}", hr);
            return false;
          }
          hr = _interfaceB2C2TunerCtrl.SetPolarity(polarity);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetPolarity() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  Lnb:{0}", lnbSelection);
          hr = _interfaceB2C2TunerCtrl.SetLnbKHz((int)lnbSelection);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetLnbKHz() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  Diseqc:{0} {1}", disType, disType);
          hr = _interfaceB2C2TunerCtrl.SetDiseqc((int)disType);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetDiseqc() failed:0x{0:X}", hr);
            return false;
          }
          Log.Log.WriteFile("ss2:  LNBFrequency:{0} MHz", switchFreq);
          hr = _interfaceB2C2TunerCtrl.SetLnbFrequency(switchFreq);
          if (hr != 0)
          {
            Log.Log.Error("ss2:SetLnbFrequency() failed:0x{0:X}", hr);
            return false;
          }
          break;
      }


      hr = _interfaceB2C2TunerCtrl.SetTunerStatus();
      _interfaceB2C2TunerCtrl.CheckLock();
      if (((uint)hr) == (uint)0x90010115)
      {
        Log.Log.Error("ss2:could not lock tuner");
      }
      if (hr != 0)
      {
        hr = _interfaceB2C2TunerCtrl.SetTunerStatus();
        if (hr != 0)
          hr = _interfaceB2C2TunerCtrl.SetTunerStatus();
        if (hr != 0)
        {
          Log.Log.Error("ss2:SetTunerStatus failed:0x{0:X}", hr);
          return false;
        }
      }
      _interfaceB2C2TunerCtrl.CheckLock();

      if (_cardType == CardType.DvbS)
      {
        DVBSChannel dvbsChannel = channel as DVBSChannel;
        if (dvbsChannel.SatelliteIndex > 0 && _conditionalAccess.DiSEqCMotor != null)
        {
          _conditionalAccess.DiSEqCMotor.GotoPosition((byte)dvbsChannel.SatelliteIndex);
        }
      }

      //from submittunerequest
      _pmtTimer.Enabled = true;
      _lastSignalUpdate = DateTime.MinValue;
      ArrayList pids = new ArrayList();
      pids.Add((ushort)0x0);//pat
      pids.Add((ushort)0x11);//sdt
      pids.Add((ushort)0x1fff);//padding stream
      if (_currentChannel != null)
      {
        DVBBaseChannel ch = (DVBBaseChannel)_currentChannel;
        if (ch.PmtPid > 0)
        {
          pids.Add((ushort)ch.PmtPid);//sdt
        }
      }
      SendHwPids(pids);

      _pmtVersion = -1;
      _newPMT = false;
      _newCA = false;
      //from submittunerequest
      SetupPmtGrabber(pmtPid);
      Log.Log.WriteFile("ss2:tune done");
      return true;
    }

    /// <summary>
    /// Starts timeshifting. Note card has to be tuned first
    /// </summary>
    /// <param name="fileName">filename used for the timeshiftbuffer</param>
    /// <returns></returns>
    public bool StartTimeShifting(string fileName)
    {
      try
      {

        Log.Log.WriteFile("dvbc:StartTimeShifting()");

        if (!CheckThreadId()) return false;
        if (_graphState == GraphState.TimeShifting)
        {
          return true;
        }

        if (_graphState == GraphState.Idle)
        {
          BuildGraph();
        }

        if (_currentChannel == null)
        {
          Log.Log.Error("dvbc:StartTimeShifting not tuned to a channel");
          throw new TvException("StartTimeShifting not tuned to a channel");
        }

        DVBBaseChannel channel = (DVBBaseChannel)_currentChannel;
        if (channel.NetworkId == -1 || channel.TransportId == -1 || channel.ServiceId == -1)
        {
          Log.Log.Error("dvbc:StartTimeShifting not tuned to a channel but to a transponder");
          throw new TvException("StartTimeShifting not tuned to a channel but to a transponder");
        }

        //RunGraph();
        //Tune(Channel);
        if (_graphState == GraphState.Created)
        {
          SetTimeShiftFileName(fileName);
        }
        _graphState = GraphState.TimeShifting;
        return true;

      }
      catch (Exception ex)
      {
        Log.Log.Write(ex);
        throw ex;
      }
      //Log.Log.WriteFile("dvbc:StartTimeShifting() done");
    }

    /// <summary>
    /// Stops timeshifting
    /// </summary>
    /// <returns></returns>
    public bool StopTimeShifting()
    {
      try
      {
        if (!CheckThreadId()) return false;
        Log.Log.WriteFile("dvbc:StopTimeShifting()");
        if (_graphState != GraphState.TimeShifting)
        {
          return true;
        }
        StopGraph();

        _graphState = GraphState.Created;

      }
      catch (Exception ex)
      {
        Log.Log.Write(ex);
        throw ex;
      }
      return true;
    }

    /// <summary>
    /// Starts recording
    /// </summary>
    /// <param name="recordingType">Recording type (content or reference)</param>
    /// <param name="fileName">filename to which to recording should be saved</param>
    /// <param name="startTime">time the recording should start (0=now)</param>
    /// <returns></returns>
    public bool StartRecording(RecordingType recordingType, string fileName, long startTime)
    {
      try
      {
        if (!CheckThreadId()) return false;
        Log.Log.WriteFile("dvbc:StartRecording to {0}", fileName);

        if (_graphState == GraphState.Recording) return false;

        if (_graphState != GraphState.TimeShifting)
        {
          throw new TvException("Card must be timeshifting before starting recording");
        }
        _graphState = GraphState.Recording;
        StartRecord(fileName, recordingType, ref startTime);

        _recordingFileName = fileName;
        Log.Log.WriteFile("dvbc:Started recording on {0}", startTime);

        return true;
      }
      catch (Exception ex)
      {
        Log.Log.Write(ex);
        throw ex;
      }
    }

    /// <summary>
    /// Stop recording
    /// </summary>
    /// <returns></returns>
    public bool StopRecording()
    {
      try
      {
        if (!CheckThreadId()) return false;
        if (_graphState != GraphState.Recording) return false;
        Log.Log.WriteFile("dvbc:StopRecording");
        _graphState = GraphState.TimeShifting;
        StopRecord();
        return true;
      }
      catch (Exception ex)
      {
        Log.Log.Write(ex);
        throw ex;
      }
    }
    #endregion


    #region quality control
    /// <summary>
    /// Get/Set the quality
    /// </summary>
    /// <value></value>
    public IQuality Quality
    {
      get
      {
        return null;
      }
      set
      {
      }
    }
    /// <summary>
    /// Property which returns true if card supports quality control
    /// </summary>
    /// <value></value>
    public bool SupportsQualityControl
    {
      get
      {
        return false;
      }
    }
    #endregion

    #region epg & scanning
    /// <summary>
    /// returns the ITVScanning interface used for scanning channels
    /// </summary>
    public ITVScanning ScanningInterface
    {
      get
      {
        if (!CheckThreadId()) return null;
        return new DVBSS2canning(this);
      }
    }
    #endregion

    /// <summary>
    /// Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </returns>
    public override string ToString()
    {
      return _tunerDevice.Name;
    }

    /// <summary>
    /// Method to check if card can tune to the channel specified
    /// </summary>
    /// <returns>true if card can tune to the channel otherwise false</returns>
    public bool CanTune(IChannel channel)
    {
      if (_cardType == CardType.DvbS)
      {
        if ((channel as DVBSChannel) == null) return false;
        return true;
      }
      if (_cardType == CardType.DvbT)
      {
        if ((channel as DVBTChannel) == null) return false;
        return true;
      }
      if (_cardType == CardType.DvbC)
      {
        if ((channel as DVBCChannel) == null) return false;
        return true;
      }
      return false;
    }


    #region SS2 specific
    /// <summary>
    /// Builds the graph.
    /// </summary>
    void BuildGraph()
    {
      Log.Log.WriteFile("ss2: build graph");
      if (_graphState != GraphState.Idle)
      {
        Log.Log.Error("ss2: Graph already build");
        throw new TvException("Graph already build");
      }
      DevicesInUse.Instance.Add(_tunerDevice);
      _managedThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
      _graphBuilder = (IFilterGraph2)new FilterGraph();
      _rotEntry = new DsROTEntry(_graphBuilder);
      _capBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
      _capBuilder.SetFiltergraph(_graphBuilder);

      //=========================================================================================================
      // add the skystar 2 specific filters
      //=========================================================================================================
      Log.Log.WriteFile("ss2:CreateGraph() create B2C2 adapter");
      _filterB2C2Adapter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(DVBSkyStar2Helper.CLSID_B2C2Adapter, false));
      if (_filterB2C2Adapter == null)
      {
        Log.Log.Error("ss2:creategraph() _filterB2C2Adapter not found");
        return;
      }
      Log.Log.WriteFile("ss2:creategraph() add filters to graph");
      int hr = _graphBuilder.AddFilter(_filterB2C2Adapter, "B2C2-Source");
      if (hr != 0)
      {
        Log.Log.Error("ss2: FAILED to add B2C2-Adapter");
        return;
      }
      // get interfaces
      _interfaceB2C2DataCtrl = _filterB2C2Adapter as DVBSkyStar2Helper.IB2C2MPEG2DataCtrl3;
      if (_interfaceB2C2DataCtrl == null)
      {
        Log.Log.Error("ss2: cannot get IB2C2MPEG2DataCtrl3");
        return;
      }
      _interfaceB2C2TunerCtrl = _filterB2C2Adapter as DVBSkyStar2Helper.IB2C2MPEG2TunerCtrl2;
      if (_interfaceB2C2TunerCtrl == null)
      {
        Log.Log.Error("ss2: cannot get IB2C2MPEG2TunerCtrl3");
        return;
      }
      //=========================================================================================================
      // initialize skystar 2 tuner
      //=========================================================================================================
      Log.Log.WriteFile("ss2: Initialize Tuner()");
      hr = _interfaceB2C2TunerCtrl.Initialize();
      if (hr != 0)
      {
        Log.Log.Error("ss2: Tuner initialize failed:0x{0:X}", hr);
        //return;
      }
      // call checklock once, the return value dont matter

      hr = _interfaceB2C2TunerCtrl.CheckLock();

      AddMpeg2DemuxerToGraph();
      ConnectInfTeeToSS2();
      ConnectMpeg2DemuxToInfTee();

      AddTsAnalyzerToGraph();

      SendHWPids(new ArrayList());
      _graphState = GraphState.Created;
    }

    /// <summary>
    /// Connects the SS2 filter to the infTee
    /// </summary>
    void ConnectInfTeeToSS2()
    {
      Log.Log.WriteFile("ss2:ConnectMainTee()");
      int hr = 0;
      IPin pinOut = DsFindPin.ByDirection(_filterB2C2Adapter, PinDirection.Output, 2);
      IPin pinIn = DsFindPin.ByDirection(_infTeeMain, PinDirection.Input, 0);
      if (pinOut == null)
      {
        Log.Log.Error("ss2:unable to find pin 2 of b2c2adapter");
        throw new TvException("unable to find pin 2 of b2c2adapter");
      }
      if (pinIn == null)
      {
        Log.Log.Error("ss2:unable to find pin 0 of _infTeeMain");
        throw new TvException("unable to find pin 0 of _infTeeMain");
      }

      hr = _graphBuilder.Connect(pinOut, pinIn);
      Release.ComObject("b2c2pin2", pinOut);
      Release.ComObject("mpeg2demux pinin", pinIn);
      if (hr != 0)
      {
        Log.Log.Error("ss2:unable to connect b2c2->_infTeeMain");
        throw new TvException("unable to connect b2c2->_infTeeMain");
      }
    }

    /// <summary>
    /// Sends the HW pids.
    /// </summary>
    /// <param name="pids">The pids.</param>
    public void SendHWPids(ArrayList pids)
    {
      const int PID_CAPTURE_ALL_INCLUDING_NULLS = 0x2000;//Enables reception of all PIDs in the transport stream including the NULL PID
      // const int PID_CAPTURE_ALL_EXCLUDING_NULLS = 0x2001;//Enables reception of all PIDs in the transport stream excluding the NULL PID.

      if (!DeleteAllPIDs(_interfaceB2C2DataCtrl, 0))
      {
        Log.Log.Error("ss2:DeleteAllPIDs() failed pid:0x2000");
      }
      if (pids.Count == 0 || true)
      {
        Log.Log.WriteFile("ss2:hw pids:all");
        int added = SetPidToPin(_interfaceB2C2DataCtrl, 0, PID_CAPTURE_ALL_INCLUDING_NULLS);
        if (added != 1)
        {
          Log.Log.Error("ss2:SetPidToPin() failed pid:0x2000");
        }
      }
      else
      {
        int maxPids;
        _interfaceB2C2DataCtrl.GetMaxPIDCount(out maxPids);
        for (int i = 0; i < pids.Count && i < maxPids; ++i)
        {
          ushort pid = (ushort)pids[i];
          Log.Log.WriteFile("ss2:hw pids:0x{0:X}", pid);
          SetPidToPin(_interfaceB2C2DataCtrl, 0, pid);
        }
      }
    }


    /// <summary>
    /// updates the signal quality/level and tuner locked statusses
    /// </summary>
    protected override void UpdateSignalQuality()
    {
      TimeSpan ts = DateTime.Now - _lastSignalUpdate;
      if (ts.TotalMilliseconds < 5000) return;
      if (_graphRunning == false)
      {
        _tunerLocked = false;
        _signalLevel = 0;
        _signalPresent = false;
        _signalQuality = 0;
        return;
      }
      if (Channel == null)
      {
        _tunerLocked = false;
        _signalLevel = 0;
        _signalPresent = false;
        _signalQuality = 0;
        return;
      }
      if (_graphState == GraphState.Idle || _interfaceB2C2TunerCtrl == null)
      {
        _tunerLocked = false;
        _signalQuality = 0;
        _signalLevel = 0;
        return;
      }

      int level, quality;
      _tunerLocked = (_interfaceB2C2TunerCtrl.CheckLock() == 0);
      GetSNR(_interfaceB2C2TunerCtrl, out level, out quality);
      if (level < 0) level = 0;
      if (level > 100) level = 100;
      if (quality < 0) quality = 0;
      if (quality > 100) quality = 100;
      _signalQuality = quality;
      _signalLevel = level;
      _lastSignalUpdate = DateTime.Now;
    }

    private void GetTunerCapabilities()
    {
      int hr;
      Log.Log.WriteFile("ss2: GetTunerCapabilities");
      _graphBuilder = (IFilterGraph2)new FilterGraph();
      _rotEntry = new DsROTEntry(_graphBuilder);
      _capBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
      _capBuilder.SetFiltergraph(_graphBuilder);

      //=========================================================================================================
      // add the skystar 2 specific filters
      //=========================================================================================================
      Log.Log.WriteFile("ss2:GetTunerCapabilities() create B2C2 adapter");
      _filterB2C2Adapter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(DVBSkyStar2Helper.CLSID_B2C2Adapter, false));
      if (_filterB2C2Adapter == null)
      {
        Log.Log.Error("ss2:GetTunerCapabilities() _filterB2C2Adapter not found");
        return;
      }
      _interfaceB2C2TunerCtrl = _filterB2C2Adapter as DVBSkyStar2Helper.IB2C2MPEG2TunerCtrl2;
      if (_interfaceB2C2TunerCtrl == null)
      {
        Log.Log.Error("ss2: cannot get IB2C2MPEG2TunerCtrl3");
        return;
      }

      //=========================================================================================================
      // initialize skystar 2 tuner
      //=========================================================================================================
      /* Not necessary for query-only application
       
      Log.Log.WriteFile("ss2: Initialize Tuner()");
      hr = _interfaceB2C2TunerCtrl.Initialize();
      if (hr != 0)
      {
        Log.Log.Error("ss2: Tuner initialize failed:0x{0:X}", hr);
        //return;
      }
      */
      //=========================================================================================================
      // Get tuner type (DVBS, DVBC, DVBT, ATSC)
      //=========================================================================================================
      tTunerCapabilities tc;
      int lTunerCapSize = Marshal.SizeOf(typeof(tTunerCapabilities));

      IntPtr ptCaps = Marshal.AllocHGlobal(lTunerCapSize);

      hr = _interfaceB2C2TunerCtrl.GetTunerCapabilities(ptCaps, ref lTunerCapSize);
      if (hr != 0)
      {
        Log.Log.Error("ss2: Tuner Type failed:0x{0:X}", hr);
        return;
      }
      tc = (tTunerCapabilities)Marshal.PtrToStructure(ptCaps, typeof(tTunerCapabilities));

      switch (tc.eModulation)
      {
        case TunerType.ttSat:
          Log.Log.WriteFile("ss2: Card type = DVBS");
          _cardType = CardType.DvbS;
          break;
        case TunerType.ttCable:
          Log.Log.WriteFile("ss2: Card type = DVBC");
          _cardType = CardType.DvbC;
          break;
        case TunerType.ttTerrestrial:
          Log.Log.WriteFile("ss2: Card type = DVBT");
          _cardType = CardType.DvbT;
          break;
        case TunerType.ttATSC:
          Log.Log.WriteFile("ss2: Card type = ATSC");
          _cardType = CardType.Atsc;
          break;
        case TunerType.ttUnknown:
          Log.Log.WriteFile("ss2: Card type = unknown?");
          _cardType = CardType.DvbS;
          break;
      }
      Marshal.FreeHGlobal(ptCaps);

      // Release all used object
      if (_filterB2C2Adapter != null)
      {
        Release.ComObject("tuner filter", _filterB2C2Adapter); _filterB2C2Adapter = null;
      }
      _rotEntry.Dispose();
      if (_capBuilder != null)
      {
        Release.ComObject("capture builder", _capBuilder); _capBuilder = null;
      }
      if (_graphBuilder != null)
      {
        Release.ComObject("graph builder", _graphBuilder); _graphBuilder = null;
      }
    }



    /// <summary>
    /// Disposes this instance.
    /// </summary>
    public override void Dispose()
    {
      if (_graphBuilder == null) return;
      if (!CheckThreadId()) return;


      if (_epgGrabbing)
      {
        _epgGrabbing = false;
        if (_epgGrabberCallback != null && _epgGrabbing)
        {
          _epgGrabberCallback.OnEpgCancelled();
        }
      }
      Log.Log.WriteFile("ss2:Decompose");
      int hr = 0;
      _graphRunning = false;

      // Decompose the graph
      //hr = (_graphBuilder as IMediaControl).StopWhenReady();
      hr = (_graphBuilder as IMediaControl).Stop();

      FilterGraphTools.RemoveAllFilters(_graphBuilder);

      _interfaceChannelScan = null;
      _interfaceEpgGrabber = null;
      _interfaceB2C2DataCtrl = null;
      _interfaceB2C2TunerCtrl = null; ;

      if (_filterMpeg2DemuxTif != null)
      {
        Release.ComObject("MPEG2 demux filter", _filterMpeg2DemuxTif); _filterMpeg2DemuxTif = null;
      }
      if (_filterB2C2Adapter != null)
      {
        Release.ComObject("tuner filter", _filterB2C2Adapter); _filterB2C2Adapter = null;
      }
      if (_filterTIF != null)
      {
        Release.ComObject("TIF filter", _filterTIF); _filterTIF = null;
      }
      //if (_filterSectionsAndTables != null)
      //{
      //  Release.ComObject("secions&tables filter", _filterSectionsAndTables); _filterSectionsAndTables = null;
      //}


      if (_filterTsAnalyzer != null)
      {
        Release.ComObject("_filterMpTsAnalyzer", _filterTsAnalyzer); _filterTsAnalyzer = null;
      }
      if (_infTeeMain != null)
      {
        Release.ComObject("_infTeeMain", _infTeeMain); _infTeeMain = null;
      }

      _rotEntry.Dispose();
      if (_capBuilder != null)
      {
        Release.ComObject("capture builder", _capBuilder); _capBuilder = null;
      }
      if (_graphBuilder != null)
      {
        Release.ComObject("graph builder", _graphBuilder); _graphBuilder = null;
      }

      if (_tunerDevice != null)
      {
        DevicesInUse.Instance.Remove(_tunerDevice);
        _tunerDevice = null;
      }

      if (_teletextDecoder != null)
      {
        _teletextDecoder.ClearBuffer();
        _teletextDecoder = null;
      }
    }

    #endregion
  }
}

