using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DigifyCXIntranet.Data;

public static class EntityValidation
{
    public static void Validate(ChangeTracker changeTracker)
    {
        var entries = changeTracker
            .Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            var context = new ValidationContext(entry.Entity);
            Validator.ValidateObject(entry.Entity, context, validateAllProperties: true);
        }
    }
}
