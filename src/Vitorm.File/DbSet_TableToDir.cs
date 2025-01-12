using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Vit.Core.Module.Serialization;
using Vit.Linq;

using Vitorm.Entity;

namespace Vitorm.File
{


    public partial class DbSet_TableToDir<Entity, EntityKey> : IDbSet<Entity>
    {
        public virtual IDbContext dbContext { get; protected set; }
        public virtual DbContext DbContext => (DbContext)dbContext;


        protected IEntityDescriptor _entityDescriptor;
        public virtual IEntityDescriptor entityDescriptor => _entityDescriptor;


        public DbSet_TableToDir(DbContext dbContext, IEntityDescriptor entityDescriptor)
        {
            this.dbContext = dbContext;
            this._entityDescriptor = entityDescriptor;
        }

        // #0 Schema :  ChangeTable
        public virtual IEntityDescriptor ChangeTable(string tableName) => _entityDescriptor = _entityDescriptor.WithTable(tableName);
        public virtual IEntityDescriptor ChangeTableBack() => _entityDescriptor = _entityDescriptor.GetOriginEntityDescriptor();



        #region  File Methods

        DirectoryInfo tableDirectory => new DirectoryInfo(Path.Combine(DbContext.dbConfig.connectionString, entityDescriptor.tableName));

        protected virtual string GetEntityPath(object key)
        {
            var strKey = key?.ToString();
            if (string.IsNullOrWhiteSpace(strKey)) throw new Exception("key can not be empty");
            return Path.Combine(DbContext.dbConfig.connectionString, entityDescriptor.tableName, key + ".json");
        }


        protected virtual Entity FromJson(Dictionary<string, object> json)
        {
            var entity = (Entity)Activator.CreateInstance(entityDescriptor.entityType);
            entityDescriptor.properties.ForEach(col =>
            {
                if (json.TryGetValue(col.columnName, out var value)) col.SetValue(entity, TypeUtil.ConvertToType(value, col.type));
            });
            return entity;
        }

        protected virtual Entity GetEntityByPath(string path)
        {
            if (!System.IO.File.Exists(path)) return default;

            var json = Json.Deserialize<Dictionary<string, object>>(System.IO.File.ReadAllText(path));

            return FromJson(json);
        }
        protected int GetMaxId()
        {
            var files = tableDirectory.GetFiles();
            if (files?.Any() != true) return 0;
            return files.Max(f => int.TryParse(f.Name, out var id) ? id : 0);
        }

        public static int RecursionDelete(DirectoryInfo directory)
        {
            if (!directory.Exists) return 0;

            int count = 0;
            foreach (var subDir in directory.GetDirectories())
            {
                count += RecursionDelete(subDir);
                subDir.Delete();
            }

            foreach (var file in directory.GetFiles())
            {
                file.Delete();
                count++;
            }
            return count;
        }



        #endregion



        #region #0 Schema : Create Drop

        public virtual bool TableExist()
        {
            return tableDirectory.Exists;
        }

        public virtual void TryCreateTable()
        {
            if (TableExist()) return;

            tableDirectory.Create();
        }
        public virtual async Task TryCreateTableAsync() => TryCreateTable();


        public virtual void TryDropTable()
        {
            if (!TableExist()) return;
            Truncate();
            tableDirectory.Delete();
        }
        public virtual async Task TryDropTableAsync() => TryDropTable();


        public virtual void Truncate()
        {
            RecursionDelete(tableDirectory);
        }

        public virtual async Task TruncateAsync() => Truncate();

        #endregion





        #region #1 Create : Add AddRange
        public virtual Entity Add(Entity entity)
        {
            object keyValue = entityDescriptor.key.GetValue(entity);

            // generate identity key if needed
            var keyIsEmpty = keyValue is null || keyValue.Equals(TypeUtil.GetDefaultValue(entityDescriptor.key.type));
            if (entityDescriptor.key.isIdentity && keyIsEmpty)
            {
                keyValue = GetMaxId() + 1;
                entityDescriptor.key.SetValue(entity, keyValue);
            }


            var path = GetEntityPath(keyValue);
            if (System.IO.File.Exists(path)) throw new Exception("file already exist");


            var json = entityDescriptor.properties.ToDictionary(col => col.columnName, col => col.GetValue(entity));

            System.IO.File.WriteAllText(path, Json.Serialize(json));

            return entity;
        }


        public virtual async Task<Entity> AddAsync(Entity entity) => Add(entity);

        public virtual void AddRange(IEnumerable<Entity> entities) => entities?.ForEach(e => Add(e));

        public virtual async Task AddRangeAsync(IEnumerable<Entity> entities) => AddRange(entities);

        #endregion


        #region #2 Retrieve : Get Query

        public virtual Entity Get(object keyValue)
        {
            var path = GetEntityPath(keyValue);
            return GetEntityByPath(path);
        }

        public virtual async Task<Entity> GetAsync(object keyValue) => Get(keyValue);

        public virtual IQueryable<Entity> Query()
        {
            return tableDirectory.GetFiles("*.json").AsQueryable().Select(file => GetEntityByPath(file.FullName));
        }

        #endregion


        #region #3 Update: Update UpdateRange
        public virtual int Update(Entity entity)
        {
            var count = Delete(entity);
            if (count == 0) return 0;
            Add(entity);
            return 1;
        }
        public virtual async Task<int> UpdateAsync(Entity entity) => Update(entity);


        public virtual int UpdateRange(IEnumerable<Entity> entities)
        {
            return entities.Sum(entity => Update(entity));
        }
        public virtual async Task<int> UpdateRangeAsync(IEnumerable<Entity> entities) => UpdateRange(entities);

        #endregion


        #region #4 Delete : Delete DeleteRange DeleteByKey DeleteByKeys

        public virtual int Delete(Entity entity)
        {
            var keyValue = entityDescriptor.key.GetValue(entity);
            return DeleteByKey(keyValue);
        }
        public virtual async Task<int> DeleteAsync(Entity entity) => Delete(entity);

        public virtual int DeleteRange(IEnumerable<Entity> entities) => entities.Sum(entity => Delete(entity));
        public virtual async Task<int> DeleteRangeAsync(IEnumerable<Entity> entities) => DeleteRange(entities);


        public virtual int DeleteByKey(object keyValue)
        {
            var path = GetEntityPath(keyValue);
            if (!System.IO.File.Exists(path)) return 0;

            System.IO.File.Delete(path);
            return 1;
        }
        public virtual async Task<int> DeleteByKeyAsync(object keyValue) => DeleteByKey(keyValue);



        public virtual int DeleteByKeys<Key>(IEnumerable<Key> keys) => keys.Sum(key => DeleteByKey(key));
        public virtual async Task<int> DeleteByKeysAsync<Key>(IEnumerable<Key> keys) => DeleteByKeys(keys);

        #endregion


    }
}
