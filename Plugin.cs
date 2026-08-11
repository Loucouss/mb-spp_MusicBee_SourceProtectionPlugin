using System;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);

            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "DAC Protector";
            about.Description = "Tue brutalement MusicBee si le SMSL C200 est éteint.";
            about.Author = "Lucas M.";
            about.TargetApplication = "MusicBee";
            about.Type = PluginType.General;
            about.VersionMajor = 1;
            about.VersionMinor = 0;
            about.Revision = 0;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;
            about.ConfigurationPanelHeight = 0;

            VerifierDAC();

            return about;
        }

        private void VerifierDAC()
        {
            bool dacDetecte = false;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%SMSL%'");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    if (queryObj["Status"] != null && queryObj["Status"].ToString().Equals("OK"))
                    {
                        dacDetecte = true;
                        break;
                    }
                }
            }
            catch (Exception) { }

            if (!dacDetecte)
            {
                MessageBox.Show("Le DAC SMSL C200 est éteint ou introuvable.\n\nFermeture d'urgence de MusicBee pour préserver vos réglages WASAPI Exclusif.", "Protection DAC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Process.GetCurrentProcess().Kill();
            }
        }

        public bool Configure(IntPtr panelHandle) { return false; }
        public void SaveSettings() { }
        public void Close(PluginCloseReason reason) { }
        public void Uninstall() { }
        public void ReceiveNotification(string sourceFileUrl, NotificationType type) { }
    }
}