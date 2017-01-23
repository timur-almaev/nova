﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ssi
{
    /// <summary>
    /// Interaction logic for AnnoTierNewPointSchemeWindow.xaml
    /// </summary>
    public partial class AnnoTierNewPointSchemeWindow : Window
    {
        AnnoScheme result;
        public AnnoScheme Result { get { return result; } }

        public class Input
        {
            public double SampleRate { get; set; }
            public double NumPoints { get; set; }
            public Color Color { get; set; }
        }

        public AnnoTierNewPointSchemeWindow(Input defaultInput)
        {
            InitializeComponent();

            result = new AnnoScheme();
            result.Type = AnnoScheme.TYPE.POINT;
            result.SampleRate = defaultInput.SampleRate;

            result.MinScore = defaultInput.NumPoints;
            result.MinOrBackColor = defaultInput.Color;

            srTextBox.Text = defaultInput.SampleRate.ToString();
            numPointsTextBox.Text = defaultInput.NumPoints.ToString();
            colorPicker.SelectedColor = defaultInput.Color;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;

            double value;
            if (double.TryParse(srTextBox.Text, out value))
            {
                result.SampleRate = value;
            }
            if (double.TryParse(numPointsTextBox.Text, out value))
            {
                result.MinScore = value;
            }
            result.MinOrBackColor = colorPicker.SelectedColor.Value;

            Close();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
