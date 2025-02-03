using System.Reflection;
using System.Runtime.CompilerServices;

namespace HogWarp.Lib
{
    public static class PluginManager
    {
        private static List<IPluginBase> _plugins = new List<IPluginBase>();

        internal static IEnumerable<IPluginBase> Plugins { get => _plugins; }

        public static void InitializePlugins(Server server)
        {
            foreach(var plugin in Plugins)
            {
                try
                {
                    plugin.Initialize(server);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Logger.Error(ex.ToString());
                }
            }
        }

        public static void LoadFromBase(string relativePath)
        {
            string root = Path.GetFullPath(Path.GetDirectoryName(typeof(PluginManager).Assembly.Location!)!);
            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));

            var locations = Directory.GetDirectories(pluginLocation);
            foreach ( var location in locations )
            {
                try
                {
                    var path = Path.Combine(pluginLocation, location, "Assembly-Plugin.dll");
                    var assembly = LoadPlugin(path);
                    _plugins.AddRange(CreatePlugins(assembly));
                }
                catch( Exception ex ) 
                {
                    Serilog.Log.Logger.Error(ex.ToString());
                }
            }

            Serilog.Log.Logger.Information($"{_plugins.Count} plugin(s) loaded.");
        }

        static Assembly LoadPlugin(string relativePath)
        {
            // Navigate up to the solution root
            string root = Path.GetFullPath(Path.GetDirectoryName(typeof(PluginManager).Assembly.Location!)!);

            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            string rel = Path.GetRelativePath(root, relativePath);
            Serilog.Log.Logger.Information($"Loading: {rel}");
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        static IEnumerable<IPluginBase> CreatePlugins(Assembly assembly) 
        {
            int count = 0;

            foreach(var type in assembly.GetTypes())
            {
                if(typeof(IPluginBase).IsAssignableFrom(type))
                {
                    IPluginBase? result = Activator.CreateInstance(type) as IPluginBase;
                    if(result != null)
                    {
                        Serilog.Log.Logger.Information($"> Loaded {type.FullName}, '{result.Name}' - '{result.Description}'");
                        ++count;
                        yield return result!;
                    }
                }
            }

            // if (count == 0)
            // {
            //     throw new ApplicationException(
            //         $"Can't find any type which implements IPluginBase in {assembly} from {assembly.Location}.");
            //     Serilog.Log.Logger.Warning($"Can't find any type which implements IPluginBase in {assembly} from {assembly.Location}.");
            //     Serilog.Log.Logger.Warning("Is this a plugin?");
            // }
        }
        
        /**
         * Check if a plugin is present by name
         * @param name The name of the plugin. Warning: This is case-sensitive.
         */
        public static bool IsPluginPresent(string name)
        {
            return _plugins.Any(p => p.Name == name);
        }
        
        /**
         * Get a plugin by name 
         * @param name The name of the plugin. Warning: This is case-sensitive.
         */
        public static T GetPlugin<T>(string name) where T : class, IPluginBase
        {
            var plugin = _plugins.First(p => p.Name == name);
            return Unsafe.As<IPluginBase, T>(ref plugin);
        }
    }
}