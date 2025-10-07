using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? DueDate { get; set; }
        
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        
        // Navigation property for assigned user
        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled
    }
}
