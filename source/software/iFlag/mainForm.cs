﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using iFlag.Properties;

namespace iFlag
{
    public partial class mainForm : Form
    {
        // Version
        const byte major = 0;                     // Major version number of this software 
        const byte minor = 64;                    // Minor version number

        // Embedded firmware version
        const byte firmwareMajor = 0;             // Major version number of the firmware payload
        const byte firmwareMinor = 20;            // Minor version number

                                                  // In case of special occasions releases,
                                                  // this is what holds the edition string,
                                                  // which features in the window title
        String edition = "";

        string repositoryURL = "https://github.com/simracer-cz/iFlag";
        string forumThreadURL = "http://members.iracing.com/jforum/posts/list/0/3341549.page";
        string updateURL = "http://simracer.cz/iflag/version.xml";

        bool simConnected;
        bool greeted;                             // Whether the startup greeting has happened

        public mainForm()
        {
            InitializeComponent();

            if (edition != "") this.Text += edition;
            flagLabel.Text = appMenuItem.Text = "iFlag v" + major + "." + minor;

                                                  // Initialize flag modules
            startCommunication();
            startSDK();
            startMatrix();
            startFlags();
            startUpdater();
        }

                                                  // When the window opens,
                                                  // restore persistent user options.
        private void mainForm_Load(object sender, EventArgs e)
        {
            if (!Settings.Default.WindowLocation.IsEmpty)
            {
                this.Location = Settings.Default.WindowLocation;
            }
            else
            {
                System.Drawing.Point screenCenter = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Location;
                screenCenter.X += System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width / 2 - this.Width / 2;
                screenCenter.Y += System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height / 2 - this.Height / 2;
                this.Location = screenCenter;
            }
            this.WindowState = Settings.Default.WindowState;
            this.TopMost = this.alwaysOnTopMenuItem.Checked = Settings.Default.WindowTopMost;
            this.demoMenuItem.Checked = Settings.Default.DemoMode;
            this.startLightsModuleMenuItem.Checked = Settings.Default.ShowStartLights;

            connectorSide = Settings.Default.UsbConnector;
            switch (connectorSide)
            {
                case 0x00: connectorBottomMenuItem.Checked = true; break;
                case 0x01: connectorRightMenuItem.Checked = true; break;
                case 0x02: connectorLeftMenuItem.Checked = true; break;
                case 0x03: connectorTopMenuItem.Checked = true; break;
            }

            matrixLuma = Settings.Default.MatrixLuma;
            switch (matrixLuma)
            {
                case 10: lowBrightnessMenuItem.Checked = true; break;
                case 25: mediumBrightnessMenuItem.Checked = true; break;
                case 60: highBrightnessMenuItem.Checked = true; break;
                case 100: fullBrightnessMenuItem.Checked = true; break;
            }

            updatesLevel = Settings.Default.Updates;
            switch (updatesLevel)
            {
                case "stable": this.stableUpdatesMenuItem.Checked = true; break;
                case "experimental": this.experimentalUpdatesMenuItem.Checked = true; break;
                case "none": this.noUpdatesMenuItem.Checked = true; break;
            }

            restoreCommunication();
        }

                                                  // When the window closes,
                                                  // make sure to save all persistent user options.
        private void mainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            showFlag(NO_FLAG);
            Settings.Default.WindowState = this.WindowState;
            if (this.WindowState == FormWindowState.Normal) Settings.Default.WindowLocation = this.Location;
            Settings.Default.WindowTopMost = this.TopMost;
            Settings.Default.DemoMode = this.demoMenuItem.Checked;
            Settings.Default.ShowStartLights = this.startLightsModuleMenuItem.Checked;
            Settings.Default.UsbConnector = connectorSide;
            Settings.Default.MatrixLuma = matrixLuma;
            Settings.Default.Updates = updatesLevel;
            storeCommunication();
            Settings.Default.Save();
        }

                                                  // When the window moves,
                                                  // make its new position persistent.
        private void mainForm_Move(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal) Settings.Default.WindowLocation = this.Location;
        }

                                                  // GUI
        private void optionsButton_Click(object sender, EventArgs e)
        {
            optionsMenu.Show(Cursor.Position);
        }


        private void gitMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(repositoryURL);
        }

        private void forumThreadMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(forumThreadURL);
        }

        private void demoTimer_Tick(object sender, EventArgs e)
        {
            showDemoFlag();
        }

        private void demoMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            Settings.Default.DemoMode = this.demoMenuItem.Checked;
            demoTimer.Stop();
            demoTimer.Start();
            showDemoFlag();
        }

        private void alwaysOnTopMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            Settings.Default.WindowTopMost = this.TopMost = this.alwaysOnTopMenuItem.Checked;
        }

                                                  // Visually indicates the serial connection alive state
                                                  // by changin color of the "Matrix" indicator
                                                  // in the lower left corner of the main form
                                                  //
        private void indicateConnection()
        {
            if (deviceConnected)
            {
                if (!deviceUpdated())
                {
                    updateFirmware();
                }
                else
                {
                    if (!greeted)
                    {
                        greeted = true;
                        showSystemFlag(STARTUP_GREETING);
                        demoTimer.Enabled = true;
                        updateSoftware();
                    }
                    hardwareLight.BackColor = Color.FromName("ForestGreen");
                    commLabel.Text = "v" + firmwareVersionMajor + "." + firmwareVersionMinor + " @" + port;
                }
            }
            else
            {
                hardwareLight.BackColor = Color.FromName("Red");
                commLabel.Text = "";
            }

            detectSDK();
        }

                                                  // Visually indicates a running iRacing session
                                                  // in which demo mode gets disabled and telemetry update
                                                  // gets enabled
        private void indicateSimConnected(bool connected)
        {
            if (connected && !simConnected)
            {
                simLight.BackColor = Color.FromName("ForestGreen");

                demoMenuItem.Enabled = false;
                demoTimer.Enabled = false;
                updateTimer.Enabled = true;
            }
            else if (!connected && simConnected)
            {
                simLight.BackColor = Color.FromName("Red");

                demoMenuItem.Enabled = true;
                demoTimer.Enabled = true;
                updateTimer.Enabled = false;
                sessionDetected = false;
            }
            simConnected = connected;
        }

        private void connectorMenuItem_Click(object sender, EventArgs e)
        {
            switch (((ToolStripMenuItem)sender).Name)
            {
                case "connectorBottomMenuItem": connectorSide = 0x00; break;
                case "connectorRightMenuItem":  connectorSide = 0x01; break;
                case "connectorLeftMenuItem":   connectorSide = 0x02; break;
                case "connectorTopMenuItem":    connectorSide = 0x03; break;
            }
            connectorTopMenuItem.Checked = false;
            connectorLeftMenuItem.Checked = false;
            connectorRightMenuItem.Checked = false;
            connectorBottomMenuItem.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;

            Settings.Default.UsbConnector = connectorSide;
            showSystemFlag(ORIENTATION_CHECK);
        }

        private void brightnessMenuItem_Click(object sender, EventArgs e)
        {
            switch (((ToolStripMenuItem)sender).Name)
            {
                case "fullBrightnessMenuItem":    matrixLuma = 100; break;
                case "highBrightnessMenuItem":    matrixLuma = 60; break;
                case "mediumBrightnessMenuItem":  matrixLuma = 25; break;
                case "lowBrightnessMenuItem":     matrixLuma = 10; break;
            }
            fullBrightnessMenuItem.Checked = false;
            highBrightnessMenuItem.Checked = false;
            mediumBrightnessMenuItem.Checked = false;
            lowBrightnessMenuItem.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;

            Settings.Default.MatrixLuma = matrixLuma;
            setMatrixLuma();
            showSystemFlag(LUMA_CHECK);
        }

        private void updatesModeMenuItem_Click(object sender, EventArgs e)
        {
            switch (((ToolStripMenuItem)sender).Name)
            {
                case "stableUpdatesMenuItem": updatesLevel = "stable"; break;
                case "experimentalUpdatesMenuItem": updatesLevel = "experimental"; break;
                case "noUpdatesMenuItem": updatesLevel = "none"; break;
            }
            noUpdatesMenuItem.Checked = false;
            stableUpdatesMenuItem.Checked = false;
            experimentalUpdatesMenuItem.Checked = false;
            ((ToolStripMenuItem)sender).Checked = true;

            Settings.Default.Updates = updatesLevel;
            updateSoftware();
        }
    }
}
