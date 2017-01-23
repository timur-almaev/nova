﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ssi
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainHandler viewh = null;

        public MainWindow()
        {
            InitializeComponent();

            this.view.OnHandlerLoaded += viewHandlerLoaded;
            CultureInfo ci = new CultureInfo("en-GB");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.Title = "(NOn)Verbal Annotator | v" + MainHandler.BuildVersion + " | HCM-Lab, Augsburg University | http://openssi.net";
        }

        private void viewHandlerLoaded(MainHandler handler)
        {
            this.viewh = handler;
            this.viewh.LoadButton.Click += loadButton_Click;
            this.KeyDown += OnKeyDown;
            this.PreviewKeyDown += handler.OnPreviewKeyDown;
            this.KeyDown += handler.OnKeyDown;
            this.KeyUp += handler.OnKeyUp;

            HandleClArgs(Environment.GetCommandLineArgs());

            ((App)Application.Current).ReceiveExternalClArgs += (app, clArgs) =>
            {
                HandleClArgs(clArgs.Args);
            };
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.LeftAlt) && e.KeyboardDevice.IsKeyDown(Key.Enter))
            {
                if (this.WindowStyle != WindowStyle.None)
                {
                    this.WindowStyle = WindowStyle.None;
                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;
                }
            }
            else if (e.KeyboardDevice.IsKeyDown(Key.Escape))
            {
                if (this.WindowStyle != WindowStyle.SingleBorderWindow)
                {
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                    this.WindowState = WindowState.Normal;
                }
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            this.viewh.loadFiles();
        }

        private void HandleClArgs(IList<string> args)
        {
            if (args.Count <= 1) return;
            for (var i = 1; i < args.Count; i++)
            {
                try
                {
                    this.viewh.loadMultipleFiles(new[] { args[i] });
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }



        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult mbr =  MessageBox.Show("Are you sure you want to close the Application?", "Question", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (mbr == MessageBoxResult.Yes) viewh.clearSession();
            else e.Cancel = true;
        }
    }
}