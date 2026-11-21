using System;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterAutoCaptureService : IDisposable {
	private const uint BeastmasterClassJobId=43u;
	private const uint SmashAxeActionId=44879u;
	private const uint CaptureActionId=44880u;
	private const uint AxebladeBiteActionId=44883u;
	private const uint ShieldsplitterActionId=44885u;
	private const uint CapturingInterestStatusId=4624u;
	private const uint InterestCapturedStatusId=4626u;
	private const uint BeastmasterUltimateActionId=47093u;
	private const uint BeastmasterReleaseBaseActionId=44890u;
	private readonly BeastmasterConfiguration configuration;
	private readonly uint smashActionId;
	private readonly uint biteActionId;
	private readonly uint shieldActionId;
	private readonly uint captureActionId;
	private readonly uint captureStatusId;
	private DateTime nextCheckUtc=DateTime.MinValue;
	private DateTime nextActionUtc=DateTime.MinValue;
	private DateTime capturePendingUntilUtc=DateTime.MinValue;
	private ulong captureTargetId;
	private uint pendingCooperationActionId;
	private uint pendingCooperationStatusId;
	private DateTime pendingCooperationUntilUtc=DateTime.MinValue;
	private DateTime nextReleaseAttemptUtc=DateTime.MinValue;
	private bool reportedMissingData;
	private (uint,string) loggedCaptureRefusal;
	private readonly BeastmasterProgressService? progressService;
	public string StatusText { get; private set; }="Waiting for a target";
	public string NextActionName { get; private set; }="-";
	public string NextActionReason { get; private set; }="";
	public string ResourceStatus { get; private set; }="Gauge not read";
	public string AdvancedActionStatus { get; private set; }="Not evaluated";
	public string TargetStatus { get; private set; }="No valid target";
	public float TargetHpPercent { get; private set; }
	public string CaptureState { get; private set; }="Not started";
	public string ManualActionStatus { get; private set; }="Not run";
	public bool IsEnabled => configuration.AutoCaptureEnabled;
	public bool TryCapture => RotationToggleOverrides.Get("Capture",configuration.AutoCaptureTryCapture);
	public BeastmasterAutoCaptureService(BeastmasterConfiguration configuration,BeastmasterProgressService? progressService=null) {
		this.configuration=configuration;
		this.progressService=progressService;
		smashActionId=44879u;
		biteActionId=44883u;
		shieldActionId=44885u;
		captureActionId=44880u;
		captureStatusId=4624u;
		DalamudApi.Framework.Update+=OnFrameworkUpdate;
	}
	public void SetEnabled(bool enabled) {
		configuration.AutoCaptureEnabled=enabled;
		configuration.Save();
		if(enabled) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] Auto-capture enabled. It only acts on the target you have selected manually.");
		}
		else {
			DalamudApi.ChatGui.Print("[Beast Master Helper] Auto-capture disabled.");
		}
	}
	public void SetTryCapture(bool enabled) {
		configuration.AutoCaptureTryCapture=enabled;
		configuration.Save();
	}
	public unsafe bool TryUseUltimate() {
		if(!configuration.AdvancedActionsEnabled) {
			ManualActionStatus="Advanced actions are switched off";
			return false;
		}
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		BeastmasterCatalogEntry summonEntry=beastmasterGaugeSnapshot.SummonEntry;
		if(!beastmasterGaugeSnapshot.Available||summonEntry==null) {
			ManualActionStatus="Gauge or current familiar unavailable";
			return false;
		}
		if(beastmasterGaugeSnapshot.Tp<100||beastmasterGaugeSnapshot.BeastPower<100) {
			ManualActionStatus=$"Not enough resources: Tenacity {beastmasterGaugeSnapshot.Tp}/250, Beast Power {beastmasterGaugeSnapshot.BeastPower}/250";
			return false;
		}
		if(!(DalamudApi.TargetManager.Target is IBattleChara { ObjectKind: ObjectKind.BattleNpc,IsTargetable: not false,IsDead: false,CurrentHp: not 0u } battleChara)) {
			ManualActionStatus="Current target is not valid";
			return false;
		}
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			ManualActionStatus="ActionManager unavailable";
			return false;
		}
		uint actionId=47093u;
		uint actionStatus=ptr->GetActionStatus(ActionType.Action,actionId,battleChara.GameObjectId,checkRecastActive: true,checkCastingActive: true,null);
		if(actionStatus!=0&&!TryUseActionWithStatusFallback(ptr,actionId,battleChara.GameObjectId,actionStatus)) {
			ManualActionStatus=$"The ultimate is unavailable right now (status code {actionStatus})";
			return false;
		}
		if(actionStatus==0&&!ptr->UseAction(ActionType.Action,actionId,battleChara.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null)) {
			ManualActionStatus="Ultimate request failed";
			return false;
		}
		nextActionUtc=DateTime.UtcNow.AddMilliseconds(700.0);
		if((configuration.BeastHeartCooperationEnabled||configuration.BeastSoulCooperationEnabled)&&TryGetCooperationAction(beastmasterGaugeSnapshot,configuration.BeastHeartCooperationEnabled,out var firstActionId,out var secondActionId,out var requiredStatusId)&&firstActionId==47093) {
			pendingCooperationActionId=secondActionId;
			pendingCooperationStatusId=requiredStatusId;
			pendingCooperationUntilUtc=DateTime.UtcNow.AddSeconds(4.0);
		}
		ManualActionStatus="Requested: "+GetActionName(actionId);
		return true;
	}
	private unsafe static bool TryUseActionWithStatusFallback(ActionManager* actionManager,uint actionId,ulong targetId,uint actionStatus) {
		if(WithinReach(actionId,targetId)) {
			return actionManager->UseAction(ActionType.Action,actionId,targetId,0u,ActionManager.UseActionMode.None,0u,null);
		}
		return false;
	}
	private unsafe static bool TryUseAdvancedAction(ActionManager* actionManager,uint actionId,ulong targetId,uint actionStatus) {
		if(WithinReach(actionId,targetId)) {
			return actionManager->UseAction(ActionType.Action,actionId,targetId,0u,ActionManager.UseActionMode.None,0u,null);
		}
		return false;
	}
	private static bool WithinReach(uint actionId,ulong targetId) {
		if(BeastmasterActionRanges.IsSelfCast(actionId)) {
			return true;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return true;
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item.GameObjectId==targetId) {
				return BeastmasterActionRanges.InRange(actionId,BeastmasterActionRanges.EdgeDistance(localPlayer,item));
			}
		}
		return true;
	}
	public void Dispose() {
		DalamudApi.Framework.Update-=OnFrameworkUpdate;
	}
	private unsafe void OnFrameworkUpdate(IFramework framework) {
		if(!configuration.AutoCaptureEnabled) {
			StatusText="Auto-capture is off";
			NextActionName="-";
			NextActionReason="Auto-attack not enabled";
			ResourceStatus="Gauge not read";
			AdvancedActionStatus="Auto-attack is not enabled";
			TargetStatus="No valid target";
			TargetHpPercent=0f;
			ResetCaptureState();
			return;
		}
		DateTime utcNow=DateTime.UtcNow;
		if(utcNow<nextCheckUtc) {
			return;
		}
		nextCheckUtc=utcNow.AddMilliseconds(100.0);
		if(utcNow<nextActionUtc||DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.Mounted]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]) {
			StatusText="Waiting until actions can be used";
			NextActionReason="The character cannot act right now";
			return;
		}
		IPlayerCharacter player=DalamudApi.ObjectTable.LocalPlayer;
		if(player==null) {
			StatusText="Waiting for the character to load";
			NextActionName="-";
			NextActionReason="The character has not loaded yet";
			return;
		}
		if(player.ClassJob.RowId!=43) {
			StatusText="Switch to Beastmaster";
			NextActionName="-";
			NextActionReason="Current job is not Beastmaster";
			ResourceStatus="Beastmaster only";
			return;
		}
		if(player.CurrentHp==0||player.IsCasting) {
			StatusText="Waiting until actions can be used";
			NextActionReason="The character is dead or casting";
			return;
		}
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		ResourceStatus=(beastmasterGaugeSnapshot.Available?$"Tenacity {beastmasterGaugeSnapshot.Tp}/250, Beast Power {beastmasterGaugeSnapshot.BeastPower}/250":beastmasterGaugeSnapshot.Status);
		AdvancedActionStatus=GetAdvancedActionStatus(beastmasterGaugeSnapshot);
		if(pendingCooperationActionId!=0&&pendingCooperationUntilUtc<=utcNow) {
			pendingCooperationActionId=0u;
			pendingCooperationStatusId=0u;
			pendingCooperationUntilUtc=DateTime.MinValue;
		}
		if(smashActionId==0||biteActionId==0||shieldActionId==0||captureActionId==0||captureStatusId==0) {
			if(!reportedMissingData) {
				reportedMissingData=true;
				StatusText="Could not parse the action or status data";
				NextActionName="-";
				NextActionReason="The actions or statuses needed for auto-attack are missing";
				SetEnabled(enabled: false);
				DalamudApi.ChatGui.Print("[Beast Master Helper] Could not resolve the actions or statuses auto-capture needs from the client. Please check the Debug tab and report what it shows.");
			}
			return;
		}
		if(!(DalamudApi.TargetManager.Target is IBattleChara { ObjectKind: ObjectKind.BattleNpc,IsTargetable: not false,IsDead: false,CurrentHp: not 0u } battleChara)) {
			StatusText="Waiting for a hostile target";
			NextActionName="-";
			NextActionReason="No valid BattleNpc target";
			TargetStatus="No valid target";
			TargetHpPercent=0f;
			ResetCaptureState();
			ResetCooperationState();
			return;
		}
		if(captureTargetId!=battleChara.EntityId) {
			captureTargetId=battleChara.EntityId;
			capturePendingUntilUtc=DateTime.MinValue;
			CaptureState="Not started";
			ResetCooperationState();
		}
		ActionManager* ptr=ActionManager.Instance();
		if(ptr==null) {
			StatusText="Waiting on the action system";
			NextActionReason="ActionManager unavailable";
			return;
		}
		bool flag=battleChara.StatusList.Any((IStatus status) => status.StatusId==captureStatusId&&status.SourceId==player.EntityId);
		bool flag2=battleChara.StatusList.Any((IStatus status) => status.StatusId==captureStatusId&&status.SourceId!=player.EntityId);
		float num=((battleChara.MaxHp==0)?100f:((float)battleChara.CurrentHp*100f/(float)battleChara.MaxHp));
		TargetHpPercent=Math.Clamp(num,0f,100f);
		BeastmasterAutoCaptureService beastmasterAutoCaptureService=this;
		string targetStatus;
		if(flag) {
			targetStatus="Your capture status is applied";
		}
		else {
			targetStatus=(flag2?"Capture status applied by someone else":"No capture status applied");
		}
		beastmasterAutoCaptureService.TargetStatus=targetStatus;
		if(flag) {
			CaptureState="Your own capture confirmed";
			capturePendingUntilUtc=DateTime.MinValue;
		}
		else if(capturePendingUntilUtc>utcNow) {
			CaptureState="Waiting for the capture result";
		}
		else if(capturePendingUntilUtc!=DateTime.MinValue) {
			CaptureState="Capture status unconfirmed - safe to retry";
			capturePendingUntilUtc=DateTime.MinValue;
		}
		bool flag3=num<=configuration.CaptureHpThreshold;
		if(capturePendingUntilUtc>utcNow) {
			StatusText="Waiting for the capture result";
			NextActionName="-";
			NextActionReason="Waiting for your capture status to refresh";
			return;
		}
		uint actionId;
		if(configuration.AdvancedActionsEnabled&&(configuration.BeastHeartCooperationEnabled||configuration.BeastSoulCooperationEnabled)&&pendingCooperationActionId!=0) {
			actionId=pendingCooperationActionId;
			StatusText="Running cooperation action...";
			NextActionName=GetActionName(actionId);
			NextActionReason="Cooperation stage 2, action chosen by attribute";
			uint actionStatus=ptr->GetActionStatus(ActionType.Action,actionId,battleChara.GameObjectId,checkRecastActive: true,checkCastingActive: true,null);
			if(TryUseAdvancedAction(ptr,actionId,battleChara.GameObjectId,actionStatus)) {
				nextActionUtc=utcNow.AddMilliseconds(700.0);
				ResetCooperationState();
			}
			return;
		}
		if(configuration.AdvancedActionsEnabled&&configuration.AutoReleaseEnabled&&beastmasterGaugeSnapshot.SummonEntry!=null&&TryUseReleaseAction(ptr,battleChara.GameObjectId,utcNow)) {
			return;
		}
		if(configuration.AdvancedActionsEnabled&&(configuration.BeastHeartCooperationEnabled||configuration.BeastSoulCooperationEnabled)&&TryGetCooperationAction(beastmasterGaugeSnapshot,configuration.BeastHeartCooperationEnabled,out var firstActionId,out var secondActionId,out var requiredStatusId)) {
			actionId=firstActionId;
			StatusText="Running cooperation action...";
			NextActionName=GetActionName(actionId);
			NextActionReason="Cooperation stage 1, next: "+GetActionName(secondActionId);
			uint actionStatus2=ptr->GetActionStatus(ActionType.Action,actionId,battleChara.GameObjectId,checkRecastActive: true,checkCastingActive: true,null);
			bool flag4=TryUseAdvancedAction(ptr,actionId,battleChara.GameObjectId,actionStatus2);
			if(!flag4) {
				NextActionReason=$"Cooperation request failed (status code {actionStatus2})";
			}
			if(flag4) {
				nextActionUtc=utcNow.AddMilliseconds(700.0);
				pendingCooperationActionId=secondActionId;
				pendingCooperationStatusId=requiredStatusId;
				pendingCooperationUntilUtc=utcNow.AddSeconds(4.0);
			}
			return;
		}
		string text=string.Empty;
		bool flag5=RotationToggleOverrides.Get("Capture",configuration.AutoCaptureTryCapture)&&!flag;
		if(flag5&&!flag3) {
			flag5=false;
			text=$"Target HP {num:0.#}% is above the capture threshold {configuration.CaptureHpThreshold:0.#}%";
		}
		if(flag5&&BeastmasterCaptureAttempts.AlreadyTried(battleChara.GameObjectId)) {
			flag5=false;
			text="Capture already used on this creature";
		}
		if(flag5) {
			CaptureVerdict captureVerdict=BeastmasterCaptureTargeting.Evaluate(battleChara,player.Level,(ushort)DalamudApi.ClientState.TerritoryType,progressService);
			if(!captureVerdict.ShouldCapture) {
				flag5=false;
				text=captureVerdict.Reason;
			}
		}
		if(!flag5&&!flag&&text.Length>0) {
			(uint,string) tuple=loggedCaptureRefusal;
			uint entityId=battleChara.EntityId;
			string text2=text;
			if(tuple.Item1!=entityId||tuple.Item2!=text2) {
				loggedCaptureRefusal=(battleChara.EntityId,text);
				DalamudApi.Log.Information("Capture skipped for {Name} (level {Level}, territory {Territory}): {Reason}.",battleChara.Name.TextValue,battleChara.Level,DalamudApi.ClientState.TerritoryType,text);
			}
		}
		if(!flag5&&!flag&&text.Length>0) {
			CaptureState=text;
		}
		if(flag5) {
			actionId=captureActionId;
		}
		else if(ptr->Combo.Timer>0f&&ptr->Combo.Action==biteActionId&&player.Level>=12) {
			actionId=shieldActionId;
		}
		else {
			actionId=((ptr->Combo.Timer>0f&&ptr->Combo.Action==smashActionId&&player.Level>=2)?biteActionId:smashActionId);
		}
		StatusText=(flag5?"Auto-capturing...":"Auto-attacking...");
		NextActionName=GetActionName(actionId);
		BeastmasterAutoCaptureService beastmasterAutoCaptureService2=this;
		string nextActionReason;
		if(string.IsNullOrEmpty(text)) {
			if(actionId==47093) {
				nextActionReason="Advanced actions on; Tenacity and Beast Power meet the ultimate threshold";
			}
			else if(flag) {
				nextActionReason="The target already carries your capture status";
			}
			else {
				nextActionReason=(flag2?"The target carries someone else's capture status, which does not affect your own attempt":"The action system allows this");
			}
		}
		else {
			nextActionReason=text;
		}
		beastmasterAutoCaptureService2.NextActionReason=nextActionReason;
		BeastmasterActionAvailability beastmasterActionAvailability=CheckActionAvailability(ptr,actionId,battleChara.GameObjectId);
		NextActionReason=beastmasterActionAvailability.Reason;
		if(!beastmasterActionAvailability.CanUse&&actionId!=47093) {
			return;
		}
		float num2=BeastmasterActionRanges.Of(actionId);
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		float num3=((localPlayer==null)?0f:BeastmasterActionRanges.EdgeDistance(localPlayer,battleChara));
		if(localPlayer!=null&&num2>0f&&num3>num2) {
			NextActionReason=$"{GetActionName(actionId)} reaches {num2:0}y and the target is {num3:0.#}y away";
			return;
		}
		bool num4;
		if(ptr->GetActionStatus(ActionType.Action,actionId,battleChara.GameObjectId,checkRecastActive: true,checkCastingActive: true,null)!=0) {
			if(actionId!=47093) {
				goto IL_0afc;
			}
			num4=ptr->UseAction(ActionType.Action,actionId,battleChara.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null);
		}
		else {
			num4=ptr->UseAction(ActionType.Action,actionId,battleChara.GameObjectId,0u,ActionManager.UseActionMode.None,0u,null);
		}
		if(num4) {
			nextActionUtc=utcNow.AddMilliseconds((actionId==captureActionId)?700:((actionId==47093)?700:250));
			if(actionId==captureActionId) {
				BeastmasterCaptureAttempts.Record(battleChara.GameObjectId);
				capturePendingUntilUtc=utcNow.AddMilliseconds(1200.0);
				CaptureState="Request sent, awaiting the result";
				StatusText="Waiting for the capture result";
				NextActionReason="Waiting for your capture status to refresh";
			}
			return;
		}
		goto IL_0afc;
		IL_0afc:
		if(actionId==captureActionId) {
			CaptureState="Capture request failed";
		}
	}
	private void ResetCaptureState() {
		captureTargetId=0uL;
		capturePendingUntilUtc=DateTime.MinValue;
		CaptureState="Not started";
	}
	private unsafe bool TryUseReleaseAction(ActionManager* actionManager,ulong targetId,DateTime now) {
		if(now<nextReleaseAttemptUtc) {
			return false;
		}
		uint adjustedActionId=actionManager->GetAdjustedActionId(44890u);
		if(adjustedActionId==0) {
			return false;
		}
		string actionName=GetActionName(adjustedActionId);
		if(actionManager->GetActionStatus(ActionType.Action,adjustedActionId,targetId,checkRecastActive: true,checkCastingActive: true,null)!=0) {
			nextReleaseAttemptUtc=now.AddMilliseconds(500.0);
			return false;
		}
		StatusText="Using familiar action...";
		NextActionName=actionName;
		NextActionReason="Release action is off cooldown";
		if(!actionManager->UseAction(ActionType.Action,adjustedActionId,targetId,0u,ActionManager.UseActionMode.None,0u,null)) {
			NextActionReason="Release request failed";
			nextReleaseAttemptUtc=now.AddSeconds(1.0);
			return false;
		}
		nextReleaseAttemptUtc=now.AddMilliseconds(500.0);
		nextActionUtc=now.AddMilliseconds(700.0);
		return true;
	}
	private void ResetCooperationState() {
		pendingCooperationActionId=0u;
		pendingCooperationStatusId=0u;
		pendingCooperationUntilUtc=DateTime.MinValue;
	}
	private bool HasOwnStatus(uint statusId) {
		IPlayerCharacter player=DalamudApi.ObjectTable.LocalPlayer;
		if(player!=null) {
			return player.StatusList.Any((IStatus status) => status.StatusId==statusId&&status.SourceId==player.EntityId);
		}
		return false;
	}
	private static bool TryGetCooperationAction(BeastmasterGaugeSnapshot gauge,bool ultimateFirst,out uint firstActionId,out uint secondActionId,out uint requiredStatusId) {
		firstActionId=0u;
		secondActionId=0u;
		requiredStatusId=0u;
		BeastmasterCatalogEntry summonEntry=gauge.SummonEntry;
		if(summonEntry==null||gauge.Tp<100||gauge.BeastPower<100) {
			return false;
		}
		BeastmasterAttribute beastmasterAttribute=summonEntry.Attribute switch {
			BeastmasterAttribute.Arcane => BeastmasterAttribute.Winged,
			BeastmasterAttribute.Winged => BeastmasterAttribute.Fierce,
			BeastmasterAttribute.Fierce => BeastmasterAttribute.Sturdy,
			BeastmasterAttribute.Sturdy => BeastmasterAttribute.Arcane,
			_ => BeastmasterAttribute.Unknown,
		};
		uint num=beastmasterAttribute switch {
			BeastmasterAttribute.Fierce => 44884u,
			BeastmasterAttribute.Sturdy => 44887u,
			BeastmasterAttribute.Arcane => 44888u,
			BeastmasterAttribute.Winged => 44889u,
			_ => 0u,
		};
		uint num2=summonEntry.Attribute switch {
			BeastmasterAttribute.Fierce => 4596u,
			BeastmasterAttribute.Sturdy => 4597u,
			BeastmasterAttribute.Arcane => 4598u,
			BeastmasterAttribute.Winged => 4595u,
			_ => 0u,
		};
		uint num3=beastmasterAttribute switch {
			BeastmasterAttribute.Fierce => 4596u,
			BeastmasterAttribute.Sturdy => 4597u,
			BeastmasterAttribute.Arcane => 4598u,
			BeastmasterAttribute.Winged => 4595u,
			_ => 0u,
		};
		if(ultimateFirst) {
			firstActionId=47093u;
			secondActionId=num;
			requiredStatusId=num2;
			if(firstActionId!=0) {
				return secondActionId!=0;
			}
			return false;
		}
		firstActionId=num;
		secondActionId=47093u;
		requiredStatusId=num3;
		if(firstActionId!=0) {
			return secondActionId!=0;
		}
		return false;
	}
	private unsafe static BeastmasterActionAvailability CheckActionAvailability(ActionManager* actionManager,uint actionId,ulong targetId) {
		if(actionId==0) {
			return new BeastmasterActionAvailability(0u,"-",CanUse: false,"Invalid ActionId");
		}
		string actionName=GetActionName(actionId);
		uint actionStatus=actionManager->GetActionStatus(ActionType.Action,actionId,targetId,checkRecastActive: true,checkCastingActive: true,null);
		if(actionStatus!=0) {
			return new BeastmasterActionAvailability(actionId,actionName,CanUse: false,$"The action system is unavailable right now (status code {actionStatus})");
		}
		return new BeastmasterActionAvailability(actionId,actionName,CanUse: true,"The action system allows this");
	}
	private string GetAdvancedActionStatus(BeastmasterGaugeSnapshot gauge) {
		if(!configuration.AdvancedActionsEnabled) {
			return "Advanced actions off - conserving resources";
		}
		if(gauge.SummonEntry==null) {
			return "Waiting for a familiar; advanced actions not evaluated yet";
		}
		if(configuration.BeastHeartCooperationEnabled||configuration.BeastSoulCooperationEnabled) {
			return (configuration.BeastHeartCooperationEnabled?"Beast Heart cooperation (yellow)":"Beast Soul cooperation (blue)")+"enabled";
		}
		return "Advanced actions on, but both the ultimate and cooperation actions are disabled";
	}
	private static uint ResolveAction(string name) {
		return (from action in DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()
			where action.RowId!=0&&action.Name.ExtractText().Equals(name,StringComparison.Ordinal)
			orderby action.ClassJob.RowId==43 descending,action.RowId
			select action.RowId).FirstOrDefault();
	}
	private static uint ResolveStatus(string name) {
		return (from status in DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()
			where status.RowId!=0&&status.Name.ExtractText().Equals(name,StringComparison.Ordinal)
			orderby status.RowId
			select status.RowId).FirstOrDefault();
	}
	private static string GetActionName(uint actionId) {
		if(!DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(actionId,out var row)) {
			return $"Action {actionId}";
		}
		return row.Name.ExtractText();
	}
}
