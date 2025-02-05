
namespace networking {
	public interface ICommandLineContext {
		CommandLineInput Input { get; }
		string Name { get; set; }
	}
}
