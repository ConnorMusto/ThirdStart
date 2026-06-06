using Microsoft.Extensions.DependencyInjection;

namespace ThirdStart
{
    // Application-level resources and startup logic
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}