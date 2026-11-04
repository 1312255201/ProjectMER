using CommandSystem;
using LabApi.Features.Wrappers;

namespace ProjectMER.Commands;

internal static class CommandAccess
{
	internal static bool IsRemoteAdmin(ICommandSender sender) => Player.Get(sender)?.RemoteAdminAccess == true;
}
