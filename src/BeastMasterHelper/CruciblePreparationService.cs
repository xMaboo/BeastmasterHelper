using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
namespace Beastmaster;

public sealed class CruciblePreparationService : IDisposable {
	private readonly BeastmasterConfiguration configuration;
	private readonly CrucibleBattleStart battleStart;
	private bool attempted;
	private bool autoRun;
	private bool awaitingBattle;
	private bool readyToCommence;
	private bool windowsWereOpen;
	private string stableEnemy="";
	private int attempts;
	private DateTime retryAt;
	private int[] desired=Array.Empty<int>();
	private DateTime windowsSeenSinceUtc=DateTime.MinValue;
	private readonly HashSet<int> unclickable=new HashSet<int>();
	private string enemy="";
	private int pendingId;
	private bool pendingSelected;
	private DateTime nextTick;
	private DateTime expires;
	public CrucibleFightState State { get; private set; }=new CrucibleFightState("","",Array.Empty<string>(),Array.Empty<CrucibleFamiliar>(),"Refresh while on a fight pad.");
	public string Status { get; private set; }="Ready to read the displayed enemy and team.";
	public bool Running { get; private set; }
	public bool Automatic {
		get {
			return configuration.CrucibleAutoPrepare;
		}
		set {
			battleStart.Cancel();
			configuration.CrucibleAutoPrepare=value;
			configuration.Save();
			Running=false;
			awaitingBattle=false;
			readyToCommence=false;
			pendingId=0;
			attempted=false;
			stableEnemy="";
		}
	}
	public static DateTime SuppressUntilUtc { get; set; }=DateTime.MinValue;
	public CruciblePreparationService(BeastmasterConfiguration configuration) {
		this.configuration=configuration;
		battleStart=new CrucibleBattleStart(configuration);
		DalamudApi.Framework.Update+=Update;
	}
	public void Refresh() {
		try {
			State=CrucibleFightReader.Read();
		}
		catch(Exception ex) {
			State=new CrucibleFightState("","",Array.Empty<string>(),Array.Empty<CrucibleFamiliar>(),"Read failed: "+ex.Message);
		}
	}
	public void ApplySuggested() {
		Begin(automatic: false);
	}
	private void Begin(bool automatic) {
		attempted=true;
		autoRun=automatic;
		readyToCommence=false;
		awaitingBattle=false;
		attempts++;
		retryAt=DateTime.UtcNow.AddSeconds(6.0);
		unclickable.Clear();
		Refresh();
		DalamudApi.Log.Information("Fight preparation (attempt {N}): valid={Valid} enemy='{Enemy}' weakness='{Weak}' board={Board} suggested={Sug} roster={Count} selected={Sel} note='{Err}'",attempts,State.Valid,State.Enemy,State.Weakness,configuration.CrucibleBoard,configuration.CrucibleSuggestedTeamMode,State.Familiars.Count,Enumerable.Count(State.Familiars,(CrucibleFamiliar f) => f.Slot<3),State.Error);
		CrucibleFamiliar[] array=CrucibleSuggestedTeam.ForFight(configuration,State,out string note);
		if(note.Length>0) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] "+note);
		}
		CrucibleFamiliar[] array2=array;
		if(array2==null) {
			array2=CrucibleSuggestedTeam.MatchGuideKinships(configuration,State,CrucibleTeamPlanner.Suggest(State),out string note2);
			if(note2.Length>0) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] "+note2);
			}
		}
		DalamudApi.Log.Information("Fight preparation: guide team={Guide} chosen={Chosen} note='{Note}'",(array==null)?"none":string.Join(",",array.Select((CrucibleFamiliar f) => f.Name)),string.Join(",",array2.Select((CrucibleFamiliar f) => f.Name)),note);
		if(array2.Length==0) {
			Status=((State.Familiars.Count==0)?"The team window listed no familiars to choose from.":"No living familiar could be selected - every one in the roster is at zero HP.");
			return;
		}
		if(array2.Length<3) {
			DalamudApi.ChatGui.Print($"[Beast Master Helper] Only {array2.Length} familiar(s) are alive, so the team is short. "+"Selecting them anyway.");
		}
		if(State.WeaknessTypes.Length==0) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] "+((State.Enemy.Length==0)?"This enemy":State.Enemy)+" states no weakness. Picking the healthiest familiars in catalogue order instead of a counter team.");
		}
		if(CrucibleTeamPlanner.SuggestionIsHurt(State)) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] Fewer than three familiars are above "+$"{85.0:0}% health - the team includes a hurt one.");
		}
		desired=array2.Select((CrucibleFamiliar f) => f.CatalogueId).ToArray();
		enemy=State.Enemy;
		pendingId=0;
		Running=true;
		nextTick=DateTime.MinValue;
		Status="Applying suggested team; checking each slot change.";
	}
	public void Stop() {
		Automatic=false;
		Status="Automatic preparation disabled; team selection stopped.";
	}
	private void Update(IFramework framework) {
		if(DateTime.UtcNow<nextTick) {
			return;
		}
		nextTick=DateTime.UtcNow.AddMilliseconds(500.0);
		bool windowsOpen=CrucibleFightReader.WindowsOpen;
		if(!configuration.CrucibleDifficultySeen&&CrucibleDifficulty.Available) {
			configuration.CrucibleDifficultySeen=true;
			configuration.Save();
			DalamudApi.Log.Information("Difficulty selector seen on Board Layout: showing {Name}.",CrucibleDifficulty.NameOf(CrucibleDifficulty.Current()));
		}
		if(DateTime.UtcNow<SuppressUntilUtc) {
			windowsWereOpen=windowsOpen;
			return;
		}
		bool num=windowsWereOpen&&!windowsOpen;
		windowsWereOpen=windowsOpen;
		if(num&&DalamudApi.Condition[ConditionFlag.BoundByDuty]&&battleStart.ArmFromObservedStart()) {
			Status=battleStart.Status;
		}
		bool flag=battleStart.Tick();
		if(battleStart.Status.Length>0&&awaitingBattle) {
			Status=battleStart.Status;
		}
		if(flag) {
			return;
		}
		if(!DalamudApi.Condition[ConditionFlag.BoundByDuty]||DalamudApi.Condition[ConditionFlag.InCombat]||!CrucibleFightReader.WindowsOpen) {
			if(awaitingBattle&&battleStart.Status.Length>0) {
				Status=battleStart.Status;
			}
			else if(Running) {
				Status="Preparation stopped: fight context closed.";
			}
			Running=false;
			awaitingBattle=false;
			readyToCommence=false;
			pendingId=0;
			attempted=false;
			stableEnemy="";
			attempts=0;
			windowsSeenSinceUtc=DateTime.MinValue;
			unclickable.Clear();
			return;
		}
		if(awaitingBattle) {
			if(DateTime.UtcNow>expires) {
				awaitingBattle=false;
				Running=false;
				Status="Commence was sent but the fight windows remain open. Stopped; capture the event trace.";
			}
			return;
		}
		if(!Running) {
			if(!Automatic) {
				return;
			}
			if(attempted) {
				if(attempts>=3||DateTime.UtcNow<retryAt) {
					return;
				}
				Refresh();
				if(!State.Valid||State.Familiars.Any((CrucibleFamiliar f) => f.Slot<3)) {
					return;
				}
				DalamudApi.Log.Information("Fight preparation: nothing was selected after attempt {N}; trying again.",attempts);
				attempted=false;
				stableEnemy="";
			}
			Refresh();
			if(!State.Valid) {
				stableEnemy="";
				Status=State.Error;
				return;
			}
			if(windowsSeenSinceUtc==DateTime.MinValue) {
				windowsSeenSinceUtc=DateTime.UtcNow;
			}
			if(State.Enemy.Length==0&&(DateTime.UtcNow-windowsSeenSinceUtc).TotalSeconds<2.5) {
				Status="Waiting for the fight details to appear.";
			}
			else if(stableEnemy!=State.Enemy+State.Weakness) {
				stableEnemy=State.Enemy+State.Weakness;
				Status="Waiting for fight details to settle.";
			}
			else {
				attempted=true;
				Begin(automatic: true);
			}
			return;
		}
		Refresh();
		if(!State.Valid||State.Enemy!=enemy||!DalamudApi.Condition[ConditionFlag.BoundByDuty]||DalamudApi.Condition[ConditionFlag.InCombat]) {
			Running=false;
			Status="Stopped: fight window, duty, or enemy changed.";
			return;
		}
		if(readyToCommence) {
			if(!(from f in State.Familiars
				where f.Slot<3&&f.HP!=0
				select f.CatalogueId).Order().SequenceEqual(desired.Order())) {
				Running=false;
				readyToCommence=false;
				Status="Team changed before commencement; stopped.";
			}
			else if(battleStart.Request()) {
				readyToCommence=false;
				awaitingBattle=true;
				expires=DateTime.UtcNow.AddSeconds(8.0);
				Status="Commence Battle sent; waiting for the fight to start.";
			}
			else if(DateTime.UtcNow>expires) {
				Running=false;
				readyToCommence=false;
				Status="Commence Battle did not become ready; stopped.";
			}
			return;
		}
		if(pendingId!=0) {
			CrucibleFamiliar crucibleFamiliar=State.Familiars.FirstOrDefault((CrucibleFamiliar f) => f.CatalogueId==pendingId);
			if(!(crucibleFamiliar!=null)||crucibleFamiliar.Slot<3!=pendingSelected) {
				if(DateTime.UtcNow>expires) {
					Running=false;
					Status="Selection was not confirmed. Stopped without repeating the toggle; capture the UI event trace.";
				}
				return;
			}
			pendingId=0;
		}
		CrucibleFamiliar crucibleFamiliar2=State.Familiars.FirstOrDefault((CrucibleFamiliar f) => f.Slot<3&&!desired.Contains(f.CatalogueId));
		CrucibleFamiliar crucibleFamiliar3=desired.Select((int id) => State.Familiars.FirstOrDefault((CrucibleFamiliar f) => f.CatalogueId==id)).FirstOrDefault((CrucibleFamiliar f) => f!=null&&f.Slot==3);
		CrucibleFamiliar crucibleFamiliar4=crucibleFamiliar2 ?? crucibleFamiliar3;
		if(crucibleFamiliar4==null) {
			bool flag2=Enumerable.Count(State.Familiars,(CrucibleFamiliar f) => f.Slot<3&&f.HP!=0&&desired.Contains(f.CatalogueId))==desired.Length;
			readyToCommence=flag2&&autoRun;
			Running=readyToCommence;
			expires=DateTime.UtcNow.AddSeconds(5.0);
			CruciblePreparationService cruciblePreparationService=this;
			string status;
			if(readyToCommence) {
				status=$"{desired.Length} suggested familiar(s) confirmed; commencing battle.";
			}
			else {
				status=(flag2?$"{desired.Length} suggested familiar(s) confirmed. Ready to commence.":"The available familiar list changed; refresh the suggestion.");
			}
			cruciblePreparationService.Status=status;
		}
		else if(crucibleFamiliar4.HP==0&&crucibleFamiliar2==null) {
			Running=false;
			Status="A suggested familiar is no longer healthy.";
		}
		else if(!CrucibleFightReader.ClickFamiliar(crucibleFamiliar4.CatalogueId)) {
			if(crucibleFamiliar2==null&&unclickable.Add(crucibleFamiliar4.CatalogueId)&&unclickable.Count<=3) {
				CrucibleFamiliar[] array=CrucibleTeamPlanner.Suggest(State with {
					Familiars = State.Familiars.Where((CrucibleFamiliar f) => !unclickable.Contains(f.CatalogueId)).ToList()
				});
				if(array.Length!=0) {
					desired=array.Select((CrucibleFamiliar f) => f.CatalogueId).ToArray();
					Status=crucibleFamiliar4.Name+" could not be selected; picking the next best familiar instead.";
					return;
				}
			}
			Running=false;
			Status="Could not resolve the live familiar control; capture its window state.";
		}
		else {
			pendingId=crucibleFamiliar4.CatalogueId;
			pendingSelected=crucibleFamiliar2==null;
			expires=DateTime.UtcNow.AddSeconds(5.0);
			Status=$"Waiting for {crucibleFamiliar4.Name} to be {(pendingSelected?"selected":"removed")}.";
		}
	}
	public void Commence() {
		Refresh();
		int num=Enumerable.Count(State.Familiars,(CrucibleFamiliar f) => f.Slot<3&&f.HP!=0);
		if(Running||!State.Valid||num==0) {
			Status="Select at least one living familiar before commencing.";
		}
		else if(battleStart.Request()) {
			awaitingBattle=true;
			attempted=true;
			expires=DateTime.UtcNow.AddSeconds(90.0);
			Status=battleStart.Status;
		}
		else {
			Status="Commence Battle is not ready.";
		}
	}
	public void Dispose() {
		DalamudApi.Framework.Update-=Update;
	}
}
