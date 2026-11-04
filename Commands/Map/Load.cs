using CommandSystem;
using ProjectMER.Features;

namespace ProjectMER.Commands.Map;

public class Load : ICommand
{
	public string Command => "load";

	public string[] Aliases => ["l"];

	public string Description => "Loads a map";

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

		string mapName = arguments.At(0);

		try
		{
			MapUtils.LoadMap(mapName);
		}
		catch (Exception e)
		{
			response = e.Message;
			return false;
		}

		response = $"{mapName} map has been loaded!";
		return true;
	}
}
