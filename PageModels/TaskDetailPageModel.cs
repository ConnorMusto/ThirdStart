using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThirdStart.Data;
using ThirdStart.Models;
using ThirdStart.Services;

namespace ThirdStart.PageModels
{
    public partial class TaskDetailPageModel : ObservableObject, IQueryAttributable
    {
        public const string ProjectQueryKey = "project";
        private ProjectTask? _task;
        private bool _canDelete;
        private readonly TaskRepository _taskRepository;
        private readonly ModalErrorHandler _errorHandler;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private int _selectedProjectIndex = -1;


        [ObservableProperty]
        private bool _isExistingProject;

        public TaskDetailPageModel(TaskRepository taskRepository, ModalErrorHandler errorHandler)
        {
            _taskRepository = taskRepository;
            _errorHandler = errorHandler;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            LoadTaskAsync(query).FireAndForgetSafeAsync(_errorHandler);
        }

        private async Task LoadTaskAsync(IDictionary<string, object> query)
        {

            int taskId = 0;

            if (query.ContainsKey("id"))
            {
                taskId = Convert.ToInt32(query["id"]);
                _task = await _taskRepository.GetAsync(taskId);

                if (_task is null)
                {
                    _errorHandler.HandleError(new Exception($"Task Id {taskId} isn't valid."));
                    return;
                }
            }
            else
            {
                _task = new ProjectTask();
            }
        }

        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                _canDelete = value;
                DeleteCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (_task is null)
            {
                _errorHandler.HandleError(
                    new Exception("Task or project is null. The task could not be saved."));

                return;
            }

            _task.Title = Title;

            _task.IsCompleted = IsCompleted;

            await Shell.Current.GoToAsync("..?refresh=true");

            if (_task.ID > 0)
                await AppShell.DisplayToastAsync("Task saved");
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task Delete()
        {
            if (_task is null)
            {
                _errorHandler.HandleError(
                    new Exception("Task is null. The task could not be deleted."));

                return;
            }

            if (_task.ID > 0)
                await _taskRepository.DeleteItemAsync(_task);

            await Shell.Current.GoToAsync("..?refresh=true");
            await AppShell.DisplayToastAsync("Task deleted");
        }
    }
}