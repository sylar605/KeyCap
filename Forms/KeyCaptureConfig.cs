////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2019 Tim Stair
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using KeyCap.Format;
using KeyCap.Settings;
using KeyCap.Wrapper;
using Support.IO;
using Support.UI;

namespace KeyCap.Forms
{
    public partial class KeyCaptureConfig
    {
        private readonly List<string> m_listRecentFiles = new List<string>();
        private FormWindowState m_ePrevWindowState = FormWindowState.Normal;
        private readonly IniManager m_zIniManager = new IniManager(Application.ProductName, false, true, false);
        private bool m_bRun = true;

        /// <summary>
        /// Text to display on the button to start/stop capturing
        /// </summary>
        private enum ActionText
        {
            Start,
            Stop
        }

        /// <summary>
        /// Used to determine where to get the flags from
        /// </summary>
        private enum FlagsFromEnum
        {
            Input,
            Output
        }

        private KeyCaptureConfig() { }

        /// <summary>
        /// Constructs a new dialog
        /// </summary>
        /// <param name="args">The command line arguments (if a file is specified it is loaded)</param>
        public KeyCaptureConfig(IReadOnlyList<string> args)
        {
            InitializeComponent();
            m_sBaseTitle = Application.ProductName + " Configuration " + Application.ProductVersion;
            m_sFileOpenFilter = Application.ProductName + " Config files (*.kfg)|*.kfg|All files (*.*)|*.*";
            Text = m_sBaseTitle;
            
            // load the input file
            if (1 == args.Count)
            {
                // existence already checked in program.cs
                InitOpen(args[0]);
            }
        }

        #region Form Events

        private void KeyCaptureConfig_Load(object sender, EventArgs e)
        {
            // must be in the load event to avoid the location being incorrect
            IniManager.RestoreState(this, m_zIniManager.GetValue(Name));

            // setup the various mouse output options
            comboBoxMouseOut.Items.Add("No Action");
            foreach (IODefinition.MouseButton sName in Enum.GetValues(typeof(IODefinition.MouseButton)))
            {
                comboBoxMouseOut.Items.Add(sName);
            }
            comboBoxMouseOut.SelectedIndex = 0;

            // set the notification icon accordingly
            notifyIcon.Icon = Resources.KeyCapIdle;
            Icon = notifyIcon.Icon;

            // populate the previously loaded configurations
            var arrayFiles = m_zIniManager.GetValue(IniSettings.PreviousFiles).Split(new char[] { KeyCapConstants.CharFileSplit }, StringSplitOptions.RemoveEmptyEntries);
            if (0 < arrayFiles.Length)
            {
                foreach (var sFile in arrayFiles)
                {
                    m_listRecentFiles.Add(sFile);
                }
            }

            // initialize capture from command line specified file
            if (0 != m_sLoadedFile.Length)
            {
                btnStart_Click(sender, new EventArgs());
                new Thread(MinimizeThread) { Name = "MinimizeThread" }.Start();
            }
        }

        private void exitMainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_bRun = false;
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = m_ePrevWindowState;
            }
            KeyCaptureLib.Shutdown();
            m_zIniManager.SetValue(Name, IniManager.GetFormSettings(this));
            Close();
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var zCancelEvent = new CancelEventArgs();
            SaveOnClose(zCancelEvent);
            if (zCancelEvent.Cancel)
            {
                return;
            }
            listViewKeys.Items.Clear();
            InitNew();
        }

        private void KeyCaptureConfig_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_bRun && !panelKeySetup.Enabled)
            {
                switch (e.CloseReason)
                {
                    case CloseReason.TaskManagerClosing:
                    case CloseReason.WindowsShutDown:
                        SaveOnClose(e);
                        break;
                    default:
                        e.Cancel = true;
                        Hide();
                        break;
                }
            }
            else
            {
                SaveOnClose(e);
                if (e.Cancel)
                {
                    return;
                }
                var zBuilder = new StringBuilder();
                var dictionaryFilenames = new Dictionary<string, object>();
                foreach (var sFile in m_listRecentFiles)
                {
                    var sLowerFile = sFile.ToLower();
                    if (dictionaryFilenames.ContainsKey(sLowerFile))
                        continue;
                    dictionaryFilenames.Add(sLowerFile, null);
                    zBuilder.Append(sFile + KeyCapConstants.CharFileSplit);
                }
                m_zIniManager.SetValue(IniSettings.PreviousFiles, zBuilder.ToString());
                m_zIniManager.FlushIniSettings();
            }
        }

        private void KeyCaptureConfig_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
            {
                m_ePrevWindowState = WindowState;
            }
        }

        #endregion

        #region Text Capture Handling

        private void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            var zDisplay = (TextBox)sender;
            //            Console.Out.WriteLine("Key Input: {0} 0x{1}".FormatString(e.KeyCode, e.KeyCode.ToString("x")));
#warning not so sure how non-english keyboards are going to handle the limitation/cast to a byte here. This should probably be an int32 (this could be one of many problems...)
            var zIODef = new IODefinition(0x00, (byte)e.KeyCode, e);
            zDisplay.Text = zIODef.GetDescription();
            e.Handled = true;
            zDisplay.Tag = zIODef;
        }

        private void txtKey_Enter(object sender, EventArgs e)
        {
            ((TextBox)sender).BackColor = Color.LightGoldenrodYellow;
        }

        private void txtKey_Leave(object sender, EventArgs e)
        {
            ((TextBox)sender).BackColor = SystemColors.Control;
        }

        #endregion

        #region AbstractDirtyForm overrides

        protected override bool SaveFormData(string sFileName)
        {
            var zWriter = new FileStream(sFileName, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (ListViewItem zItem in listViewKeys.Items)
            {
                var zIOPair = (IOPairDefinition)zItem.Tag;
                var arrayKeyDefinition = zIOPair.ToArray();
                zWriter.Write(arrayKeyDefinition, 0, arrayKeyDefinition.Length);
            }
            zWriter.Close();
            return true;
        }

        protected override bool OpenFormData(string sFileName)
        {
            txtKeyIn.Text = string.Empty;
            txtKeyOut.Text = string.Empty;
            FileStream zReader = null;
            try
            {
                zReader = new FileStream(sFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                AddListViewItems(zReader);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            finally
            {
                zReader?.Close();
            }
            UpdateProjectsList(sFileName);
            return true;
        }

        #endregion

        #region Menu Events

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitOpen();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitSave(false);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitSave(true);
        }

        private void restoreConfigurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = m_ePrevWindowState;
            Win32.ShowTopmost(Handle);
        }

        private void previousConfigurationsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            previousConfigurationsToolStripMenuItem.DropDownItems.Clear();
            foreach (var sFile in m_listRecentFiles)
            {
                previousConfigurationsToolStripMenuItem.DropDownItems.Add(sFile, null, recentConfiguration_Click);
            }
        }

        private void recentConfiguration_Click(object sender, EventArgs e)
        {
            var zItem = (ToolStripItem)sender;
            InitOpen(zItem.Text);
        }

        #endregion

        #region Control Events

        private void listViewKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnAppend.Enabled = (1 == listViewKeys.SelectedIndices.Count);
            btnAppendExtra.Enabled = (1 == listViewKeys.SelectedIndices.Count);
            btnRemove.Enabled = (0 < listViewKeys.SelectedIndices.Count);
        }

        private void listViewKeys_Resize(object sender, EventArgs e)
        {
            ListViewAssist.ResizeColumnHeaders(listViewKeys);
        }

        private void comboBoxSpecialOut_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (0 != comboBoxMouseOut.SelectedIndex) // the first entry does nothing
            {
                var zIODefinition = new IODefinition(
                    (byte)IODefinition.IOFlags.MouseOut,
                    (byte)(IODefinition.MouseButton)comboBoxMouseOut.SelectedItem);
                var zDisplay = txtKeyOut;
                zDisplay.Text = zIODefinition.GetDescription();
                zDisplay.Tag = zIODefinition;
            }
        }

        private void numericUpDownDelay_ValueChanged(object sender, EventArgs e)
        {
            var zIODefinition = new IODefinition(
                (byte)IODefinition.IOFlags.Delay,
                (byte)numericUpDownDelay.Value);
            var zDisplay = txtKeyOut;
            zDisplay.Text = zIODefinition.GetDescription();
            zDisplay.Tag = zIODefinition;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var zInput = (IODefinition)txtKeyIn.Tag;
            var zOutput = getCurrentOutputDefinition();

            if (null == zInput || null == zOutput)
            {
                MessageBox.Show(this, "Please specify both an input and output key.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            zInput.Flags = GetFlags(zInput, FlagsFromEnum.Input);

            var zPairDef = new IOPairDefinition(zInput, zOutput);

            // TODO: is it worth keeping a hashset of these to cut the time from o(n) to o(1)?
            // verify this is not already defined
            foreach (ListViewItem zListItem in listViewKeys.Items)
            {
                var zKeyOldDef = (IOPairDefinition)zListItem.Tag;
                if (zPairDef.GetHashCode() != zKeyOldDef.GetHashCode())
                {
                    continue;
                }

                MessageBox.Show(this, "Duplicate inputs are not allowed!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            var zItem = new ListViewItem(new string[] { 
                zPairDef.GetInputString(), 
                zPairDef.GetOutputString() });
            zItem.Tag = zPairDef;
            listViewKeys.Items.Add(zItem);
            listViewKeys.SelectedItems.Clear();
            zItem.Selected = true;
            MarkDirty();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (0 == listViewKeys.SelectedItems.Count)
            {
                return;
            }

            foreach (ListViewItem zItem in listViewKeys.SelectedItems)
            {
                listViewKeys.Items.Remove(zItem);
            }
            MarkDirty();
        }

        private void btnAppend_Click(object sender, EventArgs e)
        {
            if (1 != listViewKeys.SelectedItems.Count)
            {
                return;
            }

            var zItem = listViewKeys.SelectedItems[0];
            var zPairDef = (IOPairDefinition)zItem.Tag;
            var zOutDef = getCurrentOutputDefinition();
            var bSuccess = zPairDef.AddOutputDefinition(zOutDef);
            if (bSuccess)
            {
                zItem.SubItems[1].Text = zPairDef.GetOutputString();
                MarkDirty();
                txtKeyOut.Focus(); // restore focus to the output
            }
            else
            {
                MessageBox.Show(this, "Failed to append item. The maximum number of outputs allowed is 255.", "Append Error!",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private IODefinition getCurrentOutputDefinition()
        {
            var zOutput = (IODefinition)txtKeyOut.Tag;
            if (zOutput == null)
            {
                return null;
            }

            if (checkOutputNone.Checked) // if output is set to none change zOutput keyarg
            {
                zOutput = new IODefinition((byte)IODefinition.IOFlags.DoNothing, 0x00);
            }
            else
            {
                zOutput.Flags = GetFlags(zOutput, FlagsFromEnum.Output);
            }
            return zOutput;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (0 == listViewKeys.Items.Count)
            {
                return; // no keys, no point!
            }
            if(btnStart.Text.Equals(ActionText.Stop.ToString()))
            {
                KeyCaptureLib.Shutdown();
                ConfigureControls(false);
            }
            else if(btnStart.Text.Equals(ActionText.Start.ToString()))
            {
                InitSave(false);
                if (!Dirty)
                {
                    ConfigureControls(true);
                    var eReturn = KeyCaptureLib.LoadFileAndCapture(m_sLoadedFile);
                    switch (eReturn)
                    {
                        case CaptureMessage.HookCreationSuccess:
                            break;
                        case CaptureMessage.HookCreationFailure:
                        case CaptureMessage.InputBad:
                        case CaptureMessage.InputMissing:
                        case CaptureMessage.InputZero:
                        default:
                            Console.WriteLine("Error: " + eReturn);
                            ConfigureControls(false);
                            break;
                    }
                }
            }
        }

        #endregion

        #region Support Methods

        /// <summary>
        /// Updates the recent loaded file list
        /// </summary>
        /// <param name="sFileName">The most recently loaded file</param>
        private void UpdateProjectsList(string sFileName)
        {
            m_listRecentFiles.Remove(sFileName);
            m_listRecentFiles.Insert(0, sFileName);
            while (KeyCapConstants.MaxRecentProjects < m_listRecentFiles.Count)
            {
                m_listRecentFiles.RemoveAt(KeyCapConstants.MaxRecentProjects);
            }
        }

        /// <summary>
        /// Gets the flags byte based on the definition and the type of input/output
        /// </summary>
        /// <param name="zIODef">the io definition</param>
        /// <param name="eFlag">the type of io</param>
        /// <returns>New flags value based on the settings of the ui (any prior flags are lost)</returns>
        private byte GetFlags(IODefinition zIODef, FlagsFromEnum eFlag)
        {
            // get the flags from the check boxes (always, both mouse and keyboard support them in some fashion)
            var bAlt = false;
            var bControl = false;
            var bShift = false;
            var bNone = false;
            var bToggle = false;
            switch (eFlag)
            {
                case FlagsFromEnum.Input:
                    bAlt = checkInputAlt.Checked;
                    bControl = checkInputControl.Checked;
                    bShift = checkInputShift.Checked;
                    break;
                case FlagsFromEnum.Output:
                    bAlt = checkOutputAlt.Checked;
                    bControl = checkOutputControl.Checked;
                    bShift = checkOutputShift.Checked;
                    bNone = checkOutputNone.Checked;
                    bToggle = checkOutputToggle.Checked;
                    break;
            }

            byte byFlags = 0;
            byFlags = UpdateFlag(byFlags, bAlt, (byte) IODefinition.IOFlags.Alt);
            byFlags = UpdateFlag(byFlags, bControl, (byte)IODefinition.IOFlags.Control);
            byFlags = UpdateFlag(byFlags, bShift, (byte)IODefinition.IOFlags.Shift);

            byFlags = UpdateFlag(byFlags, bNone, (byte)IODefinition.IOFlags.DoNothing);

            byFlags = UpdateFlag(byFlags, bToggle, (byte)IODefinition.IOFlags.Toggle);

            byFlags = UpdateFlag(byFlags, zIODef.IsFlaggedAs(IODefinition.IOFlags.MouseOut), (byte)IODefinition.IOFlags.MouseOut);
            byFlags = UpdateFlag(byFlags, zIODef.IsFlaggedAs(IODefinition.IOFlags.Delay), (byte)IODefinition.IOFlags.Delay);
            return byFlags;
        }

        private byte UpdateFlag(byte byFlag, bool bFlagSetting, byte byFlagBit)
        {
            return (byte)(byFlag | (byte)(bFlagSetting ? byFlagBit : (byte)0));
        }

        /// <summary>
        /// Adds a new list view item representation of the key in/out
        /// </summary>
        /// <param name="zStream">The stream to read the key definitions from</param>
        /// <returns></returns>
        private void AddListViewItems(FileStream zStream)
        {
            listViewKeys.Items.Clear();
            while (zStream.Position < zStream.Length )
            {
                var zKeyDef = new IOPairDefinition(zStream);
                var zItem = new ListViewItem(new string[] {
                    zKeyDef.GetInputString(),
                    zKeyDef.GetOutputString()
                    });
                zItem.Tag = zKeyDef;
                listViewKeys.Items.Add(zItem);
            }
        }

        /// <summary>
        /// Reconfigures the controls based on the state specified
        /// </summary>
        /// <param name="bCapturing">flag indicating if actively capturing keyboard input</param>
        private void ConfigureControls(bool bCapturing)
        {
            menuStripMain.Enabled = !bCapturing;
            panelKeySetup.Enabled = !bCapturing;
            panelKeySetupControls.Enabled = !bCapturing;
            btnStart.Text = bCapturing ? ActionText.Stop.ToString() : ActionText.Start.ToString();
            notifyIcon.Icon = bCapturing? Resources.KeyCapActive : Resources.KeyCapIdle;
            Icon = notifyIcon.Icon;
        }

        /// <summary>
        /// Applications cannot start minimized (at least they can't be switched immediately in the form load event)
        /// </summary>
        private void MinimizeThread()
        {
            Thread.Sleep(1);
            // this is a close to force the icon to the tray and remove the window from the task bar
            this.InvokeAction(() => Close());
        }

        #endregion
    }
}