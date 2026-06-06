using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThirdStart.Models;

namespace ThirdStart.Data
{
    public class SeedDataService
    {
        private readonly TaskRepository _taskRepository;
        private readonly ILogger<SeedDataService> _logger;

        public SeedDataService(TaskRepository taskRepository, ILogger<SeedDataService> logger)
        {
            _taskRepository = taskRepository;
            _logger = logger;
        }
    }
}