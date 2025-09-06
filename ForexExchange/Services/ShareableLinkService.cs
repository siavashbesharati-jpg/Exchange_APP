using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public interface IShareableLinkService
    {
        Task<ShareableLink> GenerateLinkAsync(int customerId, ShareableLinkType linkType, int expirationDays = 7, string? description = null, string? createdBy = null);
        Task<ShareableLink?> GetValidLinkAsync(string token);
        Task<bool> ValidateLinkAsync(string token);
        Task MarkLinkAccessedAsync(string token);
        Task<bool> DeactivateLinkAsync(int linkId, string? deactivatedBy = null);
        Task<List<ShareableLink>> GetCustomerLinksAsync(int customerId, bool activeOnly = true);
        Task CleanupExpiredLinksAsync();
    }

    public class ShareableLinkService : IShareableLinkService
    {
        private readonly ForexDbContext _context;

        public ShareableLinkService(ForexDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Generate a new shareable link for a customer
        /// </summary>
        public async Task<ShareableLink> GenerateLinkAsync(
            int customerId, 
            ShareableLinkType linkType, 
            int expirationDays = 7, 
            string? description = null, 
            string? createdBy = null)
        {
            // Validate customer exists
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                throw new ArgumentException($"Customer with ID {customerId} not found.");
            }

            // Generate unique token
            string token;
            bool isUnique;
            int attempts = 0;
            const int maxAttempts = 10;

            do
            {
                token = ShareableLink.GenerateToken();
                isUnique = !await _context.ShareableLinks.AnyAsync(sl => sl.Token == token);
                attempts++;
            } 
            while (!isUnique && attempts < maxAttempts);

            if (!isUnique)
            {
                throw new InvalidOperationException("Unable to generate unique token after multiple attempts.");
            }

            var shareableLink = new ShareableLink
            {
                Token = token,
                CustomerId = customerId,
                LinkType = linkType,
                CreatedAt = DateTime.Now,
                ExpiresAt = expirationDays == 0 ? DateTime.MaxValue : DateTime.Now.AddDays(expirationDays),
                IsActive = true,
                CreatedBy = createdBy,
                Description = description,
                AccessCount = 0
            };

            _context.ShareableLinks.Add(shareableLink);
            await _context.SaveChangesAsync();

            return shareableLink;
        }

        /// <summary>
        /// Get a valid (active and not expired) shareable link by token
        /// </summary>
        public async Task<ShareableLink?> GetValidLinkAsync(string token)
        {
            return await _context.ShareableLinks
                .Include(sl => sl.Customer)
                .FirstOrDefaultAsync(sl => 
                    sl.Token == token && 
                    sl.IsActive && 
                    sl.ExpiresAt > DateTime.Now);
        }

        /// <summary>
        /// Validate if a token is valid
        /// </summary>
        public async Task<bool> ValidateLinkAsync(string token)
        {
            return await _context.ShareableLinks
                .AnyAsync(sl => 
                    sl.Token == token && 
                    sl.IsActive && 
                    sl.ExpiresAt > DateTime.Now);
        }

        /// <summary>
        /// Mark a link as accessed (increment access count and update last accessed time)
        /// </summary>
        public async Task MarkLinkAccessedAsync(string token)
        {
            var link = await _context.ShareableLinks
                .FirstOrDefaultAsync(sl => sl.Token == token);

            if (link != null)
            {
                link.AccessCount++;
                link.LastAccessedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Deactivate a shareable link
        /// </summary>
        public async Task<bool> DeactivateLinkAsync(int linkId, string? deactivatedBy = null)
        {
            var link = await _context.ShareableLinks.FindAsync(linkId);
            if (link == null)
            {
                return false;
            }

            link.IsActive = false;
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Get all shareable links for a customer
        /// </summary>
        public async Task<List<ShareableLink>> GetCustomerLinksAsync(int customerId, bool activeOnly = true)
        {
            var query = _context.ShareableLinks
                .Where(sl => sl.CustomerId == customerId);

            if (activeOnly)
            {
                query = query.Where(sl => sl.IsActive && sl.ExpiresAt > DateTime.Now);
            }

            return await query
                .OrderByDescending(sl => sl.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Clean up expired links (can be called periodically)
        /// </summary>
        public async Task CleanupExpiredLinksAsync()
        {
            var expiredLinks = await _context.ShareableLinks
                .Where(sl => sl.ExpiresAt <= DateTime.Now)
                .ToListAsync();

            foreach (var link in expiredLinks)
            {
                link.IsActive = false;
            }

            if (expiredLinks.Any())
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
