using CommunityToolkit.Mvvm.Input;
using ThirdStart.Models;

namespace ThirdStart.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}