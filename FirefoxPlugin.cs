using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;

namespace Wox.Plugin.Firefox
{
    public class FirefoxPlugin : IPlugin
    {
        private PluginInitContext _context;

        private const string queryFormat = @"SELECT url, title
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

        private const string dbPathFormat = "Data Source ={0};Version=3;New=False;Compress=True;";

        public void Init(PluginInitContext context)
        {
            this._context = context;         
        }

        public List<Result> Query(Query query)
        {
            string param = query.GetAllRemainingParameter();

            var results = new List<MozBookmark>();
            
            if (string.IsNullOrEmpty(param))
                results = GetBookmarks(top: true);
            else
                results = GetBookmarks(param);

            return results.Select(x => new Result
                {
                    Title = x.title,
                    SubTitle = x.url,
                    Action = e => _context.ShellRun(x.url)  //TODO: Make sure url opens in Firefox?
                }).ToList();
        }

        public List<MozBookmark> GetBookmarks(string search = null, bool top = false)
        {
            string dbPath = string.Format(dbPathFormat, PlacesPath);
            var dbConnection = new SQLiteConnection(dbPath);

            string query = top ? queryTopBookmarks : string.Format(queryFormat, search);

            dbConnection.Open();
            var reader = new SQLiteCommand(query, dbConnection).ExecuteReader();

            return reader.Select(x => new MozBookmark()
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

    public class MozBookmark
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
