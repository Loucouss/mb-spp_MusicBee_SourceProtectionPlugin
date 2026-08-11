using System;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;
using System.IO;
using System.Drawing;

namespace MusicBeePlugin
{
    // Note: On garde MusicBeePlugin comme namespace pour l'interface, 
    // mais le fichier final sera mb_spp.dll
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();
        private string targetSource = "";
        private string settingsFile;
        private System.Windows.Forms.Timer timerSurveillance;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);

            about.Name = "Source Protector";
            about.Description = "Ensures exclusive audio source connectivity.";
            about.Author = "Lucas M.";
            about.Type = PluginType.General;
            about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;

            string settingsFolder = mbApiInterface.Setting_GetPersistentStoragePath();
            settingsFile = Path.Combine(settingsFolder, "SourceProtectorSettings.txt");
            LoadSettings();

            return about;
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            if (type == NotificationType.PluginStartup)
            {
                if (!string.IsNullOrEmpty(targetSource)) CheckSource(true);

                timerSurveillance = new System.Windows.Forms.Timer();
                timerSurveillance.Interval = 1000;
                timerSurveillance.Tick += (s, e) => CheckSource(false);
                timerSurveillance.Start();
            }
        }

        private void CheckSource(bool atStartup)
        {
            if (string.IsNullOrEmpty(targetSource)) return;

            bool sourceDetected = false;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Status = 'OK' AND PNPClass = 'MEDIA'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["Name"]?.ToString().Contains(targetSource) == true)
                    {
                        sourceDetected = true;
                        break;
                    }
                }
            }
            catch { }

            if (!sourceDetected)
            {
                if (timerSurveillance != null) timerSurveillance.Stop();
                ShowAlert(atStartup);
            }
        }

        private void ShowAlert(bool atStartup)
        {
            Form alert = new Form { Text = "Source Protector - Alert", Size = new Size(450, 200), StartPosition = FormStartPosition.CenterScreen, TopMost = true };
            Label lbl = new Label { Text = (atStartup ? "The audio source is turned off at startup!" : "The audio source was disconnected!") + "\n\nMusicBee must close to protect your exclusive configuration.", Location = new Point(20, 20), AutoSize = true };

            Button btnQuit = new Button { Text = "Quit MusicBee", Location = new Point(20, 100), Width = 150 };
            btnQuit.Click += (s, e) => { Process.GetCurrentProcess().Kill(); };

            Button btnReset = new Button { Text = "Reset Config", Location = new Point(220, 100), Width = 150, ForeColor = Color.Red };
            btnReset.Click += (s, e) => {
                if (File.Exists(settingsFile)) File.Delete(settingsFile);
                Process.GetCurrentProcess().Kill();
            };

            alert.Controls.Add(lbl); alert.Controls.Add(btnQuit); alert.Controls.Add(btnReset);
            alert.ShowDialog();
        }

        public bool Configure(IntPtr panelHandle)
        {
            Form form = new Form { Text = "Configure Source Protector", Size = new Size(400, 150), StartPosition = FormStartPosition.CenterParent };
            ComboBox combo = new ComboBox { Location = new Point(20, 20), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'MEDIA'");
                foreach (ManagementObject obj in searcher.Get()) combo.Items.Add(obj["Name"].ToString());
            }
            catch { }

            if (combo.Items.Contains(targetSource)) combo.SelectedItem = targetSource;

            Button btn = new Button { Text = "Save", Location = new Point(140, 60) };
            btn.Click += (s, e) => {
                if (combo.SelectedItem != null)
                {
                    targetSource = combo.SelectedItem.ToString();
                    File.WriteAllText(settingsFile, targetSource);
                    form.Close();
                }
            };

            form.Controls.Add(combo); form.Controls.Add(btn);
            form.ShowDialog();
            return true;
        }

        private void LoadSettings() { if (File.Exists(settingsFile)) targetSource = File.ReadAllText(settingsFile).Trim(); }
        public void SaveSettings() { }
        public void Close(PluginCloseReason reason) { if (timerSurveillance != null) timerSurveillance.Stop(); }
        public void Uninstall() { if (File.Exists(settingsFile)) File.Delete(settingsFile); }
    }
}