using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThirdStart.Models;

namespace ThirdStart.PageModels
{
    public partial class MainPageModel : ObservableObject
    {
        private bool _isNavigatedTo;
        private bool _dataLoaded;
        private readonly TaskRepository _taskRepository;
        private readonly ModalErrorHandler _errorHandler;
        private readonly SeedDataService _seedDataService;


        [ObservableProperty]
        private List<Brush> _todoCategoryColors = [];

        [ObservableProperty]
        private List<ProjectTask> _tasks = [];

        [ObservableProperty]
        bool _isBusy;

        [ObservableProperty]
        bool _isRefreshing;

        [ObservableProperty]
        private string _today = DateTime.Now.ToString("dddd, MMM d");

        public bool HasCompletedTasks
            => Tasks?.Any(t => t.IsCompleted) ?? false;

        public MainPageModel(SeedDataService seedDataService,
            TaskRepository taskRepository, ModalErrorHandler errorHandler)
        {
            _taskRepository = taskRepository;
            _errorHandler = errorHandler;
            _seedDataService = seedDataService;
        }

        private async Task LoadData()
        {
            try
            {
                IsBusy = true;

                Tasks = await _taskRepository.ListAsync();
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasCompletedTasks));
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            try
            {
                IsRefreshing = true;
                await LoadData();
            }
            catch (Exception e)
            {
                _errorHandler.HandleError(e);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private void NavigatedTo() =>
            _isNavigatedTo = true;

        [RelayCommand]
        private void NavigatedFrom() =>
            _isNavigatedTo = false;

        [RelayCommand]
        private async Task Appearing()
        {
            if (!_dataLoaded)
            {
                _dataLoaded = true;
                await Refresh();
            }
            // This means we are being navigated to
            else if (!_isNavigatedTo)
            {
                await Refresh();
            }
        }

        [RelayCommand]
        private Task TaskCompleted(ProjectTask task)
        {
            OnPropertyChanged(nameof(HasCompletedTasks));
            return _taskRepository.SaveItemAsync(task);
        }

        [RelayCommand]
        private Task AddTask()
            => Shell.Current.GoToAsync($"task");

        [RelayCommand]
        private Task NavigateToTask(ProjectTask task)
            => Shell.Current.GoToAsync($"task?id={task.ID}");

        [RelayCommand]
        private async Task CleanTasks()
        {
            var completedTasks = Tasks.Where(t => t.IsCompleted).ToList();
            foreach (var task in completedTasks)
            {
                await _taskRepository.DeleteItemAsync(task);
                Tasks.Remove(task);
            }

            OnPropertyChanged(nameof(HasCompletedTasks));
            Tasks = new(Tasks);
            await AppShell.DisplayToastAsync("All cleaned up!");
        }
    }
}