﻿using MongoDB.Bson;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ssi
{
    /// <summary>
    /// Interaction logic for DatabaseEditSubjectWindow.xaml
    /// </summary>
    public partial class DatabaseAdminSubjectWindow : Window
    {
        private MongoClient mongo;
        private IMongoDatabase database;
        private string connectionstring = "mongodb://127.0.0.1:27017";

        public DatabaseAdminSubjectWindow(string name = null, string gender = null, string age = null, string culture = null)
        {
            InitializeComponent();

            connectionstring = "mongodb://" + Properties.Settings.Default.MongoDBUser + ":" + Properties.Settings.Default.MongoDBPass + "@" + Properties.Settings.Default.DatabaseAddress;
            mongo = new MongoClient(connectionstring);
            database = mongo.GetDatabase(Properties.Settings.Default.DatabaseName);
            var session = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);

            List<BsonDocument> documents;
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("name", name);
            documents = session.Find(filter).ToList();

            if (name != null) Namefield.Text = name;

            foreach (var item in Genderbox.Items)
            {
                if (gender != null && item.ToString().Contains(gender))
                    Genderbox.SelectedItem = item;
            }

            if (age != null) Agefield.Text = age;
            if (culture != null) Culturefield.Text = culture;
        }

        public string SubjectName()
        {
            return Namefield.Text;
        }

        public string Gender()
        {
            return Genderbox.SelectionBoxItem.ToString();
        }

        public string Age()
        {
            return Agefield.Text;
        }

        public string Culture()
        {
            return Culturefield.Text;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}