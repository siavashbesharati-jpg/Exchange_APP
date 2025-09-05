using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

// Simple test to verify DbContext compiles correctly for migration generation
class TestDbContext
{
    static void Main()
    {
        Console.WriteLine("Starting DbContext test...");
        
        try
        {
            var options = new DbContextOptionsBuilder<ForexDbContext>()
                .UseSqlite("Data Source=test.db")
                .Options;
                
            using var context = new ForexDbContext(options);
            
            Console.WriteLine("DbContext instantiated successfully!");
            Console.WriteLine($"Entity types count: {context.Model.GetEntityTypes().Count()}");
            
            // List all entity types
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                Console.WriteLine($"Entity: {entityType.ClrType.Name}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("Test completed.");
    }
}
