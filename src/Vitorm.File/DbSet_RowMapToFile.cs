using System.Collections.Generic;
using System.Linq;

using Vit.Core.Module.Serialization;

using Vitorm.Entity;

namespace Vitorm.File
{

    public partial class DbSet_RowMapToFile<Entity, EntityKey> : DbSet_TableToFile<Entity, EntityKey>
    {
        public DbSet_RowMapToFile(DbContext dbContext, IEntityDescriptor entityDescriptor) : base(dbContext, entityDescriptor) { }

        protected override IEnumerable<Entity> GetEntities()
        {
            if (!System.IO.File.Exists(tablePath)) return default;
            var rows = Json.Deserialize<Dictionary<EntityKey, Dictionary<string, object>>>(System.IO.File.ReadAllText(tablePath));
            return rows.Values.Select(FromJson);
        }
        protected override void SaveEntities(IEnumerable<Entity> entities)
        {
            string strContent = "{}";
            if (entities != null)
            {
                var rowMap = entities.ToDictionary(entity => (EntityKey)entityDescriptor.key.GetValue(entity), ToJson);
                strContent = Json.Serialize(rowMap);
            }
            System.IO.File.WriteAllText(tablePath, strContent);
        }

    }
}
