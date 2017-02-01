﻿using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ssi
{
    /// <summary>
    /// Interaktionslogik für DatabaseAdminWindow.xaml
    /// </summary>
    public partial class DatabaseAdminMainWindow : Window
    {
        private MongoClient mongo;
        private IMongoDatabase database;
        private string connectionstring = "mongodb://127.0.0.1:27017";
        private int authlevel = 0;
        private string lastRole = "";

        public DatabaseAdminMainWindow()
        {
            InitializeComponent();

            this.db_server.Text = Properties.Settings.Default.DatabaseAddress;
            this.db_login.Text = Properties.Settings.Default.MongoDBUser;
            this.db_pass.Password = Properties.Settings.Default.MongoDBPass;
            Autologin.IsEnabled = false;
            if (Properties.Settings.Default.DatabaseAutoLogin == true)
            {
                Autologin.IsChecked = true;
            }
            else Autologin.IsChecked = false;

            if (Autologin.IsChecked == true)
            {
                ConnecttoDB();
            }
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            DatabaseAdminMediaWindow dbmw = new DatabaseAdminMediaWindow();
            dbmw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dbmw.ShowDialog();

            if (dbmw.DialogResult == true)
            {
                Properties.Settings.Default.DataServerConnectionType = dbmw.Type();
                Properties.Settings.Default.Filenames = dbmw.Files();
                Properties.Settings.Default.DataServer = dbmw.Server(); ;
                Properties.Settings.Default.DataServerFolder = dbmw.Folder(); ;
                bool requiresAuth = dbmw.Auth();

                string[] fnames = Properties.Settings.Default.Filenames.Split(';');
                AddMediatoDatabase(Properties.Settings.Default.DataServerConnectionType, Properties.Settings.Default.DatabaseName, Properties.Settings.Default.LastSessionId, Properties.Settings.Default.DataServer, Properties.Settings.Default.DataServerFolder, fnames, requiresAuth);
            }
        }

        public void AddMediatoDatabase(string connection, string db, string session, string ip, string folder, string[] filenames, bool auth = false)
        {
            mongo = new MongoClient(connectionstring);
            database = mongo.GetDatabase(db);

            if (CollectionResultsBox.SelectedItem != null)
            {
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", session) & builder.Eq("connection", connection);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);

                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);

                for (int i = 0; i < filenames.Length; i++)
                {
                    string filename = filenames[i];
                    if (isURL(filenames[i]))
                    {
                        filename = filenames[i].Substring(filenames[i].LastIndexOf("/") + 1, (filenames[i].Length - filenames[i].LastIndexOf("/") - 1));
                    }
                    // string id = media[0]["name"].ToString();

                    string url = "";

                    if (connection == "sftp") url = "sftp://" + ip + folder + "/" + filename;
                    if (connection == "ftp") url = "ftp://" + ip + folder + "/" + filename;

                    if (connection == "http" && auth == false) url = filenames[i];
                    else if (connection == "http" && auth == true) url = ip;

                    BsonDocument b = new BsonDocument
                    {
                                 { "name", filename },
                                 { "url", url },
                                 { "requiresAuth", auth},
                                 { "session_id", sessionid},
                                 { "mediatype_id", "" },
                                 { "role_id", "" },
                                 { "subject_id", "" }
                    };

                    media.InsertOne(b);
                }

                GetMedia();
            }
        }

        private bool isURL(string url)
        {
            if (url.Contains("http://") || url.Contains("https://"))
                return true;
            else return false;
        }

        private void AddRole_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, UserInputWindow.Input> input = new Dictionary<string, UserInputWindow.Input>();
            input["role"] = new UserInputWindow.Input() { Label = "Role", DefaultValue = "" };
            UserInputWindow dialog = new UserInputWindow("Add new role", input);
            dialog.ShowDialog();

            if (dialog.DialogResult == true)
            {
                lastRole = dialog.Result("role");

                BsonArray files = new BsonArray();
                BsonDocument role = new BsonDocument {
                    {"name",  lastRole},
                    {"isValid",  true}
                };

                bool subjectalreadypresent = false;

                var collection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Roles);
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", lastRole);
                var documents = collection.Find(filter).ToList();

                foreach (var item in documents)
                {
                    if (item["name"].ToString() == lastRole)
                    {
                        subjectalreadypresent = true;
                        var update = Builders<BsonDocument>.Update.Set("isValid", true);
                        collection.UpdateOne(filter, update);
                    }
                }
                if (subjectalreadypresent == false)
                {
                    collection.InsertOne(role);
                }

                GetRoles(dialog.Result("role"));
            }
        }

        private void AddSession_Click(object sender, RoutedEventArgs e)
        {
            DatabaseAdminSessionWindow dbsw = new DatabaseAdminSessionWindow();
            dbsw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dbsw.ShowDialog();

            if (dbsw.DialogResult == true)
            {
                Properties.Settings.Default.LastSessionId = dbsw.SessionName();
                Properties.Settings.Default.Save();

                BsonElement name = new BsonElement("name", dbsw.SessionName());
                BsonElement location = new BsonElement("location", dbsw.SessionLocation());
                BsonElement language = new BsonElement("language", dbsw.SessionLanguage());
                BsonElement date = new BsonElement("date", dbsw.SessionDate());
                BsonElement isValid = new BsonElement("isValid", true);
                BsonDocument document = new BsonDocument();

                document.Add(name);
                document.Add(location);
                document.Add(language);
                document.Add(date);
                document.Add(isValid);

                bool sessionnamealreadypresent = false;
                foreach (var item in CollectionResultsBox.Items)
                {
                    if (item.ToString() == dbsw.SessionName())
                    {
                        sessionnamealreadypresent = true;
                    }
                }
                if (sessionnamealreadypresent == false)
                {
                    var collection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
                    collection.InsertOne(document);
                    GetSessions();
                }
                else MessageBox.Show("Session is already present, please select another name!");
            }
        }

        private void AddDB_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, UserInputWindow.Input> input = new Dictionary<string, UserInputWindow.Input>();
            input["name"] = new UserInputWindow.Input() { Label = "Name", DefaultValue = Properties.Settings.Default.DatabaseName };
            input["description"] = new UserInputWindow.Input() { Label = "Description", DefaultValue = "" };
            UserInputWindow dialog = new UserInputWindow("Add new database", input);
            dialog.ShowDialog();

            if (dialog.DialogResult == true)
            {
                Properties.Settings.Default.DatabaseName = dialog.Result("name");
                Properties.Settings.Default.Save();

                BsonDocument meta = new BsonDocument {
                    {"name",  dialog.Result("name")},
                    {"description",  dialog.Result("description")}
                };

                ;
                database = mongo.GetDatabase(Properties.Settings.Default.DatabaseName);
                var collection = database.GetCollection<BsonDocument>("Meta");
                collection.InsertOne(meta);
                GetDatabase();
            }
        }

        private void ConnecttoDB()
        {
            Properties.Settings.Default.DatabaseAddress = this.db_server.Text;
            Properties.Settings.Default.MongoDBUser = this.db_login.Text;
            Properties.Settings.Default.MongoDBPass = this.db_pass.Password;
            Properties.Settings.Default.Save();

            connectionstring = "mongodb://" + Properties.Settings.Default.MongoDBUser + ":" + Properties.Settings.Default.MongoDBPass + "@" + Properties.Settings.Default.DatabaseAddress;

            try
            {
                mongo = new MongoClient(connectionstring);
                int count = 0;
                while (mongo.Cluster.Description.State.ToString() == "Disconnected")
                {
                    Thread.Sleep(100);
                    if (count++ >= 25) throw new MongoException("Unable to connect to the database. Please make sure that " + mongo.Settings.Server.Host + " is online and you entered your credentials correctly!");
                }

                authlevel = DatabaseHandler.CheckAuthentication(this.db_login.Text, "admin");

                if (authlevel > 0)
                {
                    GetDatabase();
                    Autologin.IsEnabled = true;
                }
                else MessageBox.Show("You have no rights to access the database list");

                if (authlevel > 3)
                {
                    DeleteDB.Visibility = Visibility.Visible;
                    AddDB.Visibility = Visibility.Visible;
                }
            }
            catch (MongoException e)

            {
                MessageBox.Show(e.Message);
                mongo.Cluster.Dispose();
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            ConnecttoDB();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        public void GetDatabase()
        {
            DataBasResultsBox.Items.Clear();

            var databases = mongo.ListDatabasesAsync().Result.ToListAsync().Result;
            foreach (var c in databases)
            {
                string db = c.GetElement(0).Value.ToString();
                if (c.GetElement(0).Value.ToString() != "admin" && c.GetElement(0).Value.ToString() != "local" && DatabaseHandler.CheckAuthentication(Properties.Settings.Default.MongoDBUser, db) > 2)
                    DataBasResultsBox.Items.Add(db);
               
            }
        }

        public void GetSessions()

        {
            database = mongo.GetDatabase(Properties.Settings.Default.DatabaseName);

            var sessioncollection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
            var sessions = sessioncollection.Find(_ => true).ToList();

            if (CollectionResultsBox.HasItems) CollectionResultsBox.ItemsSource = null;
            List<DatabaseSession> items = new List<DatabaseSession>();
            foreach (var c in sessions)
            {
                //CollectionResultsBox.Items.Add(c.GetElement(1).Value.ToString());
                items.Add(new DatabaseSession() { Name = c["name"].ToString(), Location = c["location"].ToString(), Language = c["language"].ToString(), Date = c["date"].AsDateTime.ToShortDateString(), OID = c["_id"].AsObjectId });
            }

            CollectionResultsBox.ItemsSource = items;
        }

        public void GetRoles(string selecteditem = null)

        {
            RolesResultBox.Items.Clear();

            List<string> Collections = new List<string>();
            var roles = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Roles);

            var documents = roles.Find(_ => true).ToList();

            foreach (BsonDocument b in documents)
            {
                if (b["isValid"].AsBoolean == true) RolesResultBox.Items.Add(b["name"].ToString());
            }
            RolesResultBox.SelectedItem = selecteditem;
        }

        public void GetAnnotators(string selecteditem = null)

        {
            AnnotatorsBox.Items.Clear();

            List<string> Collections = new List<string>();
            var roles = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators);

            var documents = roles.Find(_ => true).ToList();

            foreach (BsonDocument b in documents)
            {
                AnnotatorsBox.Items.Add(b["fullname"].ToString());
            }
            AnnotatorsBox.SelectedItem = selecteditem;
        }

        public void GetSubjects(string selecteditem = null)

        {
            SubjectsResultBox.Items.Clear();

            List<string> Collections = new List<string>();
            var subjects = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects);

            var documents = subjects.Find(_ => true).ToList();

            foreach (BsonDocument b in documents)
            {
                SubjectsResultBox.Items.Add(b["name"].ToString());
            }
            SubjectsResultBox.SelectedItem = selecteditem;
        }

        public void GetMediaType(string selecteditem = null)

        {
            MediatypeResultsBox.Items.Clear();

            List<string> Collections = new List<string>();
            var mediatype = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.StreamTypes);
            var sessions = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);

            var documents = mediatype.Find(_ => true).ToList();

            foreach (BsonDocument b in documents)
            {
                MediatypeResultsBox.Items.Add(b["name"].ToString() + "#" + b["type"].ToString());
            }
            MediatypeResultsBox.SelectedItem = selecteditem;
        }

        private void GetMedia()
        {
            if (DataBasResultsBox.SelectedItem != null)
            {
                List<DatabaseMediaInfo> ci = new List<DatabaseMediaInfo>();
                MediaResultBox.Items.Clear();
                ci = GetMediafromDB(DataBasResultsBox.SelectedItem.ToString(), Properties.Settings.Default.LastSessionId);

                foreach (DatabaseMediaInfo c in ci)
                {
                    if (!c.filename.Contains(".stream~"))
                    {
                        MediaResultBox.Items.Add(c.filename);
                    }
                }
            }
        }

        public List<DatabaseMediaInfo> GetMediafromDB(string db, string session)
        {
            List<DatabaseMediaInfo> paths = new List<DatabaseMediaInfo>();
            var colllection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
            var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);

            ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);

            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("session_id", sessionid);
            var selectedmedialist = media.Find(filter).ToList();

            foreach (var selectedmedia in selectedmedialist)
            {
                {
                    DatabaseMediaInfo c = new DatabaseMediaInfo();
                    string url = selectedmedia["url"].ToString();
                    string[] split = url.Split(':');
                    c.connection = split[0];

                    if (split[0] == "ftp" || split[0] == "sftp")
                    {
                        string[] split2 = split[1].Split(new char[] { '/' }, 4);
                        c.ip = split2[2];
                        string filename = split2[3].Substring(split2[3].LastIndexOf("/") + 1, (split2[3].Length - split2[3].LastIndexOf("/") - 1));
                        c.folder = split2[3].Remove(split2[3].Length - filename.Length);
                    }

                    c.filepath = url;
                    c.filename = selectedmedia["name"].ToString();
                    c.requiresauth = selectedmedia["requiresAuth"].ToString();

                    //Todo: solve references
                    c.subject = selectedmedia["subject_id"].ToString();
                    c.role = selectedmedia["role_id"].ToString();
                    c.mediatype = selectedmedia["mediatype_id"].ToString();
                    c.session = selectedmedia["session_id"].ToString();

                    paths.Add(c);
                }
            }
            return paths;
        }

        private void DataBaseResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataBasResultsBox.SelectedItem != null)
            {
                Properties.Settings.Default.DatabaseName = DataBasResultsBox.SelectedItem.ToString();
                Properties.Settings.Default.Save();

                authlevel = DatabaseHandler.CheckAuthentication(Properties.Settings.Default.MongoDBUser, Properties.Settings.Default.DatabaseName);
                if (authlevel > 2)
                {
                    GetSessions();
                    GetAnnotators();

                    AddSession.Visibility = Visibility.Visible;
                    DeleteSession.Visibility = Visibility.Visible;
                    EditSession.Visibility = Visibility.Visible;

                    AddAnnotator.Visibility = Visibility.Visible;
                    DeleteAnnotator.Visibility = Visibility.Visible;
                    EditAnnotator.Visibility = Visibility.Visible;
                }
            }
        }

        private void CollectionResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollectionResultsBox.SelectedItem != null)
            {
                Properties.Settings.Default.LastSessionId = ((DatabaseSession)(CollectionResultsBox.SelectedValue)).Name;
                Properties.Settings.Default.Save();

                GetMedia();

                if (authlevel > 2)
                {
                    AddFiles.Visibility = Visibility.Visible;
                    DeleteFiles.Visibility = Visibility.Visible;
                    AddSubjects.Visibility = Visibility.Visible;
                    DeleteSubject.Visibility = Visibility.Visible;
                    EditSubject.Visibility = Visibility.Visible;

                    //Todo. enable when the meta field is ready

                }
                if (authlevel > 3)
                {
                    AddRole.Visibility = Visibility.Visible;
                    DeleteRole.Visibility = Visibility.Visible;
                    AddMediaType.Visibility = Visibility.Visible;
                    DeleteMediaType.Visibility = Visibility.Visible;
                }
            }
        }

        private void SubjectsResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubjectsResultBox.SelectedItem != null)
            {
                var sessions = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
                var subjects = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);
                var builder = Builders<BsonDocument>.Filter;

                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);
                var filter = builder.Eq("name", SubjectsResultBox.SelectedItem.ToString());
                var subjectsresult = subjects.Find(filter).ToList();

                if(MediaResultBox.SelectedItem != null)
                {
                    var filtermedia = builder.Eq("name", MediaResultBox.SelectedItem.ToString()) & builder.Eq("session_id", sessionid);
                    var mediadocuments = media.Find(filtermedia).ToList();

                    if (mediadocuments.Count > 0)
                    {
                        var update = Builders<BsonDocument>.Update.Set("subject_id", subjectsresult[0].GetValue(0).AsObjectId);
                        media.UpdateOne(filtermedia, update);
                    }
                }
              
            }
        }

        private void MediaResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MediaResultBox.SelectedItem != null)
            {
                GetSubjects();
                GetRoles();
                GetMediaType();

                var sessions = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
                var roles = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Roles);
                var subjects = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects);
                var mediatypes = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.StreamTypes);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);

                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", Properties.Settings.Default.LastSessionId);
                var documents = sessions.Find(filter).ToList();
                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);
                var filtermedia = builder.Eq("name", MediaResultBox.SelectedItem.ToString()) & builder.Eq("session_id", sessionid);
                var mediadoc = media.Find(filtermedia).Single();

                // BsonArray files = documents[0]["media"].AsBsonArray;

                var filter2 = builder.Eq("_id", mediadoc["role_id"]);
                var rolescollection = roles.Find(filter2).ToList();
                if (rolescollection.Count > 0)
                {
                    var role = rolescollection[0];
                    foreach (var Item in RolesResultBox.Items)
                    {
                        if (Item.ToString() == role["name"].ToString())
                        {
                            RolesResultBox.SelectedItem = Item;
                            break;
                        }
                    }
                }

                var filter3 = builder.Eq("_id", mediadoc["subject_id"]);
                var subjectcollection = subjects.Find(filter3).ToList();

                if (subjectcollection.Count > 0)
                {
                    var subject = subjectcollection[0];
                    foreach (var Item in SubjectsResultBox.Items)
                    {
                        if (Item.ToString() == subject["name"].ToString())
                        {
                            SubjectsResultBox.SelectedItem = Item;
                            break;
                        }
                    }
                }
                var filter4 = builder.Eq("_id", mediadoc["mediatype_id"]);
                var mediatypecollection = mediatypes.Find(filter4).ToList();
                if (mediatypecollection.Count > 0)
                {
                    var mediatype = mediatypecollection[0];
                    foreach (var Item in MediatypeResultsBox.Items)
                    {
                        if (Item.ToString() == (mediatype["name"] + "#" + mediatype["type"]).ToString())
                        {
                            MediatypeResultsBox.SelectedItem = Item;
                            break;
                        }
                    }
                }
            }
        }

        private void DeleteDB_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (authlevel > 3) mongo.DropDatabase(Properties.Settings.Default.DatabaseName);
                else MessageBox.Show("You are not authorized to delete this Database");

                GetDatabase();
                GetSessions();
                GetRoles();
                GetMedia();
            }
            catch
            {
                //    MessageBox.Show("Didnt find database");
            }
        }

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (authlevel > 2)
            {
                if (CollectionResultsBox.SelectedItem != null)
                {
                    for (int i = 0; i < CollectionResultsBox.SelectedItems.Count; i++)
                    {
                        var builder = Builders<BsonDocument>.Filter;

                        var filter = builder.Eq("name", ((DatabaseSession)CollectionResultsBox.SelectedItem).Name);
                        var session = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).Find(filter).Single();
                        ObjectId sessionid = session.GetValue(0).AsObjectId;

                        var filtersession = builder.Eq("session_id", sessionid);
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotations).DeleteManyAsync(filtersession);
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams).DeleteManyAsync(filtersession);
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).DeleteOne(filter);
                    }
                }
            }
            else { MessageBox.Show("You are not authorized to delete this Session"); }

            GetSessions();
        }

        private void DeleteSubject_Click(object sender, RoutedEventArgs e)
        {
            if (SubjectsResultBox.SelectedItem != null)
            {
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", SubjectsResultBox.SelectedItem.ToString());
                var result = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).DeleteOne(filter);

                GetSubjects();
            }
        }

        private void DeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            if (MediaResultBox.SelectedItem != null)
            {
                var sessions = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);

                var builder = Builders<BsonDocument>.Filter;

                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);
                var filtermedia = builder.Eq("name", MediaResultBox.SelectedItem.ToString()) & builder.Eq("session_id", sessionid);
                var mediadocuments = media.Find(filtermedia).ToList();

                if (mediadocuments.Count > 0)
                {
                    media.DeleteOne(filtermedia);
                }

                //For Stream files we also delete the stream~ without the user having to care for it.
                if (MediaResultBox.SelectedItem.ToString().EndsWith(".stream"))
                {
                    var filtermedia2 = builder.Eq("name", MediaResultBox.SelectedItem.ToString() + "~") & builder.Eq("session_id", sessionid);
                    var mediadocuments2 = media.Find(filtermedia2).ToList();
                    if (mediadocuments2.Count > 0)
                    {
                        media.DeleteOne(filtermedia2);
                    }
                }

                RolesResultBox.Items.Clear();
                SubjectsResultBox.Items.Clear();
                MediatypeResultsBox.Items.Clear();
                GetMedia();
            }
        }

        private void EditSubject_Click(object sender, RoutedEventArgs e)
        {
            if(SubjectsResultBox.SelectedValue != null)
            {

           
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Eq("name", SubjectsResultBox.SelectedValue.ToString());
            var session = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).Find(filter).ToList();

            if (session.Count > 0)
            {
                DatabaseAdminSubjectWindow dbsw = new DatabaseAdminSubjectWindow(session[0]["name"].ToString(), session[0]["gender"].ToString(), session[0]["age"].ToString(), session[0]["culture"].ToString());
                dbsw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dbsw.ShowDialog();

                if (dbsw.DialogResult == true)
                {
                    var updatelocation = Builders<BsonDocument>.Update.Set("gender", dbsw.Gender());
                    database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).UpdateOne(filter, updatelocation);

                    var updatelanguage = Builders<BsonDocument>.Update.Set("age", dbsw.Age());
                    database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).UpdateOne(filter, updatelanguage);

                    var updatedate = Builders<BsonDocument>.Update.Set("culture", dbsw.Culture());
                    database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).UpdateOne(filter, updatedate);

                    var updatename = Builders<BsonDocument>.Update.Set("name", dbsw.SubjectName());
                    database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects).UpdateOne(filter, updatename);
                }

                GetSubjects(dbsw.SubjectName());
            }
            }
        }

        private void AddSubject_Click(object sender, RoutedEventArgs e)
        {
            DatabaseAdminSubjectWindow l = new DatabaseAdminSubjectWindow();
            l.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            l.ShowDialog();

            if (l.DialogResult == true)
            {
                //Todo: Add more
                BsonDocument subject = new BsonDocument {
                    {"name",  l.SubjectName()},
                    {"gender",  l.Gender()},
                    {"age",  l.Age()},
                    {"culture",  l.Culture()},
                    {"education",  ""},
                    {"personality",  ""},
                };

                bool subjectalreadypresent = false;
                foreach (var item in SubjectsResultBox.Items)
                {
                    if (item.ToString() == l.SubjectName())
                    {
                        subjectalreadypresent = true;
                    }
                }
                if (subjectalreadypresent == false)
                {
                    var collection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Subjects);
                    collection.InsertOne(subject);
                    GetSubjects(l.SubjectName());
                }
                else MessageBox.Show("Subject already exists!");
            }
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            if (RolesResultBox.SelectedItem != null)
            {
                var collection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Roles);
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", RolesResultBox.SelectedItem.ToString());
                var update = Builders<BsonDocument>.Update.Set("isValid", false);
                collection.UpdateOne(filter, update);

                //var result = database.GetCollection<BsonDocument>(DatabaseDefinition.Roles).DeleteOne(filter);

                GetRoles();
            }
        }

        private void RolesResultBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RolesResultBox.SelectedItem != null)
            {
                lastRole = RolesResultBox.SelectedItem.ToString();

                var roles = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Roles);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);
                var builder = Builders<BsonDocument>.Filter;

                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);
                var filter = builder.Eq("name", RolesResultBox.SelectedItem.ToString());
                var rolesresult = roles.Find(filter).ToList();

                var filtermedia = builder.Eq("name", MediaResultBox.SelectedItem.ToString()) & builder.Eq("session_id", sessionid);
                var mediadocuments = media.Find(filtermedia).ToList();

                if (mediadocuments.Count > 0)
                {
                    var update = Builders<BsonDocument>.Update.Set("role_id", rolesresult[0].GetValue(0).AsObjectId);
                    media.UpdateOne(filtermedia, update);
                }
            }
        }

        private void MediatypeResultsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MediatypeResultsBox.SelectedItem != null)
            {
                var mediatypes = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.StreamTypes);
                var media = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Streams);
                var builder = Builders<BsonDocument>.Filter;

                ObjectId sessionid = GetObjectID(mongo.GetDatabase(Properties.Settings.Default.DatabaseName), DatabaseDefinitionCollections.Sessions, "name", Properties.Settings.Default.LastSessionId);
                string[] split = MediatypeResultsBox.SelectedItem.ToString().Split('#');

                var filter2 = builder.Eq("name", split[0]) & builder.Eq("type", split[1]);
                var mediatyperesult = mediatypes.Find(filter2).Single();

                var filtermedia = builder.Eq("name", MediaResultBox.SelectedItem.ToString()) & builder.Eq("session_id", sessionid);

                var update = Builders<BsonDocument>.Update.Set("mediatype_id", mediatyperesult.GetValue(0).AsObjectId);
                media.UpdateOne(filtermedia, update);
            }
        }

        private void AddMediaType_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, UserInputWindow.Input> input = new Dictionary<string, UserInputWindow.Input>();
            input["name"] = new UserInputWindow.Input() { Label = "Name", DefaultValue = "" };
            input["type"] = new UserInputWindow.Input() { Label = "Type", DefaultValue = "" };
            UserInputWindow dialog = new UserInputWindow("Add new media", input);
            dialog.ShowDialog();
           
            if (dialog.DialogResult == true)
            {
                string name = dialog.Result("name");
                string type = dialog.Result("type");

                BsonDocument mediatype = new BsonDocument
                {
                    {"name",  name},
                    {"type",  type}
                };

                bool exists = false;
                foreach (var item in MediatypeResultsBox.Items)
                {
                    if (item.ToString() == name + "#" + type)
                    {
                        exists = true;
                    }
                }

                if (exists == false)
                {
                    var collection = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.StreamTypes);
                    collection.InsertOne(mediatype);
                    GetMediaType(name + "#" + type);
                }
                else
                {
                    MessageTools.Warning("Media type already exists");
                }
            }
        }

        private void DeleteMediaType_Click(object sender, RoutedEventArgs e)
        {
            if (MediatypeResultsBox.SelectedItem != null)
            {
                string[] split = MediatypeResultsBox.SelectedItem.ToString().Split('#');
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", split[0]) & builder.Eq("type", split[1]);
                var result = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.StreamTypes).DeleteOne(filter);

                GetMediaType();
            }
        }

        private void EditSession_Click(object sender, RoutedEventArgs e)
        {
            if (CollectionResultsBox.SelectedItem != null)
            {
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("name", ((DatabaseSession)(CollectionResultsBox.SelectedValue)).Name);
                var session = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).Find(filter).ToList();

                if (session.Count > 0)
                {
                    DatabaseAdminSessionWindow dbsw = new DatabaseAdminSessionWindow(session[0]["name"].ToString(), session[0]["language"].ToString(), session[0]["location"].ToString(), session[0]["date"].AsDateTime);
                    dbsw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    dbsw.ShowDialog();

                    if (dbsw.DialogResult == true)
                    {
                        var updatelocation = Builders<BsonDocument>.Update.Set("location", dbsw.SessionLocation());
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).UpdateOne(filter, updatelocation);

                        var updatelanguage = Builders<BsonDocument>.Update.Set("language", dbsw.SessionLanguage());
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).UpdateOne(filter, updatelanguage);

                        var updatedate = Builders<BsonDocument>.Update.Set("date", dbsw.SessionDate());
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).UpdateOne(filter, updatedate);

                        var updatename = Builders<BsonDocument>.Update.Set("name", dbsw.SessionName());
                        database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Sessions).UpdateOne(filter, updatename);
                    }
                }
                GetSessions();
            }
        }

        public string FetchDBRef(IMongoDatabase database, string collection, string attribute, ObjectId reference)
        {
            string output = "";
            var builder = Builders<BsonDocument>.Filter;
            var filtera = builder.Eq("_id", reference);
            var result = database.GetCollection<BsonDocument>(collection).Find(filtera).ToList();

            if (result.Count > 0)
            {
                output = result[0][attribute].ToString();
            }

            return output;
        }

        public ObjectId GetObjectID(IMongoDatabase database, string collection, string value, string attribute)
        {
            ObjectId id = new ObjectId();
            var builder = Builders<BsonDocument>.Filter;
            var filtera = builder.Eq(value, attribute);
            var result = database.GetCollection<BsonDocument>(collection).Find(filtera).ToList();

            if (result.Count > 0) id = result[0].GetValue(0).AsObjectId;

            return id;
        }

        private void Autologin_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DatabaseAutoLogin = true;
            Properties.Settings.Default.Save();
        }

        private void Autologin_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DatabaseAutoLogin = false;
            Properties.Settings.Default.Save();
        }

        private void db_pass_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Autologin.IsChecked = false;
            Autologin.IsEnabled = false;
        }

        private void db_login_TextChanged(object sender, TextChangedEventArgs e)
        {
            Autologin.IsChecked = false;
            Autologin.IsEnabled = false;
        }

        private void Annotators_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void EditAnnotator_Click(object sender, RoutedEventArgs e)
        {
            if (AnnotatorsBox.SelectedItem != null)
            {
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("fullname", AnnotatorsBox.SelectedValue);
                var annotator = database.GetCollection<BsonDocument>( DatabaseDefinitionCollections.Annotators).Find(filter).Single();

                if (annotator != null)
                {
                    string name = "";
                    string fullname = "";
                    string email = "";
                    string expertise = "";
                    BsonElement value;

                    if (annotator.TryGetElement("name", out value)) name = annotator["name"].ToString();
                    if (annotator.TryGetElement("fullname", out value)) fullname = annotator["fullname"].ToString();
                    if (annotator.TryGetElement("email", out value)) email = annotator["email"].ToString();
                    if (annotator.TryGetElement("expertise", out value)) expertise = annotator["expertise"].ToString();

                    try
                    {
                        DatabaseAdminAnnotatorWindow dbsw = new DatabaseAdminAnnotatorWindow(name, fullname, email, expertise);
                        dbsw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        dbsw.ShowDialog();

                        if (dbsw.DialogResult == true)
                        {
                            var filterid = builder.Eq("_id", annotator["_id"].AsObjectId);

                            var updatename = Builders<BsonDocument>.Update.Set("name", dbsw.NameCombo.SelectionBoxItem.ToString());
                            database.GetCollection<BsonDocument>( DatabaseDefinitionCollections.Annotators).UpdateOne(filterid, updatename);

                            var updatefullname = Builders<BsonDocument>.Update.Set("fullname", dbsw.Fullname());
                            database.GetCollection<BsonDocument>( DatabaseDefinitionCollections.Annotators).UpdateOne(filterid, updatefullname);

                            var updateemail = Builders<BsonDocument>.Update.Set("email", dbsw.Email());
                            database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators).UpdateOne(filterid, updateemail);

                            var updateexpertise = Builders<BsonDocument>.Update.Set("expertise", dbsw.Expertise());
                            database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators).UpdateOne(filterid, updateexpertise);

                            string role = dbsw.RoleBox.SelectionBoxItem.ToString();

                            revokeRolesfromUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                            revokeRolesfromUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);
                            revokeRolesfromUser(name, "readWrite", "admin");
                            revokeRolesfromUser(name, "userAdminAnyDatabase", "admin");

                            if (role == "read") grandRolestoUser(name, "read", Properties.Settings.Default.DatabaseName);
                            else if (role == "readWrite") grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                            else if (role == "dbAdmin")
                            {
                                grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);

                            }
                            else if (role == "userAdmin")
                            {
                                grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "readWrite", "admin");
                                grandRolestoUser(name, "userAdminAnyDatabase", "admin");


                            }

                            if (dbsw.Password() != "")
                            {
                                ChangeDBPassword(dbsw.NameCombo.SelectionBoxItem.ToString(), dbsw.Password());
                            }
                        }
                    
                GetAnnotators();
                }
                  

                   catch (Exception ex)
                {

                    MessageBox.Show("Not authorized on admin database to change users");

                }
            }
            }
        }

        private void DeleteAnnotator_Click(object sender, RoutedEventArgs e)
        {
            try
            {

           
            MessageBoxResult mb = MessageBox.Show("Warning: Annotator will be deleted from the database, are you sure to continue?", "Warning", MessageBoxButton.YesNo);
            if (AnnotatorsBox.SelectedItem != null && mb == MessageBoxResult.Yes)
            {
                var builder = Builders<BsonDocument>.Filter;
                var filter = builder.Eq("fullname", AnnotatorsBox.SelectedValue);
                var result = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators).Find(filter).Single();
                string user = result["name"].AsString;
                var del = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators).DeleteOne(filter);



                revokeRolesfromUser(user, "read", Properties.Settings.Default.DatabaseName);
                revokeRolesfromUser(user, "readWrite", Properties.Settings.Default.DatabaseName);
                revokeRolesfromUser(user, "dbAdmin", Properties.Settings.Default.DatabaseName);

                MessageBoxResult mb2 = MessageBox.Show("Do you also want to delete the user login from the database? ", "Warning", MessageBoxButton.YesNo);
                if (mb2 == MessageBoxResult.Yes)
                {
                    DropUser(user);
                }

                GetAnnotators();
                }
            }
            catch(Exception ex )
            {

                MessageBox.Show("Not authorized to delete users");
            }
            
        }

        private void AddDBUser(string user, string password, string role)
        {
            var  admindatabase = mongo.GetDatabase("admin");
            var createuser = new BsonDocument { { "createUser", user }, { "pwd", password }, { "roles", new BsonArray { new BsonDocument { { "role", "readAnyDatabase" }, { "db", "admin" } }, new BsonDocument { { "role", role }, { "db", Properties.Settings.Default.DatabaseName } } } } };
            try
            {
                admindatabase.RunCommand<BsonDocument>(createuser);
            }
            catch (Exception ex)
            {
                grandRolestoUser(user, role, Properties.Settings.Default.DatabaseName);
            }
        }


        private void grandRolestoUser(string user, string role, string db)
        {
            try
            {
                var admindatabase = mongo.GetDatabase("admin");
                // var updateroles = new BsonDocument { { "updateUser", user }, { "pwd", password }, { "roles", new BsonArray { new BsonDocument { { "role", "readAnyDatabase" }, { "db", "admin" } }, new BsonDocument { { "role", "readWrite" }, { "db", Properties.Settings.Default.Database } } } } };
                var updateroles = new BsonDocument { { "grantRolesToUser", user }, { "roles", new BsonArray { { new BsonDocument { { "role", role }, { "db", db } } } } } };
                admindatabase.RunCommand<BsonDocument>(updateroles);
            }
            catch (Exception e)
            {
                MessageBox.Show("Annotator already exists in the database, no new user account was created.");
            }

        }


        private void revokeRolesfromUser(string user, string role, string db)
        {
            try
            {
                var admindatabase = mongo.GetDatabase("admin");
                var updateroles = new BsonDocument { { "revokeRolesFromUser", user }, { "roles", new BsonArray { { new BsonDocument { { "role", role }, { "db", db } } } } } };
                admindatabase.RunCommand<BsonDocument>(updateroles);
            }

            catch (Exception ex)
            {

            }

        }



        private void ChangeDBPassword(string user, string password)
        {
            var database = mongo.GetDatabase("admin");
            // database.RunCommand<string>("use admin");

            var changepw = new BsonDocument { { "updateUser", user }, { "pwd", password } };
            // var changepw = new BsonDocument { { "updateUser",  new BsonDocument { { user, password } } } };
            try
            {
                database.RunCommand<BsonDocument>(changepw);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not change password.");
            }
        }

        private void DropUser(string user)
        {
            int auth = DatabaseHandler.CheckAuthentication(user, "admin");

            if(user != Properties.Settings.Default.MongoDBUser && auth < 4)
            {
                var database = mongo.GetDatabase("admin");

                var dropuser = new BsonDocument { { "dropUser", user } };

                try
                {
                    database.RunCommand<BsonDocument>(dropuser);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not delete user.");
                }
            }

            else MessageBox.Show("You can not delete yourself or superusers");
        
        }

        private void AddAnnotator_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                DatabaseAdminAnnotatorWindow dbsw = new DatabaseAdminAnnotatorWindow();
            dbsw.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dbsw.ShowDialog();

            if (dbsw.DialogResult == true)
            {
                string name = dbsw.Name();
                if (dbsw.Name() == "") name = dbsw.NameCombo.SelectedItem.ToString();

                BsonDocument annotator = new BsonDocument {
                    {"name",  name},
                    {"fullname",  dbsw.Fullname()},
                    {"email",  dbsw.Email()},
                    {"expertise",  dbsw.Expertise()},
                };

               

                var builder = Builders<BsonDocument>.Filter;
                var filterannotator = builder.Eq("name",name);
                UpdateOptions uoa = new UpdateOptions();
                uoa.IsUpsert = true;
                var result = database.GetCollection<BsonDocument>(DatabaseDefinitionCollections.Annotators).ReplaceOne(filterannotator, annotator, uoa);
                if (result.ModifiedCount == 0)
                {
                    string role = dbsw.RoleBox.SelectionBoxItem.ToString();
                    if (dbsw.Name() != "" && dbsw.Password() != "") AddDBUser(name, dbsw.Password(), role);
                    else
                        {
                            revokeRolesfromUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                            revokeRolesfromUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);
                            revokeRolesfromUser(name, "readWrite", "admin");
                            revokeRolesfromUser(name, "userAdminAnyDatabase", "admin");

                            if (role =="read") grandRolestoUser(name, "read", Properties.Settings.Default.DatabaseName);
                            else  if (role == "readWrite") grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                            else if (role == "dbAdmin") {
                                grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);
                              
                            }
                            else if (role == "userAdmin")
                            {
                                grandRolestoUser(name, "readWrite", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "dbAdmin", Properties.Settings.Default.DatabaseName);
                                grandRolestoUser(name, "readWrite", "admin");
                                grandRolestoUser(name, "userAdminAnyDatabase", "admin");


                            }

                        }

                }
                }

                GetAnnotators(dbsw.Fullname());
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Not authorized to add users");
                }
            
            }
        
    }
}