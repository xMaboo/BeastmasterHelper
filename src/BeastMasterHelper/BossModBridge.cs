using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
namespace Beastmaster;

public sealed class BossModBridge {
	private readonly IDalamudPluginInterface pluginInterface;
	private readonly ICallGateSubscriber<bool> isFacingRestricted;
	private readonly ICallGateSubscriber<bool> shouldHoldActions;
	private readonly ICallGateSubscriber<ulong> preferredTargetID;
	private readonly ICallGateSubscriber<bool> shouldClearTarget;
	private readonly ICallGateSubscriber<bool> inCrucibleEncounter;
	private readonly ICallGateSubscriber<bool> preferChallenge;
	private readonly ICallGateSubscriber<bool> forceCancelCastMechanic;
	private readonly ICallGateSubscriber<bool> forceCancelCastMechanicAI;
	private readonly ICallGateSubscriber<int> forbiddenDirectionsCount;
	private readonly ICallGateSubscriber<bool> isMoving;
	private readonly ICallGateSubscriber<bool> isNavigating;
	private readonly ICallGateSubscriber<bool> hasActiveModule;
	private readonly ICallGateSubscriber<string> activeModuleName;
	private readonly ICallGateSubscriber<float> nextDamageIn;
	private readonly ICallGateSubscriber<int> forbiddenZonesCount;
	private readonly ICallGateSubscriber<float> forbiddenZonesNextActivation;
	private readonly ICallGateSubscriber<float> nextTankbusterDamageIn;
	private readonly ICallGateSubscriber<float> nextRaidwideDamageIn;
	private readonly ICallGateSubscriber<float> timelineTankbusterIn;
	private readonly ICallGateSubscriber<float> timelineRaidwideIn;
	private readonly ICallGateSubscriber<int> nextDamageType;
	private readonly ICallGateSubscriber<float> interruptCastRemaining;
	private readonly ICallGateSubscriber<bool> shouldDispel;
	private readonly ICallGateSubscriber<ulong> dispelTargetID;
	private readonly ICallGateSubscriber<Vector3,bool> isPositionSafe;
	private readonly ICallGateSubscriber<Vector3,Vector3,bool> isDashSafe;
	private readonly ICallGateSubscriber<string,string,string,string,bool> presetAddTransientStrategy;
	private readonly ICallGateSubscriber<string,bool,bool> presetCreate;
	private readonly ICallGateSubscriber<string,string?> presetGet;
	private readonly ICallGateSubscriber<string,bool> presetDelete;
	private readonly ICallGateSubscriber<string,bool> presetSetActive;
	private readonly ICallGateSubscriber<bool> presetClearActive;
	private readonly ICallGateSubscriber<string?> presetGetActive;
	private readonly ICallGateSubscriber<List<string>,bool,List<string>> configuration;
	private readonly ICallGateSubscriber<string,object> aiSetPreset;
	private readonly ICallGateSubscriber<string> aiGetPreset;
	private readonly ICallGateSubscriber<bool> aiIsEnabled;
	private readonly ICallGateSubscriber<int,bool> aiEnable;
	private readonly ICallGateSubscriber<bool> aiDisable;
	public bool IsFacingRestricted {
		get {
			try {
				return isFacingRestricted.InvokeFunc();
			}
			catch {
				return Ask(forbiddenDirectionsCount,0)>0;
			}
		}
	}
	public bool ShouldHoldActions {
		get {
			try {
				return shouldHoldActions.InvokeFunc();
			}
			catch {
				return IsFacingRestricted||Ask(forceCancelCastMechanic,fallback: false)||Ask(forceCancelCastMechanicAI,fallback: false);
			}
		}
	}
	public bool InCrucibleEncounter => Ask(inCrucibleEncounter,fallback: false);
	public bool PreferChallenge {
		get {
			if(InCrucibleEncounter) {
				return Ask(preferChallenge,fallback: false);
			}
			return false;
		}
	}
	public bool ShouldClearTarget {
		get {
			if(InCrucibleEncounter) {
				return Ask(shouldClearTarget,fallback: false);
			}
			return false;
		}
	}
	public ulong PreferredTargetID {
		get {
			if(!InCrucibleEncounter) {
				return 0uL;
			}
			return Ask(preferredTargetID,0uL);
		}
	}
	public bool IsInstalled {
		get {
			foreach(IExposedPlugin installedPlugin in pluginInterface.InstalledPlugins) {
				if(installedPlugin.IsLoaded&&installedPlugin.InternalName.StartsWith("BossMod",StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}
	}
	public bool IsDodging => Ask(isMoving,fallback: false);
	public bool IsNavigating => Ask(isNavigating,fallback: false);
	public bool InEncounter => Ask(hasActiveModule,fallback: false);
	public string EncounterName => Ask(activeModuleName,string.Empty) ?? string.Empty;
	public float NextDamageIn => Ask(nextDamageIn,float.MaxValue);
	public float NextTankbusterIn => Ask(nextTankbusterDamageIn,float.MaxValue);
	public float NextRaidwideIn => Ask(nextRaidwideDamageIn,float.MaxValue);
	public float TimelineTankbusterIn => Ask(timelineTankbusterIn,float.MaxValue);
	public float TimelineRaidwideIn => Ask(timelineRaidwideIn,float.MaxValue);
	public int NextHitType => Ask(nextDamageType,0);
	public float InterruptCastRemaining => Ask(interruptCastRemaining,float.MaxValue);
	public bool ShouldDispel => Ask(shouldDispel,fallback: false);
	public ulong DispelTargetID {
		get {
			if(!InCrucibleEncounter) {
				return 0uL;
			}
			return Ask(dispelTargetID,0uL);
		}
	}
	public int DangerZones => Ask(forbiddenZonesCount,0);
	public float DangerIn => Ask(forbiddenZonesNextActivation,float.MaxValue);
	public string? ActivePresetName => Ask(presetGetActive,null);
	public string AiPresetName => Ask(aiGetPreset,string.Empty) ?? string.Empty;
	public bool AiEnabled => Ask(aiIsEnabled,fallback: false);
	public BossModBridge(IDalamudPluginInterface pluginInterface) {
		this.pluginInterface=pluginInterface;
		isFacingRestricted=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.IsFacingRestricted");
		shouldHoldActions=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.ShouldHoldActions");
		preferredTargetID=pluginInterface.GetIpcSubscriber<ulong>("BossMod.Hints.PreferredTargetID");
		shouldClearTarget=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.ShouldClearTarget");
		inCrucibleEncounter=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.InCrucibleEncounter");
		preferChallenge=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.PreferChallenge");
		forceCancelCastMechanic=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.ForceCancelCastMechanic");
		forceCancelCastMechanicAI=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.ForceCancelCastMechanicAI");
		forbiddenDirectionsCount=pluginInterface.GetIpcSubscriber<int>("BossMod.Hints.ForbiddenDirectionsCount");
		isMoving=pluginInterface.GetIpcSubscriber<bool>("BossMod.Movement.IsMoving");
		isNavigating=pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.IsNavigating");
		hasActiveModule=pluginInterface.GetIpcSubscriber<bool>("BossMod.HasActiveModule");
		activeModuleName=pluginInterface.GetIpcSubscriber<string>("BossMod.ActiveModuleName");
		nextDamageIn=pluginInterface.GetIpcSubscriber<float>("BossMod.Hints.NextDamageIn");
		forbiddenZonesCount=pluginInterface.GetIpcSubscriber<int>("BossMod.Hints.ForbiddenZonesCount");
		forbiddenZonesNextActivation=pluginInterface.GetIpcSubscriber<float>("BossMod.Hints.ForbiddenZonesNextActivation");
		nextTankbusterDamageIn=pluginInterface.GetIpcSubscriber<float>("BossMod.Hints.NextTankbusterDamageIn");
		nextRaidwideDamageIn=pluginInterface.GetIpcSubscriber<float>("BossMod.Hints.NextRaidwideDamageIn");
		timelineTankbusterIn=pluginInterface.GetIpcSubscriber<float>("BossMod.Timeline.NextTankbusterIn");
		timelineRaidwideIn=pluginInterface.GetIpcSubscriber<float>("BossMod.Timeline.NextRaidwideIn");
		nextDamageType=pluginInterface.GetIpcSubscriber<int>("BossMod.Hints.NextDamageType");
		interruptCastRemaining=pluginInterface.GetIpcSubscriber<float>("BossMod.Hints.InterruptCastRemaining");
		shouldDispel=pluginInterface.GetIpcSubscriber<bool>("BossMod.Hints.ShouldDispel");
		dispelTargetID=pluginInterface.GetIpcSubscriber<ulong>("BossMod.Hints.DispelTargetID");
		isPositionSafe=pluginInterface.GetIpcSubscriber<Vector3,bool>("BossMod.Hints.IsPositionSafe");
		isDashSafe=pluginInterface.GetIpcSubscriber<Vector3,Vector3,bool>("BossMod.Hints.IsDashSafe");
		presetAddTransientStrategy=pluginInterface.GetIpcSubscriber<string,string,string,string,bool>("BossMod.Presets.AddTransientStrategy");
		presetCreate=pluginInterface.GetIpcSubscriber<string,bool,bool>("BossMod.Presets.Create");
		presetGet=pluginInterface.GetIpcSubscriber<string,string>("BossMod.Presets.Get");
		presetDelete=pluginInterface.GetIpcSubscriber<string,bool>("BossMod.Presets.Delete");
		presetSetActive=pluginInterface.GetIpcSubscriber<string,bool>("BossMod.Presets.SetActive");
		presetClearActive=pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.ClearActive");
		presetGetActive=pluginInterface.GetIpcSubscriber<string>("BossMod.Presets.GetActive");
		configuration=pluginInterface.GetIpcSubscriber<List<string>,bool,List<string>>("BossMod.Configuration");
		aiSetPreset=pluginInterface.GetIpcSubscriber<string,object>("BossMod.AI.SetPreset");
		aiGetPreset=pluginInterface.GetIpcSubscriber<string>("BossMod.AI.GetPreset");
		aiIsEnabled=pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.IsEnabled");
		aiEnable=pluginInterface.GetIpcSubscriber<int,bool>("BossMod.AI.Enable");
		aiDisable=pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.Disable");
	}
	public bool IsSafeAt(Vector3 destination) {
		return Ask(isPositionSafe,destination,fallback: true);
	}
	public bool IsDashSafe(Vector3 from,Vector3 to) {
		return Ask(isDashSafe,from,to,fallback: true);
	}
	public bool CreatePreset(string presetJson,bool overwrite) {
		try {
			return presetCreate.InvokeFunc(presetJson,overwrite);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"BossMod.Presets.Create failed.");
			return false;
		}
	}
	public bool SetTransientStrategy(string presetName,string moduleTypeName,string trackName,string value) {
		try {
			return presetAddTransientStrategy.InvokeFunc(presetName,moduleTypeName,trackName,value);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"BossMod.Presets.AddTransientStrategy failed.");
			return false;
		}
	}
	public string? GetPreset(string name) {
		return Ask(presetGet,name,null);
	}
	public bool DeletePreset(string name) {
		return Ask(presetDelete,name,fallback: false);
	}
	public bool ActivatePreset(string name) {
		try {
			return presetSetActive.InvokeFunc(name);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"BossMod.Presets.SetActive failed.");
			return false;
		}
	}
	public bool ClearActivePreset() {
		return Ask(presetClearActive,fallback: false);
	}
	public bool SetAiPreset(string name) {
		try {
			aiSetPreset.InvokeAction(name);
			return true;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"BossMod.AI.SetPreset failed.");
			return false;
		}
	}
	public string? ReadSetting(string node,params string[] fieldPatterns) {
		foreach(string text in fieldPatterns) {
			try {
				ICallGateSubscriber<List<string>,bool,List<string>> callGateSubscriber=configuration;
				int num=2;
				List<string> list=new List<string>(num);
				CollectionsMarshal.SetCount(list,num);
				Span<string> span=CollectionsMarshal.AsSpan(list);
				span[0]=node;
				span[1]=text;
				List<string> list2=callGateSubscriber.InvokeFunc(list,arg2: false);
				if(list2!=null&&list2.Count==1&&!IsConfigError(list2[0])) {
					return list2[0];
				}
			}
			catch(Exception) {
				return null;
			}
		}
		return null;
	}
	public bool WriteSetting(string node,string field,string value) {
		try {
			ICallGateSubscriber<List<string>,bool,List<string>> callGateSubscriber=configuration;
			int num=3;
			List<string> list=new List<string>(num);
			CollectionsMarshal.SetCount(list,num);
			Span<string> span=CollectionsMarshal.AsSpan(list);
			span[0]=node;
			span[1]=field;
			span[2]=value;
			List<string> list2=callGateSubscriber.InvokeFunc(list,arg2: false);
			return list2!=null&&list2.Count>0&&!IsConfigError(list2[0]);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"BossMod.Configuration write of {Node}.{Field} failed.",node,field);
			return false;
		}
	}
	private static bool IsConfigError(string line) {
		if(!line.StartsWith("Failed",StringComparison.OrdinalIgnoreCase)&&!line.StartsWith("Field not found",StringComparison.OrdinalIgnoreCase)&&!line.StartsWith("Config type",StringComparison.OrdinalIgnoreCase)&&!line.StartsWith("Ambiguous",StringComparison.OrdinalIgnoreCase)) {
			return line.StartsWith("Usage",StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}
	public bool EnableAi(int masterSlot) {
		return Ask(aiEnable,masterSlot,fallback: false);
	}
	public bool DisableAi() {
		return Ask(aiDisable,fallback: false);
	}
	private static T Ask<T>(ICallGateSubscriber<T> gate,T fallback) {
		try {
			return gate.InvokeFunc();
		}
		catch {
			return fallback;
		}
	}
	private static TRet Ask<T1,TRet>(ICallGateSubscriber<T1,TRet> gate,T1 arg,TRet fallback) {
		try {
			return gate.InvokeFunc(arg);
		}
		catch {
			return fallback;
		}
	}
	private static TRet Ask<T1,T2,TRet>(ICallGateSubscriber<T1,T2,TRet> gate,T1 a,T2 b,TRet fallback) {
		try {
			return gate.InvokeFunc(a,b);
		}
		catch {
			return fallback;
		}
	}
}
