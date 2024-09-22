using System.Data;

namespace Vitorm.File
{
    public partial class DbContext : Vitorm.DbContext
    {
        public DbConfig dbConfig { get; protected set; }

        public DbContext(DbConfig dbConfig) : base(DbSetConstructor.CreateDbSet)
        {
            this.dbConfig = dbConfig;
        }

        public DbContext(string connectionString) : this(new DbConfig(connectionString))
        {
        }


        #region Transaction
        public virtual IDbTransaction BeginTransaction() => throw new System.NotImplementedException();
        public virtual IDbTransaction GetCurrentTransaction() => throw new System.NotImplementedException();

        #endregion



        public virtual string databaseName => dbConfig.databaseName;
        public virtual void ChangeDatabase(string databaseName)
        {
            dbConfig = dbConfig.WithDatabase(databaseName);
        }



        public override void Dispose()
        {


            base.Dispose();
        }


    }
}