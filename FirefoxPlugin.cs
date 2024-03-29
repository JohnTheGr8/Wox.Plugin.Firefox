﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;

namespace Wox.Plugin.Firefox
{
    public class FirefoxPlugin : IPlugin
    {
        private PluginInitContext _context;

        private const string queryBookmarks = @"SELECT url, title
              FROM moz_places
              WHERE id in (
                SELECT bm.fk FROM moz_bookmarks bm WHERE bm.fk NOT NULL
              )
              AND ( url LIKE '%{0}%' OR title LIKE '%{0}%' )
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string queryTopBookmarks = @"SELECT url, title
              FROM moz_places
              WHERE id in (
                SELECT bm.fk FROM moz_bookmarks bm WHERE bm.fk NOT NULL
              )
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string queryHistory = @"SELECT url, title
              FROM moz_places
              WHERE  ( url LIKE '%{0}%' OR title LIKE '%{0}%' )
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string queryTopHistory = @"SELECT url, title
              FROM moz_places
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string dbPathFormat = "Data Source ={0};Version=3;New=False;Compress=True;";

        public void Init(PluginInitContext context)
        {
            this._context = context;         
        }

        public List<Result> Query(Query query)
        {
            string param = query.GetAllRemainingParameter().TrimStart();

            // Is this history search?
            var historySearch = query.ActionParameters.Count > 0 && query.ActionParameters[0].Equals("-h");

            // If it is history search, remove the -h flag from param
            if (historySearch)
                param = param.Substring("-h".Length).TrimStart();

            // Should top results be returned? (true if no search parameters have been passed)
            var topResults = string.IsNullOrEmpty(param);

            // Get results, either bookmarks or history
            List<MozPlace> results = (historySearch) ? 
                GetHistory(param, topResults) : 
                GetBookmarks(param, topResults);

            return results.Select(x => new Result
                {
                    Title = x.title,
                    SubTitle = x.url,
                    Action = e => _context.ShellRun(x.url)  //TODO: Make sure url opens in Firefox?
                }).ToList();
        }

        public List<MozPlace> GetHistory(string search = null, bool top = false)
        {
            // Create the query command for the given case
            string query = top ? queryTopHistory : string.Format(queryHistory, search);

            return GetResults(query);
        } 

        public List<MozPlace> GetBookmarks(string search = null, bool top = false)
        {
            // Create the query command for the given case
            string query = top ? queryTopBookmarks : string.Format(queryBookmarks, search);

            return GetResults(query);
        }

        /// <summary>
        /// Searches the places.sqlite db based on the given query and returns the results
        /// </summary>
        private List<MozPlace> GetResults(string query)
        {
            // create the connection string and init the connection
            string dbPath = string.Format(dbPathFormat, PlacesPath);
            var dbConnection = new SQLiteConnection(dbPath);

            // Open connection to the database file and execute the query
            dbConnection.Open();
            var reader = new SQLiteCommand(query, dbConnection).ExecuteReader();

            // return results in List<MozPlace> format
            return reader.Select(x => new MozPlace()
            {
                title = (x["title"] is DBNull) ? string.Empty : x["title"].ToString(),
                url = x["url"].ToString()
            }).ToList();
        }

        /// <summary>
        /// Path to places.sqlite
        /// </summary>
        public string PlacesPath
        {
            get
            {
                var profilesPath = Environment.ExpandEnvironmentVariables(@"%appdata%\Mozilla\Firefox\Profiles\");
                var folders = new DirectoryInfo(profilesPath).GetDirectories().Select(x => x.FullName).ToList();                
                
                // Look for the default profile folder
                return string.Format(@"{0}\places.sqlite",
                                     folders.FirstOrDefault(d => File.Exists(d + @"\places.sqlite") && d.EndsWith(".default"))
                                     ?? folders.First(d => File.Exists(d + @"\places.sqlite")));
            }
        }
    }

    public class MozPlace
    {
        public string title;

        public string url;
    }

    public static class Extensions
    {
        public static IEnumerable<T> Select<T>(this SQLiteDataReader reader, Func<SQLiteDataReader, T> projection)
        {
            while (reader.Read())
            {
                yield return projection(reader);
            }
        }
    }
}
