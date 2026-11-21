using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterNavigationService : IDisposable {
	private readonly BeastmasterConfiguration configuration;
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly ICallGateSubscriber<bool> isReady;
	private readonly ICallGateSubscriber<Vector3,bool,bool> pathfindAndMoveTo;
	private readonly ICallGateSubscriber<Vector3,float,float,Vector3?> nearestPoint;
	private readonly ICallGateSubscriber<object> stop;
	private readonly ICallGateSubscriber<uint,byte,bool> teleport;
	private BeastmasterQuestLocation? pendingLocation;
	private bool pendingLocationUseFieldNavigation;
	private BeastmasterQuestLocation? pendingVnavLocation;
	private bool pendingVnavFieldNavigation;
	private DateTime nextVnavCheckUtc=DateTime.MinValue;
	private DateTime pendingStartedUtc;
	private BeastmasterQuestLocation? pendingMountLocation;
	private DateTime pendingMountStartedUtc;
	private DateTime lastMountAttemptUtc=DateTime.MinValue;
	public bool IsVnavmeshInstalled => IsPluginLoaded("vnavmesh");
	public bool IsLifestreamInstalled => IsPluginLoaded("Lifestream");
	public BeastmasterNavigationService(IDalamudPluginInterface pluginInterface,BeastmasterConfiguration configuration) {
		this.pluginInterface=pluginInterface;
		this.configuration=configuration;
		isReady=pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
		pathfindAndMoveTo=pluginInterface.GetIpcSubscriber<Vector3,bool,bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
		nearestPoint=pluginInterface.GetIpcSubscriber<Vector3,float,float,Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
		stop=pluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
		teleport=pluginInterface.GetIpcSubscriber<uint,byte,bool>("Lifestream.Teleport");
		DalamudApi.Framework.Update+=OnFrameworkUpdate;
	}
	public void Dispose() {
		DalamudApi.Framework.Update-=OnFrameworkUpdate;
		Stop();
	}
	public bool Navigate(BeastmasterQuestLocation location) {
		if(DalamudApi.ObjectTable.LocalPlayer==null) {
			return false;
		}
		if(!IsVnavmeshInstalled) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] vnavmesh is not loaded, so navigation cannot start.");
			return false;
		}
		SetMapFlag(location);
		if(DalamudApi.ClientState.TerritoryType!=location.TerritoryType) {
			return TeleportAndContinue(location,useFieldNavigation: true);
		}
		return StartFieldNavigation(location);
	}
	public bool NavigateQuestTarget(BeastmasterQuestLocation location) {
		if(DalamudApi.ObjectTable.LocalPlayer==null||!IsVnavmeshInstalled) {
			if(!IsVnavmeshInstalled) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] vnavmesh is not loaded, so navigation cannot start.");
			}
			return false;
		}
		SetMapFlag(location);
		if(DalamudApi.ClientState.TerritoryType==location.TerritoryType) {
			return StartFieldNavigation(location);
		}
		return TeleportAndContinue(location,useFieldNavigation: true);
	}
	public bool Navigate(BeastmasterCatalogEntry entry) {
		if(entry.LocationType!=BeastmasterCatalogLocationType.Field||!entry.MapX.HasValue||!entry.MapY.HasValue) {
			return false;
		}
		ExcelSheet<Lumina.Excel.Sheets.Map> excelSheet=DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
		Lumina.Excel.Sheets.Map map=excelSheet.FirstOrDefault((Lumina.Excel.Sheets.Map candidate) => candidate.TerritoryType.RowId!=0&&candidate.SizeFactor>0&&(entry.TerritoryType==0||candidate.TerritoryType.RowId==entry.TerritoryType)&&(entry.MapRowId==0||candidate.RowId==entry.MapRowId)&&candidate.PlaceName.Value.Name.ExtractText().Equals(entry.Location,StringComparison.Ordinal));
		if(map.RowId==0) {
			map=excelSheet.FirstOrDefault((Lumina.Excel.Sheets.Map candidate) => candidate.TerritoryType.RowId!=0&&candidate.SizeFactor>0&&candidate.PlaceName.Value.Name.ExtractText().Equals(entry.Location,StringComparison.Ordinal));
		}
		if(map.RowId==0&&(!entry.WorldX.HasValue||!entry.WorldY.HasValue||!entry.WorldZ.HasValue)) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] "+entry.Location+" was not found in the client map table.");
			return false;
		}
		Vector3 position=((entry.WorldX.HasValue&&entry.WorldY.HasValue&&entry.WorldZ.HasValue)?new Vector3(entry.WorldX.Value,entry.WorldY.Value,entry.WorldZ.Value):new Vector3(50f*entry.MapX.Value-(float)map.OffsetX-102400f/(float)(int)map.SizeFactor-50f,0f,50f*entry.MapY.Value-(float)map.OffsetY-102400f/(float)(int)map.SizeFactor-50f));
		BeastmasterQuestLocation beastmasterQuestLocation=new BeastmasterQuestLocation((entry.TerritoryType!=0)?entry.TerritoryType:map.TerritoryType.RowId,(entry.MapRowId!=0)?entry.MapRowId:map.RowId,position,entry.Location,entry.Name);
		SetMapFlag(beastmasterQuestLocation);
		if(DalamudApi.ClientState.TerritoryType!=beastmasterQuestLocation.TerritoryType) {
			return TeleportAndContinue(beastmasterQuestLocation,useFieldNavigation: true);
		}
		return StartFieldNavigation(beastmasterQuestLocation);
	}
	public unsafe bool OpenDutyFinder(BeastmasterCatalogEntry entry) {
		if(entry.LocationType!=BeastmasterCatalogLocationType.Duty) {
			return false;
		}
		ContentFinderCondition contentFinderCondition=((entry.ContentFinderConditionId!=0)?DalamudApi.DataManager.GetExcelSheet<ContentFinderCondition>().FirstOrDefault((ContentFinderCondition candidate) => candidate.RowId==entry.ContentFinderConditionId):default(ContentFinderCondition));
		if(contentFinderCondition.RowId==0) {
			string normalizedDutyName=NormalizeDutyName(entry.Location);
			contentFinderCondition=DalamudApi.DataManager.GetExcelSheet<ContentFinderCondition>().FirstOrDefault((ContentFinderCondition candidate) => candidate.TerritoryType.RowId!=0&&NormalizeDutyName(candidate.Name.ExtractText()).Equals(normalizedDutyName,StringComparison.Ordinal));
		}
		if(contentFinderCondition.RowId==0) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] \""+entry.Location+"\" was not found in the Duty Finder.");
			return false;
		}
		try {
			AgentContentsFinder.Instance()->OpenRegularDuty(contentFinderCondition.RowId);
			return true;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Failed to open Duty Finder for {DutyName}.",entry.Location);
			DalamudApi.ChatGui.Print("[Beast Master Helper] Could not open the Duty Finder: "+ex.Message);
			return false;
		}
	}
	public bool MoveTo(Vector3 destination) {
		if(!IsVnavmeshInstalled) {
			return false;
		}
		try {
			if(!isReady.InvokeFunc()) {
				return false;
			}
			return pathfindAndMoveTo.InvokeFunc(destination,arg2: false);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not move to {Destination}.",destination);
			return false;
		}
	}
	public void Stop() {
		pendingLocation=null;
		pendingLocationUseFieldNavigation=false;
		pendingVnavLocation=null;
		pendingMountLocation=null;
		try {
			stop.InvokeAction();
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Failed to stop vnavmesh navigation.");
		}
	}
	private bool StartFieldNavigation(BeastmasterQuestLocation location) {
		if(!IsVnavmeshInstalled||DalamudApi.ObjectTable.LocalPlayer==null) {
			return false;
		}
		try {
			if(!isReady.InvokeFunc()) {
				pendingVnavLocation=location;
				pendingVnavFieldNavigation=true;
				DalamudApi.ChatGui.Print("[Beast Master Helper] vnavmesh is not ready yet; navigation will start automatically once it is. ");
				return true;
			}
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			Vector3 vector;
			if(localPlayer!=null&&!location.GroundOnly&&location.Position.Y==0f) {
				Vector3 position=location.Position;
				position.Y=localPlayer.Position.Y;
				vector=position;
			}
			else {
				vector=location.Position;
			}
			Vector3 vector2=vector;
			Vector3 vector3=nearestPoint.InvokeFunc(vector2,120f,300f) ?? vector2;
			bool flag=!location.GroundOnly&&configuration.UseFlightNavigation&&IsFlightUnlocked(location.TerritoryType);
			if(flag&&!DalamudApi.Condition[ConditionFlag.Mounted]) {
				pendingMountLocation=location with {
					Position = vector3
				};
				pendingMountStartedUtc=DateTime.UtcNow;
				lastMountAttemptUtc=DateTime.MinValue;
				if(configuration.ShowNavigationLogs) {
					DalamudApi.ChatGui.Print("[Beast Master Helper] Mounting; flight navigation will continue once mounted. ");
				}
				return true;
			}
			bool flag2=MoveToPoint(vector3,flag);
			if(configuration.ShowNavigationLogs) {
				DalamudApi.ChatGui.Print(flag2?$"[Beast Master Helper] Starting {(flag?"flight":"walking")} navigation to {location.Zone} {location.NpcName}.":("[Beast Master Helper] vnavmesh could not start heading to "+location.NpcName+". "));
			}
			return flag2;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Failed to navigate to Beastmaster catalog location.");
			DalamudApi.ChatGui.Print("[Beast Master Helper] Navigation failed: "+ex.Message);
			return false;
		}
	}
	private bool TeleportAndContinue(BeastmasterQuestLocation location,bool useFieldNavigation) {
		if(!IsLifestreamInstalled) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] The target is in "+location.Zone+" and Lifestream is not loaded - please travel there manually. ");
			return false;
		}
		uint rowId=DalamudApi.DataManager.GetExcelSheet<Aetheryte>().FirstOrDefault((Aetheryte aetheryte) => aetheryte.IsAetheryte&&aetheryte.Territory.RowId==location.TerritoryType).RowId;
		if(rowId==0) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] No usable aetheryte found in "+location.Zone+". ");
			return false;
		}
		try {
			if(!teleport.InvokeFunc(rowId,0)) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] Lifestream did not start the teleport. ");
				return false;
			}
			pendingLocation=location;
			pendingLocationUseFieldNavigation=useFieldNavigation;
			pendingStartedUtc=DateTime.UtcNow;
			DalamudApi.ChatGui.Print("[Beast Master Helper] Teleporting to "+location.Zone+"; navigation resumes after the zone loads. ");
			return true;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Failed to teleport for Beastmaster catalog navigation.");
			DalamudApi.ChatGui.Print("[Beast Master Helper] Lifestream call failed: "+ex.Message);
			return false;
		}
	}
	private void OnFrameworkUpdate(IFramework framework) {
		ProcessPendingVnav();
		ProcessPendingMount();
		if(pendingLocation==null) {
			return;
		}
		if(DateTime.UtcNow-pendingStartedUtc>TimeSpan.FromSeconds(45L)) {
			pendingLocation=null;
			DalamudApi.ChatGui.Print("[Beast Master Helper] Timed out waiting for the teleport; navigation cancelled. ");
		}
		else if(!DalamudApi.Condition[ConditionFlag.BetweenAreas]&&!(DateTime.UtcNow-pendingStartedUtc<TimeSpan.FromSeconds(4L))&&DalamudApi.ObjectTable.LocalPlayer!=null&&DalamudApi.ClientState.TerritoryType==pendingLocation.TerritoryType) {
			BeastmasterQuestLocation location=pendingLocation;
			bool flag=pendingLocationUseFieldNavigation;
			pendingLocation=null;
			pendingLocationUseFieldNavigation=false;
			if(flag) {
				StartFieldNavigation(location);
			}
			else {
				Navigate(location);
			}
		}
	}
	private void ProcessPendingVnav() {
		if(pendingVnavLocation==null||DalamudApi.ObjectTable.LocalPlayer==null) {
			return;
		}
		DateTime utcNow=DateTime.UtcNow;
		if(utcNow<nextVnavCheckUtc) {
			return;
		}
		nextVnavCheckUtc=utcNow.AddMilliseconds(500.0);
		try {
			if(!isReady.InvokeFunc()) {
				return;
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Failed to check vnavmesh readiness.");
			return;
		}
		BeastmasterQuestLocation location=pendingVnavLocation;
		bool flag=pendingVnavFieldNavigation;
		pendingVnavLocation=null;
		pendingVnavFieldNavigation=false;
		if(flag) {
			StartFieldNavigation(location);
		}
		else {
			StartFieldNavigation(location);
		}
	}
	private unsafe void ProcessPendingMount() {
		if(pendingMountLocation==null) {
			return;
		}
		BeastmasterQuestLocation location=pendingMountLocation;
		DateTime utcNow=DateTime.UtcNow;
		if(DalamudApi.Condition[ConditionFlag.Mounted]) {
			pendingMountLocation=null;
			StartPathfind(location,fly: true);
		}
		else if(DalamudApi.Condition[ConditionFlag.InCombat]||utcNow-pendingMountStartedUtc>TimeSpan.FromSeconds(8L)) {
			pendingMountLocation=null;
			if(configuration.ShowNavigationLogs) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] Could not mount; navigating on foot instead. ");
			}
			StartPathfind(location,fly: false);
		}
		else if(!(utcNow-lastMountAttemptUtc<TimeSpan.FromSeconds(2L))) {
			lastMountAttemptUtc=utcNow;
			ActionManager.Instance()->UseAction(ActionType.GeneralAction,9u,3758096384uL,0u,ActionManager.UseActionMode.None,0u,null);
		}
	}
	private bool MoveToPoint(Vector3 point,bool fly) {
		if(configuration.SetFlagOnNavigation) {
			string text=(fly?"/vnav flyflag":"/vnav moveflag");
			if(DalamudApi.Commands.ProcessCommand(text)) {
				DalamudApi.Log.Information("Navigating with {Command} to {Point}.",text,point);
				return true;
			}
		}
		return pathfindAndMoveTo.InvokeFunc(point,fly);
	}
	private bool StartPathfind(BeastmasterQuestLocation location,bool fly) {
		try {
			if(!isReady.InvokeFunc()) {
				pendingVnavLocation=location;
				pendingVnavFieldNavigation=true;
				return true;
			}
			bool flag=MoveToPoint(location.Position,fly);
			if(configuration.ShowNavigationLogs) {
				DalamudApi.ChatGui.Print(flag?$"[Beast Master Helper] Starting {(fly?"flight":"walking")} navigation to {location.Zone} {location.NpcName}.":("[Beast Master Helper] vnavmesh could not start heading to "+location.NpcName+". "));
			}
			return flag;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Failed to start Beastmaster pathfinding.");
			DalamudApi.ChatGui.Print("[Beast Master Helper] Navigation failed: "+ex.Message);
			return false;
		}
	}
	private unsafe static bool IsFlightUnlocked(uint territoryType) {
		if(!DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryType,out var row)||row.AetherCurrentCompFlgSet.RowId==0) {
			return false;
		}
		PlayerState* ptr=PlayerState.Instance();
		if(ptr!=null) {
			return ptr->IsAetherCurrentZoneComplete(row.AetherCurrentCompFlgSet.RowId);
		}
		return false;
	}
	private bool IsPluginLoaded(string internalName) {
		return pluginInterface.InstalledPlugins.Any((IExposedPlugin plugin) => plugin.IsLoaded&&plugin.InternalName.Equals(internalName,StringComparison.OrdinalIgnoreCase));
	}
	private static string NormalizeDutyName(string name) {
		return new string(name.Where((char character) => !char.IsWhiteSpace(character)&&character!='·'&&character!='：'&&character!=':').ToArray());
	}
	private unsafe void SetMapFlag(BeastmasterQuestLocation location) {
		if(!configuration.SetFlagOnNavigation) {
			return;
		}
		try {
			AgentMap.Instance()->SetFlagMapMarker(location.TerritoryType,location.MapRowId,location.Position);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Failed to set Beastmaster quest map flag.");
		}
	}
}
