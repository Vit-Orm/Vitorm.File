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

    public partial class DbSet_TableToFile<Entity, EntityKey> : IDbSet<Entity>
    {
        public virtual IDbContext dbContext { get; protected set; }
        public virtual DbContext DbContext => (DbContext)dbContext;


        protected IEntityDescriptor _entityDescriptor;
        public virtual IEntityDescriptor entityDescriptor => _entityDescriptor;


        public DbSet_TableToFile(DbContext dbContext, IEntityDescriptor entityDescriptor)
        {
            this.dbContext = dbContext;
            this._entityDescriptor = entityDescriptor;
        }

        // #0 Schema :  ChangeTable
        public virtual IEntityDescriptor ChangeTable(string tableName) => _entityDescriptor = _entityDescriptor.WithTable(tableName);
        public virtual IEntityDescriptor ChangeTableBack() => _entityDescriptor = _entityDescriptor.GetOriginEntityDescriptor();



        #region  File Methods

        public string tablePath => Path.Combine(DbContext.dbConfig.connectionString, entityDescriptor.tableName + ".json");


        protected virtual Entity FromJson(Dictionary<string, object> json)
        {
            var entity = (Entity)Activator.CreateInstance(entityDescriptor.entityType);
            entityDescriptor.allColumns.ForEach(col =>
            {
                if (json.TryGetValue(col.columnName, out var value)) col.SetValue(entity, TypeUtil.ConvertToType(value, col.type));
            });
            return entity;
        }
        protected virtual Dictionary<string, object> ToJson(Entity entity)
        {
            return entityDescriptor.allColumns.ToDictionary(col => col.columnName, col => col.GetValue(entity));
        }


        protected virtual IEnumerable<Entity> GetEntities()
        {
            if (!System.IO.File.Exists(tablePath)) return default;
            var rows = Json.Deserialize<List<Dictionary<string, object>>>(System.IO.File.ReadAllText(tablePath));
            return rows.Select(FromJson);
        }
        protected virtual void SaveEntities(IEnumerable<Entity> entities)
        {
            string strContent = "[]";
            if (entities != null)
            {
                var jsonList = entities.Select(ToJson);
                strContent = Json.Serialize(jsonList);
            }
            System.IO.File.WriteAllText(tablePath, strContent);
        }



        protected virtual int GetMaxId(IEnumerable<Entity> entities)
        {
            return entities.Max(entity => int.TryParse(entityDescriptor.key.GetValue(entity)?.ToString(), out var id) ? id : 0);
        }
       


        protected virtual int RemoveEntities(List<Entity> entities, IEnumerable<Entity> entitiesToRemove)
        {
            var keys = entitiesToRemove.Select(entity => entityDescriptor.key.GetValue(entity));
            return RemoveByKeys(entities, keys);
        }
     

        protected virtual int RemoveByKeys<Key>(List<Entity> entities, IEnumerable<Key> keys)
        {
            IEnumerable<EntityKey> entityKeys;
            if (typeof(Key) == typeof(EntityKey))
            {
                entityKeys = (IEnumerable<EntityKey>)keys;
            }
            else
            {
                entityKeys = keys.Select(key => (EntityKey)TypeUtil.ConvertToType(key, typeof(EntityKey)));
            }
            return entities.RemoveAll(entity => entityKeys.Contains((EntityKey)entityDescriptor.key.GetValue(entity)));
        }

        #endregion



        #region #0 Schema : Create Drop

        public virtual bool TableExist()
        {
            return System.IO.File.Exists(tablePath);
        }

        public virtual void TryCreateTable()
        {
            if (TableExist()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(tablePath));
            SaveEntities(null);
        }
        public virtual async Task TryCreateTableAsync() => TryCreateTable();


        public virtual void TryDropTable()
        {
            if (!TableExist()) return;
            System.IO.File.Delete(tablePath);
        }
        public virtual async Task TryDropTableAsync() => TryDropTable();


        public virtual void Truncate()
        {
            System.IO.File.Delete(tablePath);
            SaveEntities(null);
        }

        public virtual async Task TruncateAsync() => Truncate();

        #endregion





        #region #1 Create : Add AddRange
        public virtual Entity Add(Entity entity)
        {
            AddRange(new[] { entity });
            return entity;
        }

        public virtual async Task<Entity> AddAsync(Entity entity)
        {
            await AddRangeAsync(new[] { entity });
            return entity;
        }

        public virtual void AddRange(IEnumerable<Entity> entities)
        {
            var entityList = Query().ToList();

            var removedCount = RemoveEntities(entityList, entities);
            if (removedCount > 0) throw new Exception("entity already exist");


            #region generate identity key if needed
            if (entityDescriptor.key.isIdentity)
            {
                int maxId = GetMaxId(entityList);

                entities.ForEach(entity =>
                {
                    object keyValue = entityDescriptor.key.GetValue(entity);
                    var keyIsEmpty = keyValue is null || keyValue.Equals(TypeUtil.DefaultValue(entityDescriptor.key.type));
                    if (keyIsEmpty)
                    {
                        maxId++;
                        entityDescriptor.key.SetValue(entity, maxId);
                    }
                });
            }
            #endregion

            entityList.AddRange(entities);

            SaveEntities(entityList);
        }

        public virtual async Task AddRangeAsync(IEnumerable<Entity> entities) => AddRange(entities);

        #endregion


        #region #2 Retrieve : Get Query

        public virtual Entity Get(object keyValue)
        {
            var key = (EntityKey)TypeUtil.ConvertToType(keyValue, entityDescriptor.key.type);
            return GetEntities().FirstOrDefault(entity => key.Equals((EntityKey)entityDescriptor.key.GetValue(entity)));
        }

        public virtual async Task<Entity> GetAsync(object keyValue) => Get(keyValue);


        public virtual IQueryable<Entity> Query()
        {
            return GetEntities().AsQueryable();
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
            var entityList = Query().ToList();

            bool EntitiesHaveSameKey(Entity entity1, Entity entity2)
            {
                var key1 = (EntityKey)entityDescriptor.key.GetValue(entity1);
                var key2 = (EntityKey)entityDescriptor.key.GetValue(entity2);
                return key1?.Equals(key2) == true;
            }

            var entitiesToUpdate =
                   (from entityFromDb in entityList
                    from entityToUpdate in entities.Where(entityToUpdate => EntitiesHaveSameKey(entityFromDb, entityToUpdate))
                    select new { entityFromDb, entityToUpdate })
                   .ToList();

            var removedCount = entitiesToUpdate.Count;
            if (removedCount == 0) return 0;

            var entitiesToSave = entityList.Except(entitiesToUpdate.Select(m => m.entityFromDb)).Concat(entitiesToUpdate.Select(m => m.entityToUpdate));

            SaveEntities(entitiesToSave);

            return removedCount;
        }
        public virtual async Task<int> UpdateRangeAsync(IEnumerable<Entity> entities) => UpdateRange(entities);

        #endregion


        #region #4 Delete : Delete DeleteRange DeleteByKey DeleteByKeys

        public virtual int Delete(Entity entity)
        {
            return DeleteByKey(entityDescriptor.key.GetValue(entity));
        }
        public virtual async Task<int> DeleteAsync(Entity entity) => Delete(entity);

        public virtual int DeleteRange(IEnumerable<Entity> entities) => entities.Sum(entity => Delete(entity));
        public virtual async Task<int> DeleteRangeAsync(IEnumerable<Entity> entities) => DeleteRange(entities);


        public virtual int DeleteByKey(object keyValue)
        {
            return DeleteByKeys(new[] { keyValue });
        }
        public virtual async Task<int> DeleteByKeyAsync(object keyValue) => DeleteByKey(keyValue);



        public virtual int DeleteByKeys<Key>(IEnumerable<Key> keys)
        {
            var entityList = Query().ToList();

            var removedCount = RemoveByKeys(entityList, keys);
            if (removedCount == 0) return 0;

            SaveEntities(entityList);

            return removedCount;
        }
        public virtual async Task<int> DeleteByKeysAsync<Key>(IEnumerable<Key> keys) => DeleteByKeys(keys);

        #endregion


    }
}
