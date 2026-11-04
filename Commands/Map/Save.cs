using CommandSystem;
using ProjectMER.Features;

namespace ProjectMER.Commands.Map;

public class Save : ICommand
{
	public string Command => "save";

	public string[] Aliases => ["s"];

	public string Description => "Saves a map";

	public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
	{
		if (!CommandAccess.IsRemoteAdmin(sender))
		{
			response = "Remote Admin access is required.";
			return false;
		}

		if (arguments.Count == 0)
		{
			response = "You need to provide a map name!";
			return false;
		}

		MapUtils.SaveMap(arguments.At(0));

		response = $"Map named {arguments.At(0)} has been successfully saved!";
		return true;
	}
}
