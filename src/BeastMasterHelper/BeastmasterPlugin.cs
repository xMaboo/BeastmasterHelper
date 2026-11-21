using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
namespace Beastmaster;

public sealed class BeastmasterPlugin : IDalamudPlugin,IDisposable {
	private const string CommandName="/bmh";
	private const string LongCommandName="/beastmasterhelper";
	private readonly PluginUI ui;
	private readonly BeastmasterCrucibleService crucibleService;
	private readonly CrucibleBoardService crucibleBoardService;
	private readonly BeastmasterNavigationService navigationService;
	private readonly BeastmasterCatalogChatTracker catalogChatTracker;
	private readonly BeastmasterAutoCaptureService autoCaptureService;
	private readonly BeastmasterRotationRunner rotationRunner;
	private readonly BeastmasterIpc ipc;
	private readonly BossModBridge bossMod;
	public string Name => "Beast Master Helper";
	public BeastmasterConfiguration Configuration { get; }
	public BeastmasterPlugin(IDalamudPluginInterface pluginInterface) {
		DalamudApi.Initialize(pluginInterface);
		Configuration=(pluginInterface.GetPluginConfig() as BeastmasterConfiguration) ?? new BeastmasterConfiguration();
		Configuration.Initialize(pluginInterface);
		BeastmasterProgressService progressService=new BeastmasterProgressService(Configuration);
		BeastmasterQuestService questService=new BeastmasterQuestService();
		BeastmasterDebugDataService debugDataService=new BeastmasterDebugDataService();
		navigationService=new BeastmasterNavigationService(pluginInterface,Configuration);
		catalogChatTracker=new BeastmasterCatalogChatTracker(Configuration,progressService);
		autoCaptureService=new BeastmasterAutoCaptureService(Configuration,progressService);
		bossMod=new BossModBridge(pluginInterface);
		rotationRunner=new BeastmasterRotationRunner(Configuration,new BeastmasterRotationService(Configuration,progressService,bossMod),bossMod);
		crucibleService=new BeastmasterCrucibleService(navigationService,Configuration);
		crucibleBoardService=new CrucibleBoardService(Configuration,navigationService,crucibleService,bossMod);
		ui=new PluginUI(Configuration,progressService,questService,navigationService,debugDataService,autoCaptureService,rotationRunner,crucibleService,crucibleBoardService);
		ipc=new BeastmasterIpc(Configuration,rotationRunner,autoCaptureService);
		DalamudApi.Commands.AddHandler("/bmh",new CommandInfo(OnCommand) {
			HelpMessage = "Open Beast Master Helper (alias: /beastmasterhelper)."
		});
		DalamudApi.Commands.AddHandler("/beastmasterhelper",new CommandInfo(OnCommand) {
			HelpMessage = "Open Beast Master Helper.",
			ShowInHelp = false
		});
		pluginInterface.UiBuilder.Draw+=ui.Draw;
		pluginInterface.UiBuilder.OpenMainUi+=ui.OpenMainWindow;
		pluginInterface.UiBuilder.OpenConfigUi+=ui.OpenMainWindow;
		DalamudApi.Log.Information("Beast Master Helper loaded.");
	}
	public void Dispose() {
		DalamudApi.PluginInterface.UiBuilder.Draw-=ui.Draw;
		DalamudApi.PluginInterface.UiBuilder.OpenMainUi-=ui.OpenMainWindow;
		DalamudApi.PluginInterface.UiBuilder.OpenConfigUi-=ui.OpenMainWindow;
		DalamudApi.Commands.RemoveHandler("/bmh");
		DalamudApi.Commands.RemoveHandler("/beastmasterhelper");
		ipc.Dispose();
		catalogChatTracker.Dispose();
		autoCaptureService.Dispose();
		rotationRunner.Dispose();
		crucibleBoardService.Dispose();
		crucibleService.Dispose();
		navigationService.Dispose();
		Configuration.Save();
	}
	private void OnCommand(string command,string args) {
		ui.OpenMainWindow();
	}
}
