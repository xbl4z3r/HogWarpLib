namespace HogWarp.Lib.Commands
{
    public class CommandArgument
    {
        public required string Name;
        public required string Description;
        public object? Value = null;
        public bool Required = false;
        
        public static CommandArgument Create(string name, string description, bool required)
        {
            return new CommandArgument
            {
                Name = name,
                Description = description,
                Required = required
            };
        }
    }
}

