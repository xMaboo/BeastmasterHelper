using System;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
namespace Beastmaster;

internal sealed class DalamudApi {
	[PluginService]
	public static IDalamudPluginInterface PluginInterface { get; private set; }
	[PluginService]
	public static IAddonLifecycle AddonLifecycle { get; private set; }
	[PluginService]
	public static IClientState ClientState { get; private set; }
	[PluginService]
	public static IPlayerState PlayerState { get; private set; }
	[PluginService]
	public static IObjectTable ObjectTable { get; private set; }
	[PluginService]
	public static ITargetManager TargetManager { get; private set; }
	[PluginService]
	public static IJobGauges JobGauges { get; private set; }
	[PluginService]
	public static IGameInventory GameInventory { get; private set; }
	[PluginService]
	public static IDataManager DataManager { get; private set; }
	[PluginService]
	public static IGameInteropProvider GameInterop { get; private set; }
	[PluginService]
	public static IFramework Framework { get; private set; }
	[PluginService]
	public static ICondition Condition { get; private set; }
	[PluginService]
	public static IChatGui ChatGui { get; private set; }
	[PluginService]
	public static ICommandManager Commands { get; private set; }
	[PluginService]
	public static IGameGui GameGui { get; private set; }
	[PluginService]
	public static IPluginLog Log { get; private set; }
	[PluginService]
	public static ITextureProvider Textures { get; private set; }
	[PluginService]
	public static IKeyState KeyState { get; private set; }
	public static void Initialize(IDalamudPluginInterface pluginInterface) {
		pluginInterface.Create<DalamudApi>(Array.Empty<object>());
	}
}
