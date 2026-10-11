using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// IMicroLimsDbContext is the Application layer's view of the database.
// Every DbSet on the real context must be on the interface, and vice versa.
public class DbContextPortTests
{
    private static HashSet<string> DbSetNames(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
         .Select(p => p.Name)
         .ToHashSet();

    [Fact]
    public void EveryDbSetIsOnTheInterface_AndTheInterfaceHasNoExtras()
    {
        var real = DbSetNames(typeof(MicroLimsDbContext));
        var port = DbSetNames(typeof(IMicroLimsDbContext));

        Assert.Empty(real.Except(port));   // on the context but missing from the port
        Assert.Empty(port.Except(real));   // on the port but not implemented
    }
}
