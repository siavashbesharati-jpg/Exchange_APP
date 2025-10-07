using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using TaskStatus = ForexExchange.Models.TaskStatus;
using Microsoft.AspNetCore.Identity;

namespace ForexExchange.Services
{
    public interface ITaskManagementService
    {
        Task<List<TaskItem>> GetAllTasksAsync();
        Task<List<TaskItem>> GetFilteredTasksAsync(string? assignedUserId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, TaskStatus? status = null);
        Task<TaskItem?> GetTaskByIdAsync(int id);
        Task<TaskItem> CreateTaskAsync(string title, string description, DateTime? dueDate, string? assignedToUserId = null);
        Task<TaskItem> UpdateTaskAsync(int id, string title, string description, DateTime? dueDate, TaskStatus status, string? assignedToUserId = null);
        Task<bool> DeleteTaskAsync(int id);
        Task<bool> UpdateTaskStatusAsync(int id, TaskStatus status, string currentUserId);
        Task<List<ApplicationUser>> GetAvailableUsersAsync();
        Task<bool> CanUserChangeTaskStatusAsync(int taskId, string userId);
    }

    public class TaskManagementService : ITaskManagementService
    {
        private readonly ForexDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskManagementService(ForexDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<TaskItem>> GetAllTasksAsync()
        {
            return await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<TaskItem>> GetFilteredTasksAsync(string? assignedUserId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, TaskStatus? status = null)
        {
            var query = _context.TaskItems
                .Include(t => t.AssignedToUser)
                .AsQueryable();

            // Filter by assigned user
            if (!string.IsNullOrEmpty(assignedUserId))
            {
                query = query.Where(t => t.AssignedToUserId == assignedUserId);
            }

            // Filter by due date range
            if (dueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate >= dueDateFrom.Value);
            }

            if (dueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate <= dueDateTo.Value);
            }

            // Filter by status
            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<TaskItem?> GetTaskByIdAsync(int id)
        {
            return await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<TaskItem> CreateTaskAsync(string title, string description, DateTime? dueDate, string? assignedToUserId = null)
        {
            var task = new TaskItem
            {
                Title = title,
                Description = description,
                DueDate = dueDate,
                CreatedAt = DateTime.UtcNow,
                Status = TaskStatus.Pending,
                AssignedToUserId = assignedToUserId
            };

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            return task;
        }

        public async Task<TaskItem> UpdateTaskAsync(int id, string title, string description, DateTime? dueDate, TaskStatus status, string? assignedToUserId = null)
        {
            var task = await GetTaskByIdAsync(id);
            if (task == null)
                throw new ArgumentException("Task not found");

            task.Title = title;
            task.Description = description;
            task.DueDate = dueDate;
            task.Status = status;
            task.AssignedToUserId = assignedToUserId;

            await _context.SaveChangesAsync();
            return task;
        }

        public async Task<bool> CanUserChangeTaskStatusAsync(int taskId, string currentUserId)
        {
            var task = await _context.TaskItems.FindAsync(taskId);
            if (task == null) return false;

            // Only assigned user can change the task status
            return task.AssignedToUserId == currentUserId;
        }

        public async Task<bool> UpdateTaskStatusAsync(int taskId, TaskStatus status, string currentUserId)
        {
            // Check permission first
            if (!await CanUserChangeTaskStatusAsync(taskId, currentUserId))
            {
                return false;
            }

            var task = await _context.TaskItems.FindAsync(taskId);
            if (task == null) return false;

            task.Status = status;
            _context.TaskItems.Update(task);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var task = await GetTaskByIdAsync(id);
            if (task == null)
                return false;

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateTaskStatusAsync(int id, TaskStatus status)
        {
            var task = await GetTaskByIdAsync(id);
            if (task == null)
                return false;

            task.Status = status;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<ApplicationUser>> GetAvailableUsersAsync()
        {
            return await _userManager.Users.ToListAsync();
        }
    }
}