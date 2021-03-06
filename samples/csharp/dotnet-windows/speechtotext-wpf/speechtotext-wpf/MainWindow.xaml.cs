//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace MicrosoftSpeechSDKSamples.WpfSpeechRecognitionSample
{
    using System;
    using System.Globalization;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Media;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Forms = System.Windows.Forms;
    using System.IO.IsolatedStorage;
    using System.Configuration;
    using System.Linq;
    using Microsoft.CognitiveServices.Speech;
    using Microsoft.CognitiveServices.Speech.Audio;
    using Microsoft.Bot.Connector.DirectLine;
    using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime;
    using speechtotextwpf;

    using System.Threading;
    using System.Windows.Media.Animation;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Properties

        /// <summary>
        /// True, if audio source is mic
        /// </summary>
        public bool UseMicrophone { get; set; }

        /// <summary>
        /// True, if audio source is audio file
        /// </summary>
        public bool UseFileInput { get; set; }

        /// <summary>
        /// Only baseline model used for recognition
        /// </summary>
        public bool UseBaseModel { get; set; }

        /// <summary>
        /// Only custom model used for recognition
        /// </summary>
        public bool UseCustomModel { get; set; }

        /// <summary>
        /// use directline bot for conversation
        /// </summary>
        public bool UseDirectLineBot { get; set; }

        /// <summary>
        /// use luis bot for conversation
        /// </summary>
        public bool UseLuisBot { get; set; }

        /// <summary>
        /// Both models used for recognition
        /// </summary>
        public bool UseBaseAndCustomModels { get; set; }

        /// <summary>
        /// Gets or sets Subscription Key
        /// </summary>
        public string SubscriptionKey
        {
            get
            {
                return this.subscriptionKey;
            }

            set
            {
                this.subscriptionKey = value?.Trim();
                this.OnPropertyChanged<string>();
            }
        }

        /// <summary>
        /// Gets or sets Subscription Key
        /// </summary>
        public string DirectLineKey
        {
            get
            {
                return this.directLineKey;
            }

            set
            {
                this.directLineKey = value?.Trim();
                this.OnPropertyChanged<string>();
            }
        }


        /// <summary>
        /// Gets or sets region name of the service
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Gets or sets recognition language
        /// </summary>
        public string RecognitionLanguage { get; set; }

        /// <summary>
        /// Gets or sets endpoint ID of the custom model
        /// </summary>
        public string CustomModelEndpointId
        {
            get
            {
                return this.endpointId;
            }

            set
            {
                this.endpointId = value?.Trim();
                this.OnPropertyChanged<string>();
            }
        }

        // Private properties
        private const string defaultLocale = "en-US";
        private string endpointId;
        private string subscriptionKey;
        private string directLineKey;
        private const string endpointIdFileName = "CustomModelEndpointId.txt";
        private const string subscriptionKeyFileName = "SubscriptionKey.txt";
        private string wavFileName;
        private SpeechSynthesizer speechSynthesizer = null;
        private DeviceStatus deviceStatus = null;

        // The TaskCompletionSource must be rooted.
        // See https://blogs.msdn.microsoft.com/pfxteam/2011/10/02/keeping-async-methods-alive/ for details.
        private TaskCompletionSource<int> stopBaseRecognitionTaskCompletionSource;
        private TaskCompletionSource<int> stopCustomRecognitionTaskCompletionSource;

        #endregion

        /// <summary>
        /// For this app there are two recognizers, one with the baseline model (Base), one with CRIS model (Custom)
        /// </summary>
        enum RecoType
        {
            Base = 1,
            Custom = 2
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();
            this.Initialize();
        }

        /// <summary>
        /// Get the text value of configuration
        /// </summary>
        private string GetAppSetting(string key, string defaultValue = null)
        {
            string val = defaultValue;

            try
            {
                val = ConfigurationManager.AppSettings[key];
            }
            catch
            {
                val = defaultValue;
            }

            return val;
        }

        /// <summary>
        /// Initializes a fresh audio session.
        /// </summary>
        private void Initialize()
        {
            this.mainWindow.Title = GetAppSetting("Title", "Microsoft Speech SDK Speech Recognition Sample");

            this.UseMicrophone = (GetAppSetting("UseMicrophone", "true").ToLower().CompareTo("true") == 0);     
            this.UseFileInput = !this.UseMicrophone;

            this.UseBaseModel = true;
            this.UseCustomModel = false;
            this.UseBaseAndCustomModels = false;

            this.UseLuisBot = true;
            this.UseDirectLineBot = false;

            // Set the default values for UI
            this.fileInputRadioButton.IsChecked = this.UseFileInput;
            this.micRadioButton.IsChecked = this.UseMicrophone;

            this.basicRadioButton.IsChecked = true;
            this.luisBotButton.IsChecked = true;
            this.stopButton.IsEnabled = false;

            this.SubscriptionKey = GetAppSetting("SubscriptionKey", "");
            if (string.IsNullOrEmpty(this.SubscriptionKey))
            {
                this.SubscriptionKey = this.GetValueFromIsolatedStorage(subscriptionKeyFileName);
            }

            this.CustomModelEndpointId = GetAppSetting("CustomModelEndpointId", "");
            if (string.IsNullOrEmpty(this.CustomModelEndpointId))
            {
                this.CustomModelEndpointId = this.GetValueFromIsolatedStorage(endpointIdFileName);
            }

            string region = GetAppSetting("Region");
            if(!string.IsNullOrEmpty(region))
            {
                foreach (ComboBoxItem item in regionComboBox.Items)
                {
                    if (string.Compare(item.Tag.ToString(), region, true) == 0)
                    {
                        regionComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            string language = GetAppSetting("Language");
            if (!string.IsNullOrEmpty(language))
            {
                foreach (ComboBoxItem item in languageComboBox.Items)
                {
                    if (string.Compare(item.Tag.ToString(), language, true) == 0)
                    {
                        languageComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            this.DirectLineKey = language = GetAppSetting("DirectLineKey");
            this.EnableButtons();

            deviceStatus = new DeviceStatus();
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0,0,0,0,500);
            dispatcherTimer.Start();
        }

        /// <summary>
        /// Handles the Click event of the StartButton:
        /// Disables Settings Panel in UI to prevent Click Events during Recognition
        /// Checks if keys are valid
        /// Plays audio if input source is a valid audio file
        /// Triggers Creation of specified Recognizers
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.startButton.IsEnabled = false;
            this.stopButton.IsEnabled = true;
            this.radioGroup.IsEnabled = false;
            this.optionPanel.IsEnabled = false;
            this.LogRecognitionStart(this.customModelLogText, this.customModelCurrentText);
            this.LogRecognitionStart(this.baseModelLogText, this.baseModelCurrentText);
            wavFileName = "";

            this.Region = ((ComboBoxItem)regionComboBox.SelectedItem).Tag.ToString();
            this.RecognitionLanguage = ((ComboBoxItem)languageComboBox.SelectedItem).Tag.ToString();

            if (!AreKeysValid())
            {
                if (this.UseBaseModel)
                {
                    MessageBox.Show("Subscription Key is wrong or missing!");
                    this.WriteLine(this.baseModelLogText, "--- Error : Subscription Key is wrong or missing! ---");
                }
                else if (this.UseCustomModel)
                {
                    MessageBox.Show("Subscription Key or Custom Model Endpoint ID is missing or wrong! If you do not need the custom model, please change Settings->Recognition Type.");
                    this.WriteLine(this.customModelLogText, "--- Error : Subscription Key or Custom Model endpoint ID is wrong or missing! ---");
                }
                else if (this.UseBaseAndCustomModels)
                {
                    MessageBox.Show("Subscription Key or Custom Model endpoint ID is missing or wrong! If you do not need the custom model, please change Settings->Recognition Type.");
                    this.WriteLine(this.baseModelLogText, "--- Error : Subscription Key or Custom Model endpoint ID is wrong or missing! ---");
                    this.WriteLine(this.customModelLogText, "--- Error : Subscription Key or Custom Model endpoint ID is wrong or missing! ---");
                }

                this.EnableButtons();
                return;
            }

            if (!this.UseMicrophone)
            {
                wavFileName = GetFile();
                if (wavFileName.Length <= 0) return;
                Task.Run(() => this.PlayAudioFile());
            }

            if (this.UseCustomModel || this.UseBaseAndCustomModels)
            {
                stopCustomRecognitionTaskCompletionSource = new TaskCompletionSource<int>();
                Task.Run(async () => { await CreateCustomReco().ConfigureAwait(false); });
            }

            if (this.UseBaseModel || this.UseBaseAndCustomModels)
            {
                stopBaseRecognitionTaskCompletionSource = new TaskCompletionSource<int>();
                Task.Run(async () => { await CreateBaseReco().ConfigureAwait(false); });
            }

            CreateSynthesizer();
        }


        /// <summary>
        /// Handles the Click event of the StopButton:
        /// Stops Recognition and enables Settings Panel in UI
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            this.stopButton.IsEnabled = false;
            if (this.UseBaseModel || this.UseBaseAndCustomModels)
            {
                stopBaseRecognitionTaskCompletionSource.TrySetResult(0);
            }
            if (this.UseCustomModel || this.UseBaseAndCustomModels)
            {
                stopCustomRecognitionTaskCompletionSource.TrySetResult(0);
            }

            ReleaseSynthesizer();
            EnableButtons();
        }

        /// <summary>
        /// Creates Synthesizer with baseline model and selected language:
        /// Creates a config with subscription key and selected region
        /// </summary>
        private void CreateSynthesizer()
        {
            if (speechSynthesizer == null)
            {
                var config = SpeechConfig.FromSubscription(this.SubscriptionKey, this.Region);
                config.SpeechSynthesisLanguage = this.RecognitionLanguage;

                speechSynthesizer = new SpeechSynthesizer(config);
            }
        }

        /// <summary>
        /// Release Synthesizer with baseline model and selected language:
        /// </summary>
        private void ReleaseSynthesizer()
        {
            if (speechSynthesizer != null)
            {
                //speechSynthesizer.Dispose();
                speechSynthesizer = null;
            }
        }

        /// <summary>
        /// Synthesize with baseline model and selected language:
        /// </summary>
        private async Task SynthesizeText(string text)
        {
            if (speechSynthesizer != null && !string.IsNullOrEmpty(text))
            {
                await speechSynthesizer.SpeakTextAsync(text);
            }
        }

        /// <summary>
        /// Creates Recognizer with baseline model and selected language:
        /// Creates a config with subscription key and selected region
        /// If input source is audio file, creates recognizer with audio file otherwise with default mic
        /// Waits on RunRecognition
        /// </summary>
        private async Task CreateBaseReco()
        {
            // Todo: suport users to specifiy a different region.
            var config = SpeechConfig.FromSubscription(this.SubscriptionKey, this.Region);
            config.SpeechRecognitionLanguage = this.RecognitionLanguage;

            //create recognizer
            SpeechRecognizer basicRecognizer;
            if (this.UseMicrophone)
            {
                using (basicRecognizer = new SpeechRecognizer(config))
                {
                    await this.RunRecognizer(basicRecognizer, RecoType.Base, stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                }
            }
            else
            {
                using (var audioInput = AudioConfig.FromWavFileInput(wavFileName))
                {
                    using (basicRecognizer = new SpeechRecognizer(config, audioInput))
                    {
                        await this.RunRecognizer(basicRecognizer, RecoType.Base, stopBaseRecognitionTaskCompletionSource).ConfigureAwait(false);
                    }
               }
            }
        }

        /// <summary>
        /// Creates Recognizer with custom model endpointId and selected language:
        /// Creates a config with subscription key and selected region
        /// If input source is audio file, creates recognizer with audio file otherwise with default mic
        /// Waits on RunRecognition
        /// </summary>
        private async Task CreateCustomReco()
        {
            // Todo: suport users to specifiy a different region.
            var config = SpeechConfig.FromSubscription(this.SubscriptionKey, this.Region);
            config.SpeechRecognitionLanguage = this.RecognitionLanguage;
            config.EndpointId = this.CustomModelEndpointId;

            SpeechRecognizer customRecognizer;
            if (this.UseMicrophone)
            {
                using (customRecognizer = new SpeechRecognizer(config))
                {
                    await this.RunRecognizer(customRecognizer, RecoType.Custom, stopCustomRecognitionTaskCompletionSource).ConfigureAwait(false);
                }
            }
            else
            {
                using (var audioInput = AudioConfig.FromWavFileInput(wavFileName))
                {
                    using (customRecognizer = new SpeechRecognizer(config, audioInput))
                    {
                        await this.RunRecognizer(customRecognizer, RecoType.Custom, stopCustomRecognitionTaskCompletionSource).ConfigureAwait(false);
                    }
               }
            }
        }

        /// <summary>
        /// Subscribes to Recognition Events
        /// Starts the Recognition and waits until final result is received, then Stops recognition
        /// </summary>
        /// <param name="recognizer">Recognizer object</param>
        /// <param name="recoType">Type of Recognizer</param>
        ///  <value>
        ///   <c>Base</c> if Baseline model; otherwise, <c>Custom</c>.
        /// </value>
        private async Task RunRecognizer(SpeechRecognizer recognizer, RecoType recoType, TaskCompletionSource<int> source)
        {
            //subscribe to events
            bool isChecked = false;
            this.Dispatcher.Invoke(() =>
            {
                isChecked = this.immediateResultsCheckBox.IsChecked == true;
            });

            EventHandler<SpeechRecognitionEventArgs> recognizingHandler = (sender, e) => RecognizingEventHandler(e, recoType);
            if (isChecked)
            {
                recognizer.Recognizing += recognizingHandler;
            }

            EventHandler<SpeechRecognitionEventArgs> recognizedHandler = (sender, e) => RecognizedEventHandler(e, recoType);
            EventHandler<SpeechRecognitionCanceledEventArgs> canceledHandler = (sender, e) => CanceledEventHandler(e, recoType, source);
            EventHandler<SessionEventArgs> sessionStartedHandler = (sender, e) => SessionStartedEventHandler(e, recoType);
            EventHandler<SessionEventArgs> sessionStoppedHandler = (sender, e) => SessionStoppedEventHandler(e, recoType, source);
            EventHandler<RecognitionEventArgs> speechStartDetectedHandler = (sender, e) => SpeechDetectedEventHandler(e, recoType, "start");
            EventHandler<RecognitionEventArgs> speechEndDetectedHandler = (sender, e) => SpeechDetectedEventHandler(e, recoType, "end");

            recognizer.Recognized += recognizedHandler;
            recognizer.Canceled += canceledHandler;
            recognizer.SessionStarted += sessionStartedHandler;
            recognizer.SessionStopped += sessionStoppedHandler;
            recognizer.SpeechStartDetected -= speechStartDetectedHandler;
            recognizer.SpeechEndDetected -= speechEndDetectedHandler;

            //start,wait,stop recognition
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            await source.Task.ConfigureAwait(false);
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);

            this.EnableButtons();

            // unsubscribe from events
            if (isChecked)
            {
                recognizer.Recognizing -= recognizingHandler;
            }
            recognizer.Recognized -= recognizedHandler;
            recognizer.Canceled -= canceledHandler;
            recognizer.SessionStarted -= sessionStartedHandler;
            recognizer.SessionStopped -= sessionStoppedHandler;
            recognizer.SpeechStartDetected -= speechStartDetectedHandler;
            recognizer.SpeechEndDetected -= speechEndDetectedHandler;
        }

        #region Recognition Event Handlers

        /// <summary>
        /// Logs intermediate recognition results
        /// </summary>
        private void RecognizingEventHandler(SpeechRecognitionEventArgs e, RecoType rt)
        {
            var log = (rt == RecoType.Base) ? this.baseModelLogText : this.customModelLogText;
            this.WriteLine(log, "Intermediate result: {0} ", e.Result.Text);

            if (rt == RecoType.Base)
            {
                log = this.baseModelLogText;
                this.SetCurrentText(this.baseModelCurrentText, e.Result.Text);
            }
            else
            {
                log = this.customModelLogText;
                this.SetCurrentText(this.customModelCurrentText, e.Result.Text);
            }
        }

        /// <summary>
        /// Logs the final recognition result
        /// </summary>
        private void RecognizedEventHandler(SpeechRecognitionEventArgs e, RecoType rt)
        {
            TextBox log;
            if (rt == RecoType.Base)
            {
                log = this.baseModelLogText;
                this.SetCurrentText(this.baseModelCurrentText, e.Result.Text);
            }
            else
            {
                log = this.customModelLogText;
                this.SetCurrentText(this.customModelCurrentText, e.Result.Text);
            }

            this.WriteLine(log);
            this.WriteLine(log, $" --- Final result received. Reason: {e.Result.Reason.ToString()}. --- ");
            if (!string.IsNullOrEmpty(e.Result.Text))
            {
                //write log
                this.WriteLine(log, e.Result.Text);

                //write result
                this.WriteLine(this.recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Me: {e.Result.Text}");
                SendMessageToBot(e.Result.Text);
            }
        }

        /// <summary>
        /// Logs Canceled events
        /// And sets the TaskCompletionSource to 0, in order to trigger Recognition Stop
        /// </summary>
        private void CanceledEventHandler(SpeechRecognitionCanceledEventArgs e, RecoType rt, TaskCompletionSource<int> source)
        {
            var log = (rt == RecoType.Base) ? this.baseModelLogText : this.customModelLogText;
            source.TrySetResult(0);
            this.WriteLine(log, "--- recognition canceled ---");
            this.WriteLine(log, $"CancellationReason: {e.Reason.ToString()}. ErrorDetails: {e.ErrorDetails}.");
            this.WriteLine(log);
        }

        /// <summary>
        /// Session started event handler.
        /// </summary>
        private void SessionStartedEventHandler(SessionEventArgs e, RecoType rt)
        {
            var log = (rt == RecoType.Base) ? this.baseModelLogText : this.customModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "Speech recognition: Session started event: {0}.", e.ToString()));
        }

        /// <summary>
        /// Session stopped event handler. Set the TaskCompletionSource to 0, in order to trigger Recognition Stop
        /// </summary>
        private void SessionStoppedEventHandler(SessionEventArgs e, RecoType rt, TaskCompletionSource<int> source)
        {
            var log = (rt == RecoType.Base) ? this.baseModelLogText : this.customModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "Speech recognition: Session stopped event: {0}.", e.ToString()));
            source.TrySetResult(0);
        }

        private void SpeechDetectedEventHandler(RecognitionEventArgs e, RecoType rt, string eventType)
        {
            var log = (rt == RecoType.Base) ? this.baseModelLogText : this.customModelLogText;
            this.WriteLine(log, String.Format(CultureInfo.InvariantCulture, "Speech recognition: Speech {0} detected event: {1}.",
                eventType, e.ToString()));
        }

        #endregion

        #region Helper Functions
        /// <summary>
        /// Retrieves Key from File
        /// </summary>
        /// <param name="fileName">Name of file which contains key</param>
        private string GetValueFromIsolatedStorage(string fileName)
        {
            string value = null;
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
            {
                try
                {
                    using (var iStream = new IsolatedStorageFileStream(fileName, FileMode.Open, isoStore))
                    {
                        using (var reader = new StreamReader(iStream))
                        {
                            value = reader.ReadLine();
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    value = null;
                }
            }

            return value;
        }

        /// <summary>
        /// Writes Key to File
        /// </summary>
        /// <param name="fileName">Name of file where key should be stored</param>
        /// <param name="key">The key which should be stored</param>
        private static void SaveKeyToIsolatedStorage(string fileName, string key)
        {
            if (fileName != null && key != null)
            {
                using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null))
                {
                    using (var oStream = new IsolatedStorageFileStream(fileName, FileMode.Create, isoStore))
                    {
                        using (var writer = new StreamWriter(oStream))
                        {
                            writer.WriteLine(key);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save Button Click triggers the three entered keys to be saved to their corresponding files.
        /// </summary>
        private void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveKeyToIsolatedStorage(subscriptionKeyFileName, this.SubscriptionKey);
                SaveKeyToIsolatedStorage(endpointIdFileName, this.CustomModelEndpointId);
                MessageBox.Show("Keys are saved to your disk.\nYou do not need to paste it next time.", "Keys");
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Fail to save Keys. Error message: " + exception.Message,
                    "Keys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Checks if keys are valid
        /// </summary>
        private bool AreKeysValid()
        {
            if (this.subscriptionKey == null || this.subscriptionKey.Length <= 0 ||
                ((this.UseCustomModel || this.UseBaseAndCustomModels) && (this.endpointId == null || this.endpointId.Length <= 0)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if specified audio file exists and returns it
        /// </summary>
        public string GetFile()
        {
            string filePath = "";
            this.Dispatcher.Invoke(() =>
            {
                filePath = this.fileNameTextBox.Text;
            });
            if (!File.Exists(filePath))
            {
                MessageBox.Show("File does not exist!");
                this.WriteLine(this.baseModelLogText, "--- Error : File does not exist! ---");
                this.WriteLine(this.customModelLogText, "--- Error : File does not exist! ---");
                this.EnableButtons();
                return "";
            }
            return filePath;
        }

        /// <summary>
        /// Plays the audio file
        /// </summary>
        private void PlayAudioFile()
        {
            SoundPlayer player = new SoundPlayer(wavFileName);
            player.Load();
            player.Play();
        }

        /// <summary>
        /// Logs the recognition start.
        /// </summary>
        private void LogRecognitionStart(TextBox log, TextBlock currentText)
        {
            string recoSource;
            recoSource = this.UseMicrophone ? "microphone" : "wav file";

            this.SetCurrentText(currentText, string.Empty);
            log.Clear();
            this.WriteLine(log, "\n--- Start speech recognition using " + recoSource + " in " + defaultLocale + " language ----\n\n");
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        private void WriteLine(TextBox log)
        {
            this.WriteLine(log, string.Empty);
        }

        private void WriteLine(TextBox log, string format, params object[] args)
        {
            var formattedStr = string.Format(CultureInfo.InvariantCulture, format, args);
            Trace.WriteLine(formattedStr);
            this.Dispatcher.Invoke(() =>
            {
                log.AppendText((formattedStr + "\n"));
                log.ScrollToEnd();
            });
        }

        private void SetCurrentText(TextBlock textBlock, string text)
        {
            this.Dispatcher.Invoke(() =>
            {
                textBlock.Text = text;
            });
        }

        /// <summary>
        /// Helper function for INotifyPropertyChanged interface
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="caller">Property name</param>
        private void OnPropertyChanged<T>([CallerMemberName]string caller = null)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(caller));
            }
        }

        private void RadioButton_Click(object sender, RoutedEventArgs e)
        {
            this.EnableButtons();
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            Forms.FileDialog fileDialog = new Forms.OpenFileDialog();
            fileDialog.ShowDialog();
            this.fileNameTextBox.Text = fileDialog.FileName;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://azure.microsoft.com/en-us/services/cognitive-services/");
        }

        private void EnableButtons()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.startButton.IsEnabled = true;
                this.radioGroup.IsEnabled = true;
                this.optionPanel.IsEnabled = true;
                this.selectFileButton.IsEnabled = this.UseFileInput;
                this.fileNameTextBox.IsEnabled = this.UseFileInput;
            });
        }
        #endregion

        #region Events
        /// <summary>
        /// Implement INotifyPropertyChanged interface
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the System.Windows.Window.Closed event.
        /// </summary>
        /// <param name="e">An System.EventArgs that contains the event data.</param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }

        #endregion Events

        #region DirectLine

        DirectLineClient directLineClient = null;
        Conversation conversation = null;
        string watermark = null;
        string userId = null;
        CancellationTokenSource cts = null;
        System.Collections.Queue sendMessageQueue = new System.Collections.Queue();

        /// <summary>
        /// 发送消息给Bot
        /// </summary>
        /// <param name="content">内容</param>
        void SendMessageToBot(string content)
        {
            if (directLineClient != null || luisClient != null)
            {
                sendMessageQueue.Enqueue(content);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendMessageToBot(txtSendContent.Text.Trim());
            WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] User：{txtSendContent.Text.Trim()}");
            txtSendContent.Text = "";
            
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cts == null)
                {
                    cts = new CancellationTokenSource();
                }

                if (UseDirectLineBot)
                {
                    if (!string.IsNullOrEmpty(directLineKey))
                    {
                        if (directLineClient == null)
                        {
                            directLineClient = new DirectLineClient(directLineKey);
                        }
                        if (conversation == null)
                        {
                            conversation = await directLineClient.Conversations.StartConversationAsync();
                        }
                    }
                    userId = Guid.NewGuid().ToString();
                    WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Suceceful to connect direct service, you can start a conversation now. User Id:{userId}");
                }

                if (UseLuisBot)
                {
                    if (luisClient == null)
                    {
                        luisClient = new LUISRuntimeClient(new ApiKeyServiceClientCredentials(this.luisSubscriptionKey));
                        luisClient.Endpoint = this.luisEndPoint;
                    }

                    WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Suceceful to connect luis service, you can start a conversation now. End Point:{luisClient.Endpoint}");
                }

                //recognizedText.Clear();
                btnStart.Visibility = Visibility.Collapsed;
                btnStop.Visibility = Visibility.Visible;
                btnSend.IsEnabled = true;
                txtSendContent.IsEnabled = true;

                //start a thread as async call
                CheckBotMessageAsync();
            }
            catch (Exception exc)
            {
                WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Exception: {exc.Message}");
            }

        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //结束会话
                var endOfConversationMessage = new Activity
                {
                    From = new ChannelAccount(userId),
                    Type = ActivityTypes.EndOfConversation
                };

                if (directLineClient != null)
                {
                    await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, endOfConversationMessage, CancellationToken.None);
                    directLineClient = null;
                    conversation = null;
                }

                if (luisClient != null)
                {
                    luisClient = null;
                }

                cts.Cancel();
                cts = null;

                recognizedText.Clear();
                btnStart.Visibility = Visibility.Visible;
                btnStop.Visibility = Visibility.Collapsed;
                btnSend.IsEnabled = false;
                txtSendContent.IsEnabled = false;
            }
            catch (Exception exc)
            {
                WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Exception: {exc.Message}");
            }
        }

        async Task ReplyFromDirectLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            var userMessage = new Activity
            {
                From = new ChannelAccount(userId),
                Text = message,
                Type = ActivityTypes.Message
            };

            await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, userMessage, CancellationToken.None);
            var activitySet = await directLineClient.Conversations.GetActivitiesAsync(conversation.ConversationId, watermark, CancellationToken.None);
            watermark = activitySet?.Watermark;

            var activities = from x in activitySet.Activities
                             where x.From.Id == "CIBCustomerBotPoc"
                             select x;
            foreach (Activity activity in activities)
            {
                //输出bot返回的文本
                if (!string.IsNullOrEmpty(activity.Text))
                {
                    WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Bot: {activity.Text}");
                    await SynthesizeText(activity.Text);
                }
            }
        }

        async Task CheckBotMessageAsync()
        {
            bool bAlive = true;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    if (sendMessageQueue.Count > 0)
                    {
                        var sendContent = sendMessageQueue.Dequeue().ToString();

                        if (directLineClient != null && this.UseDirectLineBot)
                        {
                            await ReplyFromDirectLine(sendContent);
                        }

                        if (luisClient != null && this.UseLuisBot)
                        {
                            await ReplyFromLuis(sendContent);
                        }
                    }

                    bAlive = true;
                }
                catch (Exception exc)
                {
                    if (bAlive)
                    {
                        WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Exception: {exc.Message}");
                        bAlive = false;    
                    }
                }
                finally
                {
                    await Task.Delay(100);
                }
            }
        }

        private void TxtSendContent_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Return)
            {
                BtnSend_Click(null, null);
            }
        }

        #endregion

        #region LuisBot

        ILUISRuntimeClient luisClient = null;
        string luisSubscriptionKey = "a7cee06b63d44cc7a0660518bf997394";
        string luisEndPoint = "https://eastasia.api.cognitive.microsoft.com";
        string luisApplicationId = "5a2a656f-8a1c-4f5e-ad97-7221e3e28b76";

        /// <summary>
        /// Timer event handler.
        /// </summary>
        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            // code goes here
            if (deviceStatus != null)
            {
                string status = string.Format("玛塔: {0}\n照明: {1}\n投影: {2}\n空调: {3}度",
                                                deviceStatus.IsBotAwake ? "On" : "Off",
                                                deviceStatus.IsLightOn ? "On" : "Off",
                                                deviceStatus.IsProjectorOn ? "On" : "Off",
                                                deviceStatus.CoolerTemperature);

                if (status != this.deviceStatusText.Text)
                {
                    this.SetCurrentText(this.deviceStatusText, status);
                }
            }
        }

        /// <summary>
        /// Identify whether the bot is awake
        /// </summary>
        bool IsBotAwake(IList<EntityModel> entityList)
        {
            deviceStatus.TempDegree = 1;

            foreach (var entity in entityList)
            {
                if (entity.Type == "ActivationWord")
                {
                    deviceStatus.IsBotAwake = true;
                }
                else if (entity.Type == "builtin.temperature")
                {
                    deviceStatus.TempDegree = DeviceStatus.ParserNumberFromText(entity.Entity, 1);
                }
            }

            return deviceStatus.IsBotAwake;
        }

        /// <summary>
        /// Luis Reply
        /// </summary>
        async Task ReplyFromLuis(string message)
        {
            var result = await luisClient.Prediction.ResolveAsync(this.luisApplicationId, message);
            var reply = "";

            if (result.TopScoringIntent.Score < 0.2)
            {
                deviceStatus.IsBotAwake = false;
                return;
            }

            switch (result.TopScoringIntent.Intent.Trim())
            {
                case "Status.Activate":
                    reply = "您好，我是玛塔，请问有什么可以为您服务的？";
                    deviceStatus.IsBotAwake = IsBotAwake(result.Entities);
                    break;

                case "Light.TurnOn":
                    if (!IsBotAwake(result.Entities)) return;
                    if (deviceStatus.IsLightOn) reply = "您好，目前灯光已开启。";
                    else
                    {
                        deviceStatus.IsLightOn = true;
                        reply = "您好，下面将为您开灯。";
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "Light.TurnOff":
                    if (!IsBotAwake(result.Entities)) return;
                    if (!deviceStatus.IsLightOn) reply = "您好，目前灯光已关闭。";
                    else
                    {
                        deviceStatus.IsLightOn = false;
                        reply = "您好，下面将为您关灯。";
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "Projector.TurnOn":
                    if (!IsBotAwake(result.Entities)) return;
                    if (deviceStatus.IsProjectorOn) reply = "您好，目前投影已开启。";
                    else
                    {
                        deviceStatus.IsProjectorOn = true;
                        reply = "您好，下面将为您打开投影。";
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "Projector.TurnOff":
                    if (!IsBotAwake(result.Entities)) return;
                    if (!deviceStatus.IsProjectorOn) reply = "您好，目前投影已关闭。";
                    else
                    {
                        deviceStatus.IsProjectorOn = false;
                        reply = "您好，下面将为您关闭投影。";
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "Temperature.Increase":
                    if (!IsBotAwake(result.Entities)) return;
                    else
                    {
                        if (deviceStatus.TempDegree < 10)
                        {
                            deviceStatus.CoolerTemperature += deviceStatus.TempDegree;
                            reply = string.Format("您好，下面将为您将空调温度提高{0}度。", deviceStatus.TempDegree);
                        }
                        else
                        {
                            deviceStatus.CoolerTemperature = deviceStatus.TempDegree;
                            reply = string.Format("您好，下面将为您将空调温度设为{0}度。", deviceStatus.CoolerTemperature);
                        }
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "Temperature.Decrease":
                    if (!IsBotAwake(result.Entities)) return;
                    else
                    {
                        if (deviceStatus.TempDegree < 10)
                        {
                            deviceStatus.CoolerTemperature -= deviceStatus.TempDegree;
                            reply = string.Format("您好，下面将为您将空调温度降低{0}度。", deviceStatus.TempDegree);
                        }
                        else
                        {
                            deviceStatus.CoolerTemperature = deviceStatus.TempDegree;
                            reply = string.Format("您好，下面将为您将空调温度设为{0}度。", deviceStatus.CoolerTemperature);
                        }
                    }
                    deviceStatus.IsBotAwake = false;
                    break;

                case "None":
                    deviceStatus.IsBotAwake = false;
                    break;
            }

            //输出bot返回的文本
            if (!string.IsNullOrEmpty(reply))
            {
                WriteLine(recognizedText, $"[{DateTime.Now.ToString("HH:mm:ss")}] Marta: {reply}");
                await SynthesizeText(reply);
            }
        }

        #endregion
    }
}
