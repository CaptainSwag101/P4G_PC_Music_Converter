using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace P4G_PC_Music_Converter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string AdpcmEncoderPath = "tools/AdpcmEncode.exe";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void EncodeButton_Click(object sender, RoutedEventArgs e)
        {
            // Check to ensure all the necessary fields are filled out before we proceed
            bool fieldsPopulated = !string.IsNullOrWhiteSpace(InputWavPath.Text) &&
                !string.IsNullOrWhiteSpace(OutputRawPath.Text) &&
                (!LoopEnable.IsChecked.Value || (!string.IsNullOrWhiteSpace(LoopStart.Text)) && !string.IsNullOrWhiteSpace(LoopEnd.Text));

            if (!fieldsPopulated)
            {
                MessageBox.Show("Not all required fields have been filled out!\n" +
                    "Make sure that both input and output file paths are specified,\n" +
                    "and if looping is enabled, make sure the start and end point are provided.");
                return;
            }

            // Verify that the input file exists
            if (!File.Exists(InputWavPath.Text))
            {
                MessageBox.Show("The specified input file does not exist!");
                return;
            }

            // If looping is enabled, ensure that the loop start and end textboxes contain valid numbers
            if (LoopEnable.IsChecked.Value)
            {
                if (!int.TryParse(LoopStart.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int _) ||
                    int.Parse(LoopStart.Text, NumberStyles.Integer) < 0)
                {
                    MessageBox.Show("Unable to parse the loop start point, make sure it's a valid positive integer!");
                    return;
                }

                if (!int.TryParse(LoopEnd.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int _) ||
                    int.Parse(LoopEnd.Text, NumberStyles.Integer) < 0)
                {
                    MessageBox.Show("Unable to parse the loop end point, make sure it's a valid positive integer!");
                    return;
                }
            }

            // Encode the input file using the MSADPCM tool
            if (!File.Exists(AdpcmEncoderPath))
            {
                MessageBox.Show($"Unable to find MSADPCM encoder tool! It should be located in {AdpcmEncoderPath}");
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(AdpcmEncoderPath, $"\"{InputWavPath.Text}\" \"{OutputRawPath.Text}\"");
            var encodeProcess = Process.Start(startInfo);
            encodeProcess.WaitForExit();
            int exit = encodeProcess.ExitCode;

            if (exit != 0)
            {
                MessageBox.Show($"The MSADPCM encoder tool exited with code {exit}. This indicates an error. Aborting...");
                return;
            }

            // Read the input file and determine/verify its info (sample count, etc.)
            using (WaveFileReader waveReader = new WaveFileReader(OutputRawPath.Text))
            {
                // Create a StringBuilder to output the info about this file to our TextBlock
                StringBuilder outputInfoBuilder = new StringBuilder();

                // We can't get the sample count from the ADPCM encoded wave, so we need to use the original input file
                long sampleCount = -1;
                using (WaveFileReader originalWaveReader = new WaveFileReader(InputWavPath.Text))
                {
                    sampleCount = originalWaveReader.SampleCount;
                }

                outputInfoBuilder.Append($"num_samples = {sampleCount}\n");
                outputInfoBuilder.Append($"codec = MSADPCM\n");
                outputInfoBuilder.Append($"channels = {waveReader.WaveFormat.Channels}\n");
                outputInfoBuilder.Append($"sample_rate = {waveReader.WaveFormat.SampleRate}\n");
                outputInfoBuilder.Append($"interleave = {waveReader.WaveFormat.BlockAlign}\n");

                if (LoopEnable.IsChecked.Value)
                {
                    int loopStart = int.Parse(LoopStart.Text);
                    if (loopStart > sampleCount)
                    {
                        MessageBox.Show("The loop start point is out of bounds.");
                        return;
                    }

                    int loopEnd = int.Parse(LoopEnd.Text);
                    if (loopEnd > sampleCount)
                    {
                        MessageBox.Show("The loop end point is out of bounds.");
                        return;
                    }

                    outputInfoBuilder.Append($"loop_start_sample = {loopStart}\n");
                    outputInfoBuilder.Append($"loop_end_sample = {loopEnd}\n");
                }

                OutputFileInfo.Text = outputInfoBuilder.ToString();

                // Setup an output TXTH file in the same location as the output RAW file
                File.WriteAllText(OutputRawPath.Text + ".txth", outputInfoBuilder.ToString());
            }

            // Strip the first 0x4E bytes (RIFF header)
            byte[] waveData = File.ReadAllBytes(OutputRawPath.Text);
            byte[] strippedWaveData = new byte[waveData.Length - 0x4E];
            Array.Copy(waveData, 0x4E, strippedWaveData, 0, waveData.Length - 0x4E);
            File.WriteAllBytes(OutputRawPath.Text, strippedWaveData);
        }

        private void BrowseInputFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "WAV audio files (*.wav)|*.wav|All files (*.*)|*.*";

            // Abort if the user canceled the dialog
            if (!dlg.ShowDialog().Value)
            {
                return;
            }

            InputWavPath.Text = dlg.FileName;

            // If there is nothing in the output file path textbox, populate it with an automatic output file in the same location
            if (string.IsNullOrWhiteSpace(OutputRawPath.Text))
            {
                FileInfo temp = new FileInfo(dlg.FileName);
                OutputRawPath.Text = temp.FullName.Substring(0, temp.FullName.Length - temp.Extension.Length) + ".raw";
            }
        }

        private void BrowseOutputFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "RAW audio files (*.raw)|*.raw|All files (*.*)|*.*";

            // Abort if the user canceled the dialog
            if (!dlg.ShowDialog().Value)
            {
                return;
            }

            OutputRawPath.Text = dlg.FileName;
        }

        private void LoopEnable_Checked(object sender, RoutedEventArgs e)
        {
            LoopStart.IsEnabled = true;
            LoopEnd.IsEnabled = true;
        }

        private void LoopEnable_Unchecked(object sender, RoutedEventArgs e)
        {
            LoopStart.IsEnabled = false;
            LoopEnd.IsEnabled = false;
        }
    }
}
