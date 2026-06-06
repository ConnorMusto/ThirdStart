using Microsoft.Extensions.Logging;
using ThirdStart.Models;

namespace ThirdStart.Data
{
    /// <summary>
    /// Repository class for managing tasks stored in a CSV file.
    /// </summary>
    public class TaskRepository
    {
        private bool _hasBeenInitialized = false;
        private readonly ILogger _logger;
        private const string Header = "ID,Title,IsCompleted";

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public TaskRepository(ILogger<TaskRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Ensures the CSV file exists, copying the bundled seed file from app package if available.
        /// </summary>
        private async Task Init()
        {
            if (_hasBeenInitialized)
                return;

            try
            {
                var masterList = Constants.MasterListPath;
                if (!File.Exists(masterList))
                {
                    // Use a bundled Resources/Raw/tasks.csv seed file if present
                    if (await FileSystem.AppPackageFileExistsAsync(Constants.SeedDataFileName))
                    {
                        await using var assetStream = await FileSystem.OpenAppPackageFileAsync(Constants.SeedDataFileName);
                        await using var destStream = File.Create(masterList);
                        await assetStream.CopyToAsync(destStream);
                    }
                    else
                    {
                        // Create an empty CSV with just the header
                        await File.WriteAllTextAsync(masterList, Header + Environment.NewLine);
                    }
                }

                _hasBeenInitialized = true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error initializing tasks CSV");
                throw;
            }
        }

        /// <summary>
        /// Reads all tasks from the CSV file.
        /// </summary>
        private async Task<List<ProjectTask>> ReadAllAsync()
        {
            var tasks = new List<ProjectTask>();
            var lines = await File.ReadAllLinesAsync(Constants.MasterListPath);

            foreach (var line in lines.Skip(1)) // Skip header row
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = ParseCsvLine(line);
                //make sure we have at least as many parts as we expect in the headerto avoid index out of range errors
                if (parts.Length < Header.Split(',').Length) continue;

                tasks.Add(new ProjectTask
                {
                    ID = int.Parse(parts[0]),
                    Title = parts[1],
                    IsCompleted = bool.Parse(parts[2])
                });
            }

            return tasks;
        }

        /// <summary>
        /// Writes all tasks back to the CSV file.
        /// </summary>
        private async Task WriteAllAsync(List<ProjectTask> tasks)
        {
            var lines = tasks
                .Select(t => $"{t.ID},{EscapeCsvField(t.Title)},{t.IsCompleted}")
                .Prepend(Header);

            await File.WriteAllLinesAsync(Constants.MasterListPath, lines);
        }

        // Wraps fields containing commas or quotes in double-quotes
        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        // Basic CSV line parser that handles quoted fields
        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else current.Append(c);
                }
            }

            result.Add(current.ToString());
            return [.. result];
        }

        /// <summary>
        /// Retrieves a list of all tasks from the CSV.
        /// </summary>
        public async Task<List<ProjectTask>> ListAsync()
        {
            await Init();
            return await ReadAllAsync();
        }


        /// <summary>
        /// Retrieves a specific task by its ID.
        /// </summary>
        public async Task<ProjectTask?> GetAsync(int id)
        {
            await Init();
            var tasks = await ReadAllAsync();
            return tasks.FirstOrDefault(t => t.ID == id);
        }

        /// <summary>
        /// Saves a task. Inserts if ID is 0, otherwise updates the existing record.
        /// </summary>
        public async Task<int> SaveItemAsync(ProjectTask item)
        {
            await Init();
            var tasks = await ReadAllAsync();

            if (item.ID == 0)
            {
                item.ID = tasks.Count > 0 ? tasks.Max(t => t.ID) + 1 : 1;
                tasks.Add(item);
            }
            else
            {
                var index = tasks.FindIndex(t => t.ID == item.ID);
                if (index >= 0)
                    tasks[index] = item;
            }

            await WriteAllAsync(tasks);
            return item.ID;
        }

        /// <summary>
        /// Deletes a task from the CSV.
        /// </summary>
        /// <returns>The number of rows removed.</returns>
        public async Task<int> DeleteItemAsync(ProjectTask item)
        {
            await Init();
            var tasks = await ReadAllAsync();
            var removed = tasks.RemoveAll(t => t.ID == item.ID);
            await WriteAllAsync(tasks);
            return removed;
        }

        /// <summary>
        /// Clears all tasks from the CSV, leaving only the header row.
        /// </summary>
        public async Task DropTableAsync()
        {
            await File.WriteAllTextAsync(Constants.MasterListPath, Header + Environment.NewLine);
            _hasBeenInitialized = false;
        }
    }
}