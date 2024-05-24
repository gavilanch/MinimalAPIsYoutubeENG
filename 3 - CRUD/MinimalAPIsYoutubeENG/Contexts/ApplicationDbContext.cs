using Microsoft.EntityFrameworkCore;
using MinimalAPIsYoutubeENG.Entities;

namespace MinimalAPIsYoutubeENG.Contexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Person> People { get; set; }
    }
}
