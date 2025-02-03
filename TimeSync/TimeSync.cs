using HogWarp.Lib;

namespace TimeSync
{
    public class TimeSync : IPluginBase
    {
        public string Name => "TimeSync";
        public string Description => "Manage World Time & Season";
        private Server? _server;

        public void Initialize(Server server)
        {
            _server = server;
        }
    }
}