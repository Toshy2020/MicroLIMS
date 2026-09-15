using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests;

public static class MediaProductTestData
{
    public static async Task<MediaProduct> CreateOrGetAsync(MicroLimsDbContext db, string name, string code)
    {
        var product = await db.MediaProducts.FirstOrDefaultAsync(p => p.Code == code || p.Name == name);
        if (product == null)
        {
            product = new MediaProduct { Name = name, Code = code };
            db.MediaProducts.Add(product);
            await db.SaveChangesAsync();
        }
        return product;
    }

    public static async Task<MediaProduct> CreateAndLinkAsync(MicroLimsDbContext db, Material material, string name, string code)
    {
        var product = await CreateOrGetAsync(db, name, code);
        Link(material, product);
        await db.SaveChangesAsync();
        return product;
    }

    public static MediaProduct CreateOrGet(MicroLimsDbContext db, string name, string code)
    {
        var product = db.MediaProducts.FirstOrDefault(p => p.Code == code || p.Name == name);
        if (product == null)
        {
            product = new MediaProduct { Name = name, Code = code };
            db.MediaProducts.Add(product);
            db.SaveChanges();
        }
        return product;
    }

    public static MediaProduct CreateAndLink(MicroLimsDbContext db, Material material, string name, string code)
    {
        var product = CreateOrGet(db, name, code);
        Link(material, product);
        db.SaveChanges();
        return product;
    }

    public static void Link(Material material, MediaProduct product)
    {
        material.MediaProductId = product.Id;
        material.MediaProduct = product;
        material.MaterialName = product.Name;
        material.Code = product.Code;
    }

    public static MediaIncubationCondition Condition(MediaProduct product, int minHours = 24, int maxHours = 48, decimal tempMin = 30, decimal tempMax = 35) =>
        new()
        {
            MediaProductId = product.Id,
            IncubationMinHours = minHours,
            IncubationMaxHours = maxHours,
            TemperatureMin = tempMin,
            TemperatureMax = tempMax
        };

    public static async Task<MediaIncubationCondition> AddConditionAsync(MicroLimsDbContext db, MediaProduct product, int minHours = 24, int maxHours = 48, decimal tempMin = 30, decimal tempMax = 35)
    {
        var condition = Condition(product, minHours, maxHours, tempMin, tempMax);
        db.MediaIncubationConditions.Add(condition);
        await db.SaveChangesAsync();
        return condition;
    }

    public static void Link(MediaConfiguration config, MediaProduct product)
    {
        config.MediaProductId = product.Id;
        config.MediaProduct = product;
        config.Name = product.Name;
    }
}
