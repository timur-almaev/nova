﻿using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Windows;

namespace ssi
{
    /// <summary>
    /// Interaktionslogik für DataBaseSessionWindow.xaml
    /// </summary>
    public partial class DatabaseAdminSessionWindow : Window
    {
        private MongoClient mongo;
        private IMongoDatabase database;
        private string connectionstring = "mongodb://127.0.0.1:27017";

        public DatabaseAdminSessionWindow(string name = null, string language = null, string location = null, BsonDateTime date = null)
        {
            InitializeComponent();

            connectionstring = "mongodb://" + Properties.Settings.Default.MongoDBUser + ":" + Properties.Settings.Default.MongoDBPass + "@" + Properties.Settings.Default.DatabaseAddress;
            mongo = new MongoClient(connectionstring);
            database = mongo.GetDatabase(Properties.Settings.Default.DatabaseName);
            var session = database.GetCollection<BsonDocument>(Properties.Settings.Default.LastSessionId);

            if (name != null) Namefield.Text = name;

            foreach (var item in LanguageField.Items)
            {
                if (language != null && item.ToString().Contains(language))
                    LanguageField.SelectedItem = item;
            }

            if (location != null) LocationField.Text = location;
            if (date != null) datepicker.SelectedDate = date.AsDateTime;
            else datepicker.SelectedDate = DateTime.Now;
        }

        public string SessionName()
        {
            return Namefield.Text;
        }

        public string SessionLanguage()
        {
            return LanguageField.SelectionBoxItem.ToString();
        }

        public string SessionLocation()
        {
            return LocationField.Text;
        }

        public DateTime SessionDate()
        {
            if (datepicker.SelectedDate != null)
                return datepicker.SelectedDate.Value;
            else return new DateTime();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}