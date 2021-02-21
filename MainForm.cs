using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotaGameAcceptor {
    public partial class MainForm : Form {
        private bool _hueConnected { get; set; }
        public string bridgeIpAddress { get; set; }

        private const string DEVICENAME = "AghanimsAcceptor";

        private static HueAuthSuccess _hueAuthUser;
        private static Dictionary<string, HueLightsDto> _hueBridgeLights;
        private static HueUserLights _hueUserLights;



        public MainForm() {
            InitializeComponent();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            // set initial image
            this.pictureBox.Image = AghanimsAcceptor.Properties.Resources.IdleImage;

            string infoString = "Game finder will not work without enabling Settings -> Advanced Options -> Bring Dota 2 to front when match found";
            showBalloon(title: "Important", body: infoString);

            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
        }

        private void OnStartClick(object sender, EventArgs e) {
            // show animated image
            this.pictureBox.Image = AghanimsAcceptor.Properties.Resources.AnimatedImage;
            // change button states
            this.buttonStart.Enabled = false;
            // start background operation
            this.backgroundWorker.RunWorkerAsync();
        }

        private void OnDoWork(object sender, DoWorkEventArgs e) {

            this.backgroundWorker.ReportProgress(-1, string.Format("Searching..."));

            string path = Directory.GetCurrentDirectory();

            int ExitCode;
            ProcessStartInfo ProcessInfo;
            Process Process;

            ProcessInfo = new ProcessStartInfo(path + "\\Script\\DotaGameAcceptor.exe");
            ProcessInfo.CreateNoWindow = true;
            ProcessInfo.UseShellExecute = false;

            Process = Process.Start(ProcessInfo);
            Process.WaitForExit();

            ExitCode = Process.ExitCode;
            Process.Close();

            if (ExitCode == 2) {
                e.Cancel = true;
            }

        }

        private void OnProgressChanged(object sender, ProgressChangedEventArgs e) {
            if (e.UserState is String) {
                this.labelProgress.Text = (String)e.UserState;
            }
        }

        private void OnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            // hide animation
            this.pictureBox.Image = null;
            // show result indication
            if (e.Cancelled) {
                this.labelProgress.Text = "Operation cancelled!";
                this.pictureBox.Image = AghanimsAcceptor.Properties.Resources.WarningImage;
            }
            else {
                if (e.Error != null) {
                    this.labelProgress.Text = "Operation failed: " + e.Error.Message;
                }
                else {
                    this.labelProgress.Text = "Operation finish!";
                    this.pictureBox.Image = AghanimsAcceptor.Properties.Resources.SuccessImage;

                    // Fire Hue Change Lights
                    if (_hueConnected) {
                        AlertHueLights();
                    }
                }
            }
            // restore button states
            this.buttonStart.Enabled = true;

        }

        private void MainForm_Load(object sender, EventArgs e) {

        }

        private static void showBalloon(string title, string body) {
            NotifyIcon notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Information;
            notifyIcon.Visible = true;

            if (title != null) {
                notifyIcon.BalloonTipTitle = title;
            }

            if (body != null) {
                notifyIcon.BalloonTipText = body;
            }

            notifyIcon.ShowBalloonTip(5);
        }

        private async void toolStripButton1_Click(object sender, EventArgs e) {

            this.hueConnectButton.Image = AghanimsAcceptor.Properties.Resources.lightbulb_waiting;

            // Amazing Error Handling for Failure to Connect
            try {
                _hueConnected = await ConnectToHueBridge();
            }
            catch (HttpRequestException ex) {
                WriteLog(ex.Message);

                if (ex.InnerException != null) {
                    WriteLog(ex.InnerException.Message);
                    WriteLog(ex.InnerException.StackTrace);
                }
                showBalloon(title: "Bridge Connection Issue", body: ex.Message);
                _hueConnected = false;
            }
            catch (Exception exAll) {
                WriteLog(exAll.Message);
                WriteLog(exAll.StackTrace);
                showBalloon(title: "Important", body: exAll.Message);
                _hueConnected = false;
            }
            finally {                
            }

            if (_hueConnected) {
                this.hueConnectButton.Image = AghanimsAcceptor.Properties.Resources.lightbulb_success;
                TestHueLights();
            }
            else {
                this.hueConnectButton.Image = AghanimsAcceptor.Properties.Resources.lightbulb_failed;
            }            
        }

        #region Config File

        public static void SaveHueAuthXml(HueAuthSuccess hueAuthSuccess) {            
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(HueAuthSuccess));

            var path = Directory.GetCurrentDirectory() + "\\Config\\HueAuthentication.xml";
            System.IO.FileStream file = System.IO.File.Create(path);

            writer.Serialize(file, hueAuthSuccess);
            file.Close();
        }

        public static HueAuthSuccess ReadHueAuthXml() {            
            // Now we can read the serialized book ...  
            System.Xml.Serialization.XmlSerializer reader =
                new System.Xml.Serialization.XmlSerializer(typeof(HueAuthSuccess));

            var path = Directory.GetCurrentDirectory() + "\\Config\\HueAuthentication.xml";
            System.IO.StreamReader file = new System.IO.StreamReader(path);
            HueAuthSuccess hueAuth = (HueAuthSuccess)reader.Deserialize(file);
            file.Close();
            return hueAuth;
        }

        public static HueUserLights ReadHueUserLightsXml() {
            // Now we can read the serialized book ...  
            System.Xml.Serialization.XmlSerializer reader =
                new System.Xml.Serialization.XmlSerializer(typeof(HueUserLights));

            var path = Directory.GetCurrentDirectory() + "\\Config\\HueUserLights.xml";
            System.IO.StreamReader file = new System.IO.StreamReader(path);
            HueUserLights hueUserLights = (HueUserLights)reader.Deserialize(file);
            file.Close();
            return hueUserLights;
        }

        #endregion


        #region Connection Methods

        static async Task<bool> ConnectToHueBridge() {
            bool connected = false;

            string hueIp = String.Empty;

            hueIp = await GetHueBridgeIpAddress();
            List<BridgeIpAddressResponse> bridgeIpAddressResponse = JsonConvert.DeserializeObject<List<BridgeIpAddressResponse>>(hueIp);
            HueBridgeDetails.ip = bridgeIpAddressResponse.First().internalipaddress;
            HueBridgeDetails.id = bridgeIpAddressResponse.First().id;

            // Try Read from config file for Hue Auth User
            try {
                _hueAuthUser = ReadHueAuthXml();
            }
            catch {

            }

            // If _hueAuthUser hasn't been set yet, try to connect
            if (_hueAuthUser == null) {
                string hueAuth = String.Empty;
                hueAuth = await GetHueAuthentication();

                if (hueAuth.ToUpper().Contains("ERROR")) {
                    List<HueAuthError> hueAuthErrors = JsonConvert.DeserializeObject<List<HueAuthError>>(hueAuth);
                    showBalloon(title: "Hue Authentication Error", body: hueAuthErrors.First().error.description + ". Press Link Button on Hue Bridge and try again.");
                    WriteLog(hueAuthErrors.First().error.description + ". Press Link Button on Hue Bridge and try again.");
                    connected = false;
                }
                else {
                    List<HueAuthSuccess> hueAuthSuccesses = JsonConvert.DeserializeObject<List<HueAuthSuccess>>(hueAuth);
                    _hueAuthUser = hueAuthSuccesses.First();
                    SaveHueAuthXml(_hueAuthUser);
                    connected = true;
                }

                return connected;
            }
            else {
                string hueLights = await GetHueLights();

                if (hueLights.ToUpper().Contains("ERROR")) {
                    List<HueAuthError> hueAuthErrors = JsonConvert.DeserializeObject<List<HueAuthError>>(hueLights);
                    showBalloon(title: "Hue Authentication Token Error", body: hueAuthErrors.First().error.description + ".");
                    WriteLog("Hue Authentication Token Error:" + hueAuthErrors.First().error.description + ". Delete Authentication configuration file and try again or update Authentication token.");
                    return false;
                }
                else {
                    _hueBridgeLights = JsonConvert.DeserializeObject<Dictionary<string, HueLightsDto>>(hueLights);
                    _hueUserLights = ReadHueUserLightsXml();
                    return true;
                }                
            }
        }

        private static async Task<string> GetHueLights() {
            string lightsJson = String.Empty;
            using (var httpClient = new HttpClient()) {
                string authUri = $"https://{HueBridgeDetails.ip}/api/{_hueAuthUser.success.username}/lights";
                // Build json and attach to HttpRequest
                HttpResponseMessage lightsResponse = await httpClient.GetAsync(authUri);
                if (lightsResponse.IsSuccessStatusCode) {
                    lightsJson = await lightsResponse.Content.ReadAsStringAsync();
                }
            }
            return lightsJson;
        }

        private static async Task<string> GetHueAuthentication() {
            string s = String.Empty;
            using (var httpClient = new HttpClient()) {
                string authUri = $"https://{HueBridgeDetails.ip}/api";
                // Build json and attach to HttpRequest
                HueAuthRequest authRequest = new HueAuthRequest() { devicetype = DEVICENAME };
                var stringPayload = await Task.Run(() => JsonConvert.SerializeObject(authRequest));
                var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage authResponse = await httpClient.PostAsync(authUri, httpContent);
                authResponse.EnsureSuccessStatusCode();
                s = await authResponse.Content.ReadAsStringAsync();                
            }
            return s;
        }

        private static async Task<string> GetHueBridgeIpAddress() {
            string s = string.Empty;
            using (var httpClient = new HttpClient()) {
                HttpResponseMessage ipResponse = await httpClient.GetAsync(@"https://discovery.meethue.com/");
                if (ipResponse.IsSuccessStatusCode) {
                    s = await ipResponse.Content.ReadAsStringAsync();
                }
            }
            return s;
        }

        private static async Task<string> AlertHueLights() {
            string s = String.Empty;

            List<string> lightIndexes = new List<string>();
            foreach (var light in _hueUserLights.Lights) {
                var bridgeLight = _hueBridgeLights.Where(x => x.Value.name == light.Name).FirstOrDefault();
                if (bridgeLight.Key != null)
                    lightIndexes.Add(bridgeLight.Key);
                else
                    WriteLog($"Did not find Light on Hue Bridge with Name = {light.Name}");
            }

            using (var httpClient = new HttpClient()) {
                foreach (var index in lightIndexes) {

                    string authUri = $"https://{HueBridgeDetails.ip}/api/{_hueAuthUser.success.username}/lights/{index}/state";
                    // Build json and attach to HttpRequest
                    var json = new { alert = "lselect" };
                    var stringPayload = await Task.Run(() => JsonConvert.SerializeObject(json));
                    var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage authResponse = await httpClient.PutAsync(authUri, httpContent);
                    authResponse.EnsureSuccessStatusCode();
                    s = await authResponse.Content.ReadAsStringAsync();
                }
            }
            return s;
        }

        private static async Task<string> TestHueLights() {
            string s = String.Empty;

            List<string> lightIndexes = new List<string>();
            foreach (var light in _hueUserLights.Lights) {
                var bridgeLight = _hueBridgeLights.Where(x => x.Value.name == light.Name).FirstOrDefault();
                if (bridgeLight.Key != null)
                    lightIndexes.Add(bridgeLight.Key);
                else
                    WriteLog($"Did not find Light on Hue Bridge with Name = {light.Name}");
            }

            using (var httpClient = new HttpClient()) {
                foreach (var index in lightIndexes) {

                    string authUri = $"https://{HueBridgeDetails.ip}/api/{_hueAuthUser.success.username}/lights/{index}/state";
                    // Build json and attach to HttpRequest
                    var json = new { alert = "select" };
                    var stringPayload = await Task.Run(() => JsonConvert.SerializeObject(json));
                    var httpContent = new StringContent(stringPayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage authResponse = await httpClient.PutAsync(authUri, httpContent);
                    authResponse.EnsureSuccessStatusCode();
                    s = await authResponse.Content.ReadAsStringAsync();
                }
            }
            return s;
        }

        #endregion

        #region Hue DTOs

        private class BridgeIpAddressResponse {
            public string id { get; set; }
            public string internalipaddress { get; set; }
        }

        private static class HueBridgeDetails {
            public static string ip { get; set; }
            public static string id { get; set; }
            public static string userName { get; set; }
        }

        private class HueAuthRequest {
            public string devicetype { get; set; }
        }
        private class HueAuthError {
            public Error error { get; set; }
            public class Error {
                public int type { get; set; }
                public string address { get; set; }
                public string description { get; set; }
            }
        }
        public class HueAuthSuccess {
            public Success success { get; set; }
            public class Success {
                public string username { get; set; }
            }
        }

        public class HueUserLights {
            public List<Light> Lights;
        }
        public class Light {
            public string Name;
        }

        public class State {
            public bool on { get; set; }
            public int bri { get; set; }
            public int hue { get; set; }
            public int sat { get; set; }
            public string effect { get; set; }
            public List<double> xy { get; set; }
            public int ct { get; set; }
            public string alert { get; set; }
            public string colormode { get; set; }
            public bool reachable { get; set; }
        }

        public class HueLightsDto {
            public State state { get; set; }
            public string type { get; set; }
            public string name { get; set; }
            public string modelid { get; set; }
            public string manufacturername { get; set; }
            public string uniqueid { get; set; }
            public string swversion { get; set; }
        }

        #endregion

        #region Logging

        public static void WriteLog(string log) {
            string path = Directory.GetCurrentDirectory();
            using (StreamWriter w = File.AppendText(path + "\\Logs\\log.txt")) {
                Log(log, w);                
            }
        }

        public static void Log(string logMessage, TextWriter w) {
            w.Write("\r\nLog Entry : ");
            w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            w.WriteLine("  :");
            w.WriteLine($"  :{logMessage}");
            w.WriteLine("-------------------------------");
        }

        public static void DumpLog(StreamReader r) {
            string line;
            while ((line = r.ReadLine()) != null) {
                Console.WriteLine(line);
            }
        }

        #endregion
    }
}
