using CommandSystem;
using ProjectMER.Features.Objects;

namespace ProjectMER.Commands.Utility;

public class Indicators : ICommand
{
	public string Command => "indicators";

	public string[] Aliases => ["i", "si"];

	public string Description => "Shows indicators for invisible objects.";

	public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
	{
		if (!CommandAccess.IsRemoteAdmin(sender))
		{
			response = "Remote Admin access is required.";
			return false;
		}

		if (IndicatorObject.Dictionary.Count > 0)
		{
			IndicatorObject.ClearIndicators();
			response = "Removed all indicators!";
			return true;
		}

		IndicatorObject.RefreshIndicators();
		response = "Indicators have been shown!";
		return true;
	}
}
