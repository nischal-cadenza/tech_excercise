using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StargateAPI.Business.Data;

namespace StargateAPI.Tests.Helpers
{
    public static class TestDbContextFactory
    {
        public static StargateContext Create()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<StargateContext>()
                .UseSqlite(connection)
                .Options;

            var context = new StargateContext(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
