using System;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

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

        // Empêche l'ouverture simultanée de plusieurs alertes
        private bool alertDisplayed = false;


        // ============================================================
        // API WINDOWS
        // ============================================================

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);


        // Petit wrapper permettant d'utiliser un Handle Win32
        // comme propriétaire d'une fenêtre WinForms.
        private class WindowWrapper : IWin32Window
        {
            private readonly IntPtr handle;

            public WindowWrapper(IntPtr handle)
            {
                this.handle = handle;
            }

            public IntPtr Handle
            {
                get { return handle; }
            }
        }


        // ============================================================
        // INITIALISATION DU PLUGIN
        // ============================================================

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);

            about.Name = "Source Protector";
            about.Description = "Ensures exclusive audio source connectivity.";
            about.Author = "Lucas M.";
            about.Type = PluginType.General;
            about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;

            string settingsFolder =
                mbApiInterface.Setting_GetPersistentStoragePath();

            settingsFile = Path.Combine(
                settingsFolder,
                "SourceProtectorSettings.txt"
            );

            LoadSettings();

            return about;
        }


        // ============================================================
        // DEMARRAGE MUSICBEE
        // ============================================================

        public void ReceiveNotification(
            string sourceFileUrl,
            NotificationType type)
        {
            if (type == NotificationType.PluginStartup)
            {
                // Vérification immédiate au démarrage.
                if (!string.IsNullOrEmpty(targetSource))
                    CheckSource(true);

                // Si MusicBee n'a pas déjà été tué,
                // on démarre la surveillance.
                timerSurveillance =
                    new System.Windows.Forms.Timer();

                timerSurveillance.Interval = 1000;

                timerSurveillance.Tick +=
                    (s, e) => CheckSource(false);

                timerSurveillance.Start();
            }
        }


        // ============================================================
        // VERIFICATION DE LA SOURCE AUDIO
        // ============================================================

        private void CheckSource(bool atStartup)
        {
            if (string.IsNullOrEmpty(targetSource))
                return;

            // Une alerte est déjà affichée :
            // inutile de refaire une vérification.
            if (alertDisplayed)
                return;

            bool sourceDetected = false;

            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name FROM Win32_PnPEntity " +
                        "WHERE Status = 'OK' AND PNPClass = 'MEDIA'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name =
                            obj["Name"] != null
                            ? obj["Name"].ToString()
                            : "";

                        if (name.IndexOf(
                                targetSource,
                                StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sourceDetected = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // En cas d'erreur WMI, on conserve le comportement
                // actuel du plugin.
            }

            if (!sourceDetected)
            {
                if (timerSurveillance != null)
                    timerSurveillance.Stop();

                ShowAlert(atStartup);
            }
        }


        // ============================================================
        // ALERTE BLOQUANTE / MODALE
        // ============================================================

        private void ShowAlert(bool atStartup)
        {
            if (alertDisplayed)
                return;

            alertDisplayed = true;

            // On mémorise le handle de MusicBee AVANT
            // de créer notre fenêtre.
            IntPtr musicBeeHandle = IntPtr.Zero;

            try
            {
                Process currentProcess = Process.GetCurrentProcess();

                currentProcess.Refresh();

                musicBeeHandle = currentProcess.MainWindowHandle;
            }
            catch
            {
                musicBeeHandle = IntPtr.Zero;
            }


            Form alert = new Form();

            alert.Text = "Source Protector - Alert";

            alert.Size = new Size(520, 235);

            alert.FormBorderStyle = FormBorderStyle.FixedDialog;

            alert.MaximizeBox = false;
            alert.MinimizeBox = false;

            // Supprime le X.
            // L'utilisateur doit choisir une action.
            alert.ControlBox = false;

            // N'ajoute pas une seconde icône MusicBee
            // dans la barre des tâches.
            alert.ShowInTaskbar = false;

            // Toujours devant les autres fenêtres.
            alert.TopMost = true;

            alert.StartPosition =
                musicBeeHandle != IntPtr.Zero
                ? FormStartPosition.CenterParent
                : FormStartPosition.CenterScreen;


            // --------------------------------------------------------
            // MESSAGE
            // --------------------------------------------------------

            string message;

            if (atStartup)
            {
                message =
                    "The protected audio source is not available.\n\n" +
                    "Source:\n" +
                    targetSource +
                    "\n\n" +
                    "MusicBee must close to protect the " +
                    "exclusive audio configuration.";
            }
            else
            {
                message =
                    "The protected audio source was disconnected!\n\n" +
                    "Source:\n" +
                    targetSource +
                    "\n\n" +
                    "MusicBee must close to protect the " +
                    "exclusive audio configuration.";
            }


            Label lbl = new Label();

            lbl.Text = message;

            lbl.Location = new Point(20, 20);

            lbl.Size = new Size(470, 115);

            lbl.AutoSize = false;


            // --------------------------------------------------------
            // BOUTON QUITTER
            // --------------------------------------------------------

            Button btnQuit = new Button();

            btnQuit.Text = "Quit MusicBee";

            btnQuit.Location = new Point(40, 145);

            btnQuit.Size = new Size(180, 35);

            btnQuit.Click += (s, e) =>
            {
                // Terminaison immédiate.
                //
                // On ne demande volontairement PAS à MusicBee
                // d'effectuer une fermeture normale afin d'éviter
                // qu'il sauvegarde éventuellement une nouvelle
                // configuration de périphérique audio.
                Process.GetCurrentProcess().Kill();
            };


            // --------------------------------------------------------
            // BOUTON RESET
            // --------------------------------------------------------

            Button btnReset = new Button();

            btnReset.Text = "Reset Config";

            btnReset.Location = new Point(280, 145);

            btnReset.Size = new Size(180, 35);

            btnReset.ForeColor = Color.Red;

            btnReset.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(settingsFile))
                        File.Delete(settingsFile);
                }
                catch
                {
                }

                Process.GetCurrentProcess().Kill();
            };


            // --------------------------------------------------------
            // EMPECHE ALT+F4 / FERMETURE MANUELLE
            // --------------------------------------------------------

            alert.FormClosing += (s, e) =>
            {
                // Ne permet pas de fermer simplement la popup.
                //
                // Elle doit rester présente jusqu'à ce que
                // Quit MusicBee ou Reset Config soit utilisé.
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                }
            };


            alert.Controls.Add(lbl);
            alert.Controls.Add(btnQuit);
            alert.Controls.Add(btnReset);


            // --------------------------------------------------------
            // FORCE LA POPUP AU PREMIER PLAN
            // --------------------------------------------------------

            alert.Shown += (s, e) =>
            {
                try
                {
                    alert.TopMost = true;

                    alert.BringToFront();
                    alert.Activate();

                    BringWindowToTop(alert.Handle);
                    SetForegroundWindow(alert.Handle);

                    // Place directement le focus sur Quit MusicBee.
                    btnQuit.Focus();
                }
                catch
                {
                }
            };


            // --------------------------------------------------------
            // VRAIE MODALITE MUSICBEE
            // --------------------------------------------------------

            try
            {
                if (musicBeeHandle != IntPtr.Zero &&
                    IsWindow(musicBeeHandle))
                {
                    WindowWrapper owner =
                        new WindowWrapper(musicBeeHandle);

                    /*
                     * C'est la différence importante :
                     *
                     * MusicBee devient le propriétaire de la popup.
                     *
                     * Windows désactive alors la fenêtre MusicBee
                     * pendant toute la durée de ShowDialog().
                     */
                    alert.ShowDialog(owner);
                }
                else
                {
                    // Fallback très improbable :
                    // le handle MusicBee n'était pas encore disponible.
                    alert.ShowDialog();
                }
            }
            finally
            {
                alert.Dispose();
                alertDisplayed = false;
            }
        }


        // ============================================================
        // CONFIGURATION DU PLUGIN
        // ============================================================

        public bool Configure(IntPtr panelHandle)
        {
            Form form = new Form();

            form.Text = "Configure Source Protector";

            form.Size = new Size(400, 160);

            form.FormBorderStyle =
                FormBorderStyle.FixedDialog;

            form.MaximizeBox = false;
            form.MinimizeBox = false;

            form.StartPosition =
                FormStartPosition.CenterParent;


            ComboBox combo = new ComboBox();

            combo.Location = new Point(20, 20);

            combo.Width = 340;

            combo.DropDownStyle =
                ComboBoxStyle.DropDownList;


            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name FROM Win32_PnPEntity " +
                        "WHERE PNPClass = 'MEDIA'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["Name"] != null)
                        {
                            combo.Items.Add(
                                obj["Name"].ToString()
                            );
                        }
                    }
                }
            }
            catch
            {
            }


            if (combo.Items.Contains(targetSource))
                combo.SelectedItem = targetSource;


            Button btn = new Button();

            btn.Text = "Save";

            btn.Location = new Point(140, 65);

            btn.Size = new Size(100, 30);


            btn.Click += (s, e) =>
            {
                if (combo.SelectedItem != null)
                {
                    targetSource =
                        combo.SelectedItem.ToString();

                    File.WriteAllText(
                        settingsFile,
                        targetSource
                    );

                    form.Close();
                }
            };


            form.Controls.Add(combo);
            form.Controls.Add(btn);


            // Configuration également modale par rapport à MusicBee.
            if (panelHandle != IntPtr.Zero &&
                IsWindow(panelHandle))
            {
                WindowWrapper owner =
                    new WindowWrapper(panelHandle);

                form.ShowDialog(owner);
            }
            else
            {
                form.ShowDialog();
            }


            form.Dispose();

            return true;
        }


        // ============================================================
        // SETTINGS
        // ============================================================

        private void LoadSettings()
        {
            if (File.Exists(settingsFile))
            {
                targetSource =
                    File.ReadAllText(settingsFile).Trim();
            }
        }


        public void SaveSettings()
        {
        }


        public void Close(PluginCloseReason reason)
        {
            if (timerSurveillance != null)
            {
                timerSurveillance.Stop();
                timerSurveillance.Dispose();
                timerSurveillance = null;
            }
        }


        public void Uninstall()
        {
            if (File.Exists(settingsFile))
                File.Delete(settingsFile);
        }
    }
}