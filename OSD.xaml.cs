using System;
using System.Windows;
using System.Windows.Threading;

namespace SDRbright {
    public partial class OSD : Window {
        private readonly DispatcherTimer _OSDtimeout;
        public OSD() {
            InitializeComponent();
            _OSDtimeout = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _OSDtimeout.Tick += (sender, e) => {
                _OSDtimeout.Stop();
                this.Close();
            };
        }

        public void ShowOSD(int brightness) {
            BrightnessBar.Value = brightness;
            Value.Text = $"{brightness}%";
            if (!this.IsVisible) {
                this.Show();
            }
            _OSDtimeout.Stop();
            _OSDtimeout.Start();
        }
    }
}
