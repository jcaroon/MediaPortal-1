#region Copyright (C) 2005-2009 Team MediaPortal

/* 
 *	Copyright (C) 2005-2009 Team MediaPortal
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

#endregion

using System;
using MediaPortal.GUI.Library;

namespace MediaPortal.Dialogs
{
  /// <summary>
  /// 
  /// </summary>
  public class GUIDialogMenuBottomRight : GUIDialogMenu
  {
    [SkinControl(6)] protected GUILabelControl lblHeading3 = null;

    private int timeoutInSeconds = -1;
    private bool timedOut = false;
    private DateTime startTime = DateTime.Now;

    public GUIDialogMenuBottomRight()
    {
      GetID = (int) Window.WINDOW_DIALOG_MENU_BOTTOM_RIGHT;
    }

    public override bool Init()
    {
      return Load(GUIGraphicsContext.Skin + @"\DialogMenuBottomRight.xml");
    }

    #region Base Dialog Members

    public override void DoModal(int dwParentId)
    {
      startTime = DateTime.Now;
      base.DoModal(dwParentId);
    }

    public override bool ProcessDoModal()
    {
      base.ProcessDoModal();
      TimeSpan ts = DateTime.Now - startTime;
      if (timeoutInSeconds > 0 && ts.TotalSeconds > timeoutInSeconds)
      {
        selectedItemIndex = -1;
        selectedId = -1;
        timedOut = true;
        return false;
      }
      return true;
    }

    #endregion

    public override void Reset()
    {
      base.Reset();
      SetHeadingRow2(string.Empty);
      SetHeadingRow3(string.Empty);

      timedOut = true;
      timeoutInSeconds = -1;
    }

    public override void SetHeading(string strLine)
    {
      LoadSkin();
      AllocResources();
      InitControls();

      lblHeading.Label = strLine;
    }

    public void SetHeadingRow2(string strLine)
    {
      LoadSkin();
      AllocResources();
      InitControls();

      if (lblHeading2 != null)
      {
        lblHeading2.Label = strLine;
      }
    }

    public void SetHeadingRow3(string strLine)
    {
      LoadSkin();
      AllocResources();
      InitControls();

      if (lblHeading3 != null)
      {
        lblHeading3.Label = strLine;
      }
    }

    public void SetHeadingRow2(int iString)
    {
      SetHeadingRow2(GUILocalizeStrings.Get(iString));
      selectedItemIndex = -1;
      listItems.Clear();
    }

    public void SetHeadingRow3(int iString)
    {
      SetHeadingRow3(GUILocalizeStrings.Get(iString));
      selectedItemIndex = -1;
      listItems.Clear();
    }

    public int TimeOut
    {
      get { return timeoutInSeconds; }
      set { timeoutInSeconds = value; }
    }

    public bool TimedOut
    {
      get { return timedOut; }
    }
  }
}