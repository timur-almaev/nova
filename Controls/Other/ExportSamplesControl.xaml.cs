﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ssi
{
    /// <summary>
    /// Interaction logic for ExportSamplesControl.xaml
    /// </summary>
    public partial class ExportSamplesControl : UserControl
    {
        public delegate void CloseEvent();

        public event CloseEvent closeEvent;

        public ExportSamplesControl()
        {
            InitializeComponent();

            selectButton.Click += selectButton_Click;
            unselectButton.Click += unselectButton_Click;
            selectAllButton.Click += selectAllButton_Click;
            unselectAllButton.Click += unselectAllButton_Click;
            exportButton.Click += exportButton_Click;
            cancelButton.Click += cancelButton_Click;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (closeEvent != null)
            {
                closeEvent();
            }
        }

        private void exportButton_Click(object sender, RoutedEventArgs e)
        {
            exportSSISampleList();

            if (closeEvent != null)
            {
                closeEvent();
            }
        }

        private void selectAllButton_Click(object sender, RoutedEventArgs e)
        {
            while (!signalAvailableListBox.Items.IsEmpty)
            {
                var item = signalAvailableListBox.Items[0];
                signalAvailableListBox.Items.Remove(item);
                signalSelectedListBox.Items.Add(item);
            }
        }

        private void selectButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = signalAvailableListBox.SelectedItem;
            if (selected != null)
            {
                signalAvailableListBox.Items.Remove(selected);
                signalSelectedListBox.Items.Add(selected);
            }
        }

        private void unselectButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = signalSelectedListBox.SelectedItem;
            if (selected != null)
            {
                signalSelectedListBox.Items.Remove(selected);
                signalAvailableListBox.Items.Add(selected);
            }
        }

        private void unselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            while (!signalSelectedListBox.Items.IsEmpty)
            {
                var item = signalSelectedListBox.Items[0];
                signalSelectedListBox.Items.Remove(item);
                signalAvailableListBox.Items.Add(item);
            }
        }

        private void exportSSISampleList()
        {
            String userName = userNameTextBox.Text;
            String annoPath = (String)annoComboBox.SelectedItem;
            ItemCollection signalItems = signalSelectedListBox.Items;

            if (userName.Length == 0 || annoPath == null || signalItems.IsEmpty)
            {
                MessageTools.Error("Select a user name and annotation and at least one signal.");
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.InitialDirectory = Path.GetDirectoryName(annoPath);
            dialog.Filter = "Sample files (*.samples)|*.samples|All files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                string samplesPath = dialog.FileName;
                StringBuilder signalPaths = new StringBuilder();
                bool first = true;
                foreach (string item in signalItems)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        signalPaths.Append(';');
                    }
                    signalPaths.Append(item);
                }

                if(!File.Exists("anno2samp.exe"))
                {
                    try
                    {
                        string url = "https://github.com/hcmlab/nova/blob/master/bin/anno2samp.exe?raw=true";
                        WebClient Client = new WebClient();
                        Client.DownloadFile(url, AppDomain.CurrentDomain.BaseDirectory + "anno2samp.exe");
                    }
                    catch
                    {
                        MessageBox.Show("Tools for creating Samplelists are not available, please check your internet connection.");
                        return;
                    }
                  
                }




                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                startInfo.FileName = "anno2samp.exe";
                startInfo.Arguments = userName + " " + annoPath + " " + signalPaths + " " + samplesPath;
                if (continuousCheckBox.IsChecked == true)
                {
                    startInfo.Arguments += " -frame " + frameTextBox.Text + " -delta " + deltaTextBox.Text + " -percent " + percentTextBox.Text;
                    if (labelTextBox.Text.Length > 0)
                    {
                        startInfo.Arguments += " -label " + labelTextBox.Text;
                    }

                    if(!annoPath.Contains(".annotation")) startInfo.Arguments += " -legacy";


                }
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show("Samplelist successfully created in " + samplesPath);
                }

            }
        }
    }
}