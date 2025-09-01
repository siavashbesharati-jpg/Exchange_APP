using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Scripts
{
    public class UpdateCurrencyDisplayOrder
    {
        public static async Task UpdateDisplayOrderAsync(ForexDbContext context)
        {
            // Update display orders to match the correct order
            var currencies = await context.Currencies.ToListAsync();
            
            foreach (var currency in currencies)
            {
                switch (currency.Code)
                {
                    case "IRR":
                        currency.DisplayOrder = 1;
                        break;
                    case "OMR":
                        currency.DisplayOrder = 2;
                        break;
                    case "AED":
                        currency.DisplayOrder = 3;
                        break;
                    case "USD":
                        currency.DisplayOrder = 4;
                        break;
                    case "EUR":
                        currency.DisplayOrder = 5;
                        break;
                    case "TRY":
                        currency.DisplayOrder = 6;
                        break;
                }
            }
            
            await context.SaveChangesAsync();
            Console.WriteLine("Currency DisplayOrder values updated successfully!");
        }
    }
}
