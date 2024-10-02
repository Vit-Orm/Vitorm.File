using System;
using System.Reflection;

using Vitorm.Entity;

namespace Vitorm.File
{
    public class DbSetConstructor
    {
        public static IDbSet CreateDbSet(IDbContext dbContext, IEntityDescriptor entityDescriptor)
        {
            return _CreateDbSet.MakeGenericMethod(entityDescriptor.entityType, entityDescriptor.key?.type ?? typeof(string))
                     .Invoke(null, new object[] { dbContext, entityDescriptor }) as IDbSet;
        }

        static readonly MethodInfo _CreateDbSet = new Func<DbContext, IEntityDescriptor, IDbSet>(CreateDbSet<object, string>)
                   .Method.GetGenericMethodDefinition();
        public static IDbSet<Entity> CreateDbSet<Entity, EntityKey>(DbContext dbContext, IEntityDescriptor entityDescriptor)
        {
            // TableToFile(default), TableToDir , RowMapToFile
            if (dbContext.dbConfig.mode == "TableToDir") return new DbSet_TableToDir<Entity, EntityKey>(dbContext, entityDescriptor);
            if (dbContext.dbConfig.mode == "TableMapToFile") return new DbSet_RowMapToFile<Entity, EntityKey>(dbContext, entityDescriptor);

            return new DbSet_TableToFile<Entity, EntityKey>(dbContext, entityDescriptor);
        }

    }
}
