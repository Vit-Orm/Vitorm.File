using System.Collections.Generic;
using System.IO;

namespace Vitorm.File
{
    public class DbConfig
    {

        public DbConfig(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public DbConfig(string connectionString, string mode)
        {
            this.connectionString = connectionString;
            this.mode = mode;
        }

        public DbConfig(Dictionary<string, object> config)
        {
            object value;
            if (config.TryGetValue("connectionString", out value))
                this.connectionString = value as string;


            if (config.TryGetValue("mode", out value))
                this.mode = value as string;
        }

        public string connectionString { get; set; }

        /// <summary>
        /// TableToFile(default), TableToDir , RowMapToFile
        /// </summary>
        public string mode { get; set; }


        public virtual string databaseName => Path.GetFileName(connectionString);

        public virtual DbConfig WithDatabase(string databaseName)
        {
            var _connectionString = Path.Combine(Path.GetDirectoryName(connectionString), databaseName);

            return new DbConfig(_connectionString, mode);
        }

        internal string dbHashCode => (connectionString + (mode ?? "TableToFile")).GetHashCode().ToString();
    }
}
