namespace ZooscapeRunner.ViewModels
{
    public class ProcessViewModel : BindableBase
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _status = "Stopped";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
