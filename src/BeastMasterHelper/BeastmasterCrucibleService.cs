using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
namespace Beastmaster;

public sealed class BeastmasterCrucibleService : IDisposable {
	private enum Step {
		Idle,
		Travel,
		Interact,
		Menu,
		ChallengeMenu,
		Board,
		Difficulty,
		Team,
		VerifyTeam,
		ApplyPreset,
		Challenge,
		Commence,
		Entering,
		LevelClear,
		LevelOpen,
		LevelScan,
		LevelClose,
		LevelFill,
		Prepare
	}
	private readonly BeastmasterNavigationService navigation;
	private readonly BeastmasterConfiguration configuration;
	private Step step;
	private DateTime deadline;
	private DateTime nextAction;
	private DateTime nextBoardSelection;
	private int board;
	private bool recommended;
	private bool ownsNavigation;
	public static readonly string[] BoardNames=new string[5] { "First Board of the Unbroken", "Second Board of the Unbroken", "Third Board of the Unbroken", "First Master's Board", "Second Master's Board" };
	private int prepStage;
	private int prepAttempts;
	private DateTime prepAt;
	private int levelAttempts;
	private int levelPending;
	private bool levelSuggested;
	private int difficultyClicks;
	private int difficultyRechecks;
	private int? difficultyFrom;
	private bool difficultyLastRight;
	private DateTime difficultySince;
	private DateTime difficultyClickedAt;
	private bool levelPendingSelected;
	private DateTime levelPendingUntil;
	private DateTime levelActionAt;
	private bool levelOpenedNotebook;
	private List<int> scanIds=new List<int>();
	private int scanIndex;
	private bool scanReading;
	private int scanTries;
	private int scanOk;
	private int scanLocked;
	private int scanMisses;
	private int scanStage;
	private int scanLastHover;
	private int? scanSeen;
	private int fillHovered;
	private int scanPagesDone;
	private int scanUnregId;
	private DateTime scanHoverAt;
	private DateTime scanSeenAt;
	private readonly Dictionary<int,int> familiarPage=new Dictionary<int,int>();
	private DateTime scanUnregUntil;
	private int scanPageTry;
	private bool scanPageClicked;
	private DateTime scanPageClickedAt;
	private int levelMenuTries;
	private bool levelRecheck;
	private int levelPendingCount;
	private bool levelRecheckDone;
	private readonly Dictionary<int,bool> levelTeamOverride=new Dictionary<int,bool>();
	private int fillPage;
	private int pageWaitTarget;
	private DateTime pageWaitUntil;
	private bool suggestedCleared;
	private int suggestedPagesClear;
	public CruciblePreparationService Preparation { get; }
	public CrucibleDiagnostics Diagnostics { get; }=new CrucibleDiagnostics();
	public CrucibleClickRecorder Recorder { get; }=CrucibleClickRecorder.Shared;
	private static Vector3 LaudaPosition => CrucibleEntryRules.LaudaPosition;
	public bool IsRunning => step!=Step.Idle;
	public string Status { get; private set; }="Ready. Choose a board and team option.";
	private string BoardName => BoardNames[board];
	private TimeSpan Buffer => TimeSpan.FromMilliseconds(Math.Clamp(configuration.CrucibleGridBufferMs,5,500));
	public BeastmasterCrucibleService(BeastmasterNavigationService navigation,BeastmasterConfiguration configuration) {
		this.navigation=navigation;
		this.configuration=configuration;
		Preparation=new CruciblePreparationService(configuration);
		Recorder.Restore(configuration.CrucibleRecordedClick);
		DalamudApi.Framework.Update+=Update;
	}
	public void Start(int selectedBoard,bool useRecommended) {
		if(IsRunning) {
			return;
		}
		if(selectedBoard<0||selectedBoard>=BoardNames.Length||DalamudApi.ObjectTable.LocalPlayer==null||DalamudApi.PlayerState.ClassJob.RowId!=43) {
			Status="Switch to Beastmaster before starting.";
			return;
		}
		if(DalamudApi.Condition[ConditionFlag.BoundByDuty]||DalamudApi.Condition[ConditionFlag.InCombat]) {
			Status="Leave the current duty or combat before starting.";
			return;
		}
		nextBoardSelection=DateTime.MinValue;
		levelSuggested=false;
		board=selectedBoard;
		recommended=useRecommended;
		if(configuration.CrucibleRepairEnabled||configuration.CrucibleFoodItemId!=0) {
			prepStage=0;
			prepAttempts=0;
			prepAt=DateTime.MinValue;
			Advance(Step.Prepare,"Checking gear and food before entering.",120);
		}
		else {
			BeginTravel();
		}
	}
	private bool BeginTravel() {
		if(CrucibleEntryRules.AtLauda()) {
			Advance(Step.Interact,"Already at Lauda; opening the Crucible menu.");
			return true;
		}
		TerritoryType row=DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRow(148u);
		if(!navigation.NavigateQuestTarget(new BeastmasterQuestLocation(148u,row.Map.RowId,LaudaPosition,"Central Shroud","Lauda",GroundOnly: true))) {
			Stop("Navigation could not start. Check vnavmesh and teleport availability.");
			return false;
		}
		ownsNavigation=true;
		Advance(Step.Travel,"Travelling to Lauda in Bentbranch Meadows.",180);
		return true;
	}
	private void PrepareTick() {
		if(DateTime.UtcNow<prepAt) {
			return;
		}
		if(prepStage==0) {
			int num=(configuration.CrucibleRepairEnabled?CrucibleChores.LowestConditionPercent():(-1));
			if(num<0||num>=configuration.CrucibleRepairThreshold) {
				if(CrucibleChores.RepairWindowOpen) {
					CrucibleChores.CloseRepair();
				}
				prepStage=1;
				prepAttempts=0;
				return;
			}
			if(++prepAttempts>14) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] Could not repair gear ("+CrucibleChores.LastResult+"); continuing.");
				if(CrucibleChores.RepairWindowOpen) {
					CrucibleChores.CloseRepair();
				}
				prepStage=1;
				prepAttempts=0;
				return;
			}
			Status=$"Repairing gear ({num}%).";
			if(CrucibleUi.YesNoOpen) {
				CrucibleUi.ConfirmYes();
			}
			else if(CrucibleChores.RepairWindowOpen) {
				CrucibleChores.RepairAll();
			}
			else {
				CrucibleChores.OpenRepair();
			}
			prepAt=DateTime.UtcNow.AddMilliseconds(900.0);
		}
		else if(prepStage==1) {
			uint crucibleFoodItemId=configuration.CrucibleFoodItemId;
			if(crucibleFoodItemId==0||CrucibleChores.FoodMinutesLeft()>=(float)configuration.CrucibleFoodMinMinutes) {
				prepStage=2;
			}
			else if(!CrucibleChores.HasFood(crucibleFoodItemId)) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] The chosen food is not in your bag; continuing without renewing it.");
				prepStage=2;
			}
			else if(++prepAttempts>3) {
				DalamudApi.ChatGui.Print("[Beast Master Helper] Could not eat the food ("+CrucibleChores.LastResult+"); continuing.");
				prepStage=2;
			}
			else {
				Status="Renewing food.";
				CrucibleChores.UseFood(crucibleFoodItemId);
				prepAt=DateTime.UtcNow.AddSeconds(3.0);
			}
		}
		else {
			BeginTravel();
		}
	}
	public void Stop(string message="Stopped.") {
		if(levelOpenedNotebook&&CrucibleBestiaryReader.IsOpen) {
			CrucibleBestiaryReader.Close();
		}
		levelOpenedNotebook=false;
		if(ownsNavigation) {
			navigation.Stop();
		}
		ownsNavigation=false;
		step=Step.Idle;
		Status=message;
	}
	private void Advance(Step next,string message,int seconds=45) {
		step=next;
		Status=message;
		deadline=DateTime.UtcNow.AddSeconds(seconds);
		nextAction=DateTime.UtcNow.AddMilliseconds(PaceFor(next));
	}
	private static int PaceFor(Step next) {
		switch(next) {
		case Step.Board:
		case Step.Difficulty:
		case Step.Team:
		case Step.ApplyPreset:
		return 250;
		case Step.Prepare:
		return 700;
		case Step.LevelScan:
		return 0;
		case Step.LevelFill:
		return 0;
		case Step.VerifyTeam:
		case Step.LevelClear:
		case Step.LevelOpen:
		case Step.LevelClose:
		return 500;
		default:
		return 1000;
		}
	}
	private unsafe void Update(IFramework framework) {
		if(!IsRunning) {
			return;
		}
		try {
			if(DateTime.UtcNow>deadline) {
				Stop("Timed out: "+Status+" Use Inspect windows to identify the missing control.");
			}
			else if(DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51]||DalamudApi.ObjectTable.LocalPlayer==null||DalamudApi.PlayerState.ClassJob.RowId==0) {
				Step step=this.step;
				if((step!=Step.Travel&&step!=Step.Entering)||1==0) {
					Stop("Stopped: character unavailable.");
				}
			}
			else if(!DalamudApi.ClientState.IsLoggedIn) {
				Stop("Stopped: logged out.");
			}
			else if(this.step==Step.Entering&&DalamudApi.Condition[ConditionFlag.BoundByDuty]) {
				Stop("Entered the Crucible. Entry complete; encounter movement is handled by BossmodReborn.");
			}
			else if(DalamudApi.PlayerState.ClassJob.RowId!=43) {
				Stop("Stopped: job changed.");
			}
			else if(this.step!=Step.Entering&&DalamudApi.Condition[ConditionFlag.BoundByDuty]) {
				Stop("Stopped: another duty was entered.");
			}
			else if(DalamudApi.Condition[ConditionFlag.InCombat]) {
				Stop("Stopped: combat started.");
			}
			else {
				if(DateTime.UtcNow<nextAction) {
					return;
				}
				nextAction=DateTime.UtcNow.AddMilliseconds(PaceFor(this.step));
				switch(this.step) {
				case Step.Prepare:
				PrepareTick();
				break;
				case Step.Travel:
				if(DalamudApi.ClientState.TerritoryType==148) {
					IGameObject gameObject=FindLauda();
					if(gameObject!=null&&CrucibleEntryRules.InInteractionRange(DalamudApi.ObjectTable.LocalPlayer.Position,gameObject.Position)) {
						navigation.Stop();
						ownsNavigation=false;
						Advance(Step.Interact,"Arrived at Lauda; opening the Crucible menu.");
					}
					else {
						float value=Vector3.Distance(DalamudApi.ObjectTable.LocalPlayer.Position,gameObject?.Position ?? LaudaPosition);
						Status=$"Approaching Lauda ({value:0.0} yalms).";
					}
				}
				break;
				case Step.Interact:
				if(CrucibleUi.HasMenu("Crucible of the Unbroken")) {
					Advance(Step.Menu,"Lauda's menu is open; selecting Crucible of the Unbroken.");
				}
				else if(CrucibleUi.HasMenu("Challenge the Crucible of the Unbroken.")) {
					Advance(Step.ChallengeMenu,"Waiting for Challenge the Crucible of the Unbroken.");
				}
				else {
					if(DalamudApi.Condition[ConditionFlag.Mounting]) {
						break;
					}
					if(DalamudApi.Condition[ConditionFlag.Mounted]) {
						Status="Dismounting before speaking to Lauda.";
						ActionManager* ptr=ActionManager.Instance();
						if(ptr!=null) {
							ptr->UseAction(ActionType.GeneralAction,23u,3758096384uL,0u,ActionManager.UseActionMode.None,0u,null);
						}
						break;
					}
					IGameObject gameObject2=FindLauda();
					if(gameObject2==null) {
						Status="Waiting for Lauda to become targetable.";
						break;
					}
					if(!CrucibleEntryRules.InInteractionRange(DalamudApi.ObjectTable.LocalPlayer.Position,gameObject2.Position)) {
						Status="Lauda is out of interaction range. Move closer or restart entry.";
						break;
					}
					DalamudApi.TargetManager.Target=gameObject2;
					TargetSystem* ptr2=TargetSystem.Instance();
					if(ptr2!=null) {
						ulong num3=ptr2->InteractWithObject((GameObject*)gameObject2.Address,checkLineOfSight: false);
						Status=((num3!=0L)?"Interaction requested; waiting for Lauda's menu.":"Interaction not accepted; retrying Lauda.");
						nextAction=DateTime.UtcNow.AddSeconds(2.0);
					}
				}
				break;
				case Step.Menu:
				if(CrucibleUi.SelectMenu("Crucible of the Unbroken")) {
					Advance(Step.ChallengeMenu,"Waiting for Challenge the Crucible of the Unbroken.");
				}
				break;
				case Step.ChallengeMenu:
				if(CrucibleUi.SelectMenu("Challenge the Crucible of the Unbroken")) {
					Advance(Step.Board,"Selecting "+BoardName+".");
				}
				break;
				case Step.Board:
				if(CrucibleUi.Has("Challenge This Board")) {
					difficultyClicks=0;
					difficultyFrom=null;
					difficultySince=DateTime.UtcNow;
					difficultyRechecks=0;
					Advance(Step.Difficulty,"Board Layout is open; checking the difficulty.",40);
				}
				else if(!(DateTime.UtcNow<nextBoardSelection)) {
					if(CrucibleUi.SelectBoard(BoardName)) {
						Status="Requested "+BoardName+"; waiting for Board Layout to open.";
						nextBoardSelection=DateTime.UtcNow.AddSeconds(3.0);
					}
					else {
						Status="Waiting for a usable "+BoardName+" button. Inspect windows if this persists.";
					}
				}
				break;
				case Step.Difficulty:
				DifficultyTick();
				break;
				case Step.Team:
				if(CrucibleUi.Has("Challenge This Board")) {
					if(configuration.CrucibleSuggestedTeamMode&&CrucibleSuggestedTeam.EntryPool(configuration).Count>0) {
						levelSuggested=true;
						levelAttempts=0;
						levelPending=0;
						Advance(Step.LevelClear,"Suggested Team Mode: setting the guide's entry team.",90);
					}
					else if(configuration.CrucibleLevelUpBeasts&&LevelTeamStillGood()) {
						configuration.CrucibleLevelRunsSinceScan++;
						configuration.Save();
						Advance(Step.Challenge,$"Level Up Beasts: keeping the same team (run {configuration.CrucibleLevelRunsSinceScan} of {configuration.CrucibleLevelRescanEvery} since the last full read).");
					}
					else if(configuration.CrucibleLevelUpBeasts) {
						levelAttempts=0;
						levelPending=0;
						Advance(Step.LevelClear,"Level Up Beasts: clearing the loaded team.",90);
					}
					else if(!recommended) {
						Advance(Step.VerifyTeam,"Checking the loaded team.",30);
					}
					else if(CrucibleContents.PresetPreviewWaiting) {
						Advance(Step.ApplyPreset,"Recommended team previewed; applying it.");
					}
					else if(CrucibleUi.Click("Recommended Team")) {
						Advance(Step.ApplyPreset,"Recommended team requested; waiting for the preview to apply it.");
					}
				}
				break;
				case Step.VerifyTeam: {
					List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
					if(list.Count==0) {
						Status="Waiting for the team list to load.";
						break;
					}
					int num2=Enumerable.Count(list,(CrucibleFamiliar f) => f.Slot<3);
					if(num2==0) {
						Stop("Team Composition has no familiars selected. Build a team once, then start again - the loop reuses whatever is loaded.");
						break;
					}
					Advance(Step.Challenge,$"{num2} familiar(s) loaded; waiting for Challenge This Board.");
					break;
				}
				case Step.LevelClear:
				LevelClear();
				break;
				case Step.LevelOpen:
				LevelOpen();
				break;
				case Step.LevelScan:
				LevelScan();
				break;
				case Step.LevelClose:
				LevelClose();
				break;
				case Step.LevelFill:
				LevelFill();
				break;
				case Step.ApplyPreset:
				if(CrucibleContents.PresetPreviewWaiting) {
					Status=(CrucibleContents.ApplyPresetTeam()?"Applied the recommended team; waiting for the preview to close.":"Waiting for Apply in the recommended-team preview.");
				}
				else {
					Advance(Step.Challenge,"Recommended team applied; waiting for Challenge This Board.");
				}
				break;
				case Step.Challenge: {
					int? num=CrucibleDifficulty.Current();
					if(num.HasValue) {
						int valueOrDefault=num.GetValueOrDefault();
						if(valueOrDefault!=Math.Clamp(configuration.CrucibleDifficulty,0,3)&&difficultyRechecks++<2) {
							difficultyClicks=0;
							difficultyFrom=null;
							difficultySince=DateTime.UtcNow;
							Advance(Step.Difficulty,"Difficulty reads "+CrucibleDifficulty.NameOf(valueOrDefault)+"; setting it again.",40);
							break;
						}
					}
					if(CrucibleUi.Click("Challenge This Board")) {
						Advance(Step.Commence,"Waiting for duty confirmation.");
					}
					break;
				}
				case Step.Commence:
				if(CrucibleUi.Commence(BoardName)) {
					Advance(Step.Entering,"Waiting to enter the Crucible.",120);
				}
				break;
				case Step.Entering:
				break;
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Error(exception,"Crucible entry stopped");
			Stop("Stopped after a UI error. See the plugin log.");
		}
	}
	private void DifficultyTick() {
		int? difficulty=CrucibleDifficulty.Current();
		if(!difficulty.HasValue) {
			if((DateTime.UtcNow-difficultySince).TotalSeconds>2.5) {
				Advance(Step.Team,"No difficulty selector on Board Layout; waiting for Team Composition.");
			}
			else {
				Status="Looking for the difficulty selector.";
			}
			return;
		}
		if(!configuration.CrucibleDifficultySeen) {
			configuration.CrucibleDifficultySeen=true;
			configuration.Save();
		}
		int? num=difficultyFrom;
		if(num.HasValue) {
			int valueOrDefault=num.GetValueOrDefault();
			if(difficulty.Value==valueOrDefault) {
				if((DateTime.UtcNow-difficultyClickedAt).TotalSeconds<1.5) {
					Status="Waiting for the difficulty to change from "+CrucibleDifficulty.NameOf(valueOrDefault)+".";
					return;
				}
				DalamudApi.Log.Warning("Difficulty: pressed {Arrow} but it still reads {Name}.",difficultyLastRight?"right":"left",CrucibleDifficulty.NameOf(valueOrDefault));
				difficultyFrom=null;
				if(difficultyClicks>=8) {
					Stop("Could not change the difficulty: it stayed on "+CrucibleDifficulty.NameOf(valueOrDefault)+".");
					return;
				}
			}
			else {
				int num2=Math.Sign(difficulty.Value-valueOrDefault);
				int num3=((difficultyLastRight==configuration.CrucibleDifficultyRightIsHarder)?1:(-1));
				DalamudApi.Log.Information("Difficulty: pressed {Arrow}; it read {From}, now reads {Now}.",difficultyLastRight?"right":"left",CrucibleDifficulty.NameOf(valueOrDefault),CrucibleDifficulty.NameOf(difficulty));
				if(num2!=num3) {
					configuration.CrucibleDifficultyRightIsHarder=!configuration.CrucibleDifficultyRightIsHarder;
					configuration.Save();
					DalamudApi.Log.Information("Difficulty: the right arrow {Way} the difficulty; remembering that.",configuration.CrucibleDifficultyRightIsHarder?"raises":"lowers");
				}
				difficultyFrom=null;
			}
		}
		int num4=Math.Clamp(configuration.CrucibleDifficulty,0,3);
		if(difficulty.Value==num4) {
			Advance(Step.Team,"Difficulty is "+CrucibleDifficulty.NameOf(difficulty)+"; waiting for Team Composition.");
			return;
		}
		if(difficultyClicks>=8) {
			Stop($"Could not reach {CrucibleDifficulty.NameOf(num4)}: stuck on {CrucibleDifficulty.NameOf(difficulty)}.");
			return;
		}
		bool flag=num4>difficulty.Value==configuration.CrucibleDifficultyRightIsHarder;
		if(!CrucibleDifficulty.Arrow(flag)) {
			Status="Waiting for the difficulty arrow.";
			return;
		}
		difficultyClicks++;
		difficultyFrom=difficulty.Value;
		difficultyLastRight=flag;
		difficultyClickedAt=DateTime.UtcNow;
		Status=$"Difficulty: {CrucibleDifficulty.NameOf(difficulty)} -> {CrucibleDifficulty.NameOf(num4)} ({(flag?"right":"left")} arrow).";
	}
	private static int PageOf(IEnumerable<int> ids) {
		if(ids.Max()>25) {
			return 2;
		}
		return 1;
	}
	private bool LevelToggleSettled(IReadOnlyList<CrucibleFamiliar> roster) {
		if(levelPending==0) {
			return true;
		}
		if(roster.Any((CrucibleFamiliar x) => x.CatalogueId==levelPending)==levelPendingSelected||roster.Count!=levelPendingCount||CrucibleBestiaryReader.IsRegistered(levelPending)==levelPendingSelected) {
			levelTeamOverride[levelPending]=levelPendingSelected;
			levelPending=0;
			levelAttempts=0;
			return true;
		}
		if(DateTime.UtcNow>levelPendingUntil) {
			DalamudApi.Log.Information("Level Up Beasts: team list still stale after a click on No.{Id}; closing the Bestiary to refresh it.",levelPending);
			levelPending=0;
			levelRecheck=true;
			levelAttempts=0;
			Advance(Step.LevelClose,"Closing the Master's Bestiary to refresh the team list.",60);
			return false;
		}
		Status="Waiting for the team window to show the change.";
		return false;
	}
	private bool LevelTeamStillGood() {
		if(configuration.CrucibleLevelRunsSinceScan<1||configuration.CrucibleLevelRunsSinceScan>=configuration.CrucibleLevelRescanEvery) {
			return false;
		}
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		if(list.Count>0) {
			return list.All((CrucibleFamiliar f) => configuration.CrucibleBeastRankLearned.ContainsKey(CrucibleBeastRank.MonsterIdFor(f.CatalogueId))&&configuration.CrucibleBeastExpLearned.ContainsKey(CrucibleBeastRank.MonsterIdFor(f.CatalogueId)));
		}
		return false;
	}
	private bool IsUnlockedFamiliar(int catalogueId) {
		if(configuration.CrucibleBeastRankLearned.Count==0) {
			return true;
		}
		uint key=CrucibleBeastRank.MonsterIdFor(catalogueId);
		if(configuration.CrucibleBeastRankLearned.ContainsKey(key)) {
			if(configuration.CrucibleBeastExpLearned.Count!=0) {
				return configuration.CrucibleBeastExpLearned.ContainsKey(key);
			}
			return true;
		}
		return false;
	}
	private HashSet<int> LevelRegistered(IReadOnlyList<CrucibleFamiliar> roster) {
		HashSet<int> hashSet=roster.Select((CrucibleFamiliar f) => f.CatalogueId).ToHashSet();
		foreach(var (item,flag2) in levelTeamOverride) {
			if(flag2) {
				hashSet.Add(item);
			}
			else {
				hashSet.Remove(item);
			}
		}
		return hashSet;
	}
	private static string FamiliarName(int id) {
		return new CrucibleBestiaryEntry(id,Selected: false,null).Name;
	}
	private void LevelClear() {
		if(CrucibleContextMenu.IsOpen) {
			CrucibleContextMenu.Close();
		}
		levelAttempts=0;
		levelMenuTries=0;
		levelRecheck=false;
		levelRecheckDone=false;
		levelTeamOverride.Clear();
		Advance(Step.LevelOpen,"Opening the Master's Bestiary.",90);
	}
	private void LevelOpen() {
		if(CrucibleBestiaryReader.IsOpen) {
			levelOpenedNotebook=true;
			if(levelSuggested) {
				levelAttempts=0;
				levelPending=0;
				levelActionAt=DateTime.MinValue;
				fillPage=0;
				suggestedCleared=false;
				suggestedPagesClear=0;
				pageWaitTarget=0;
				Advance(Step.LevelFill,"Clearing the team before registering the guide's entry team.",120);
				return;
			}
			if(levelRecheckDone) {
				levelAttempts=0;
				levelPending=0;
				levelActionAt=DateTime.MinValue;
				fillPage=0;
				Advance(Step.LevelFill,"Re-checking the team after the refresh.",120);
				return;
			}
			scanIds=new List<int>();
			scanIndex=0;
			scanReading=false;
			scanTries=0;
			scanOk=0;
			scanLocked=0;
			scanMisses=0;
			scanUnregId=0;
			scanStage=0;
			scanPagesDone=0;
			scanPageTry=0;
			scanPageClicked=false;
			scanLastHover=0;
			scanSeen=null;
			scanHoverAt=DateTime.UtcNow;
			Advance(Step.LevelScan,"Reading every familiar's Beast Rank.",180);
		}
		else if(DateTime.UtcNow<levelActionAt) {
			Status="Waiting for the Master's Bestiary to open.";
		}
		else if(levelAttempts>=3) {
			Stop("Level Up Beasts: the Master's Bestiary did not open.");
		}
		else if(CrucibleBestiaryReader.OpenFromTeamComposition()) {
			levelAttempts++;
			levelActionAt=DateTime.UtcNow.AddSeconds(4.0);
			Status="Opening the Master's Bestiary.";
		}
		else {
			Status="Waiting for the Master's Bestiary button.";
		}
	}
	private void LevelScan() {
		if(!CrucibleBestiaryReader.IsOpen) {
			FinishScan("the Master's Bestiary was closed before the scan finished");
			return;
		}
		if(scanOk==0&&scanLocked==0&&scanMisses>=3) {
			FinishScan("hovering a familiar did not update the detail pane");
			return;
		}
		if(scanUnregId!=0) {
			if(CrucibleBestiaryReader.IsRegistered(scanUnregId)==false||!CruciblePetParty.Familiars().Any((CrucibleFamiliar f) => f.CatalogueId==scanUnregId)) {
				levelTeamOverride[scanUnregId]=false;
				scanUnregId=0;
			}
			else {
				if(!(DateTime.UtcNow>scanUnregUntil)) {
					Status="Waiting for "+FamiliarName(scanUnregId)+" to unregister.";
					return;
				}
				DalamudApi.Log.Warning("Level Up Beasts: No.{Id} did not show as unregistered after the click; leaving it to the fill step.",scanUnregId);
				scanUnregId=0;
			}
		}
		switch(scanStage) {
		case 0: {
			List<CrucibleBestiaryReader.GridCell> list=CrucibleBestiaryReader.Cells();
			if(list.Count==0) {
				if(DateTime.UtcNow>scanHoverAt.AddSeconds(4.0)) {
					FinishScan("the grid never showed any familiars");
				}
				else {
					Status="Waiting for the familiar grid.";
				}
				return;
			}
			scanIds=list.Select((CrucibleBestiaryReader.GridCell c) => c.CatalogueId).ToList();
			foreach(int scanId in scanIds) {
				familiarPage[scanId]=PageOf(scanIds);
			}
			scanIndex=0;
			scanReading=false;
			scanTries=0;
			scanStage=1;
			return;
		}
		case 1: {
			if(scanIndex>=scanIds.Count) {
				scanPagesDone++;
				scanStage=2;
				scanPageClicked=false;
				return;
			}
			int num=scanIds[scanIndex];
			Status=$"Reading ranks: page {scanPagesDone+1}, familiar {scanIndex+1} of {scanIds.Count}.";
			if(!scanReading) {
				if(scanLastHover!=0) {
					CrucibleBestiaryReader.Hover(scanLastHover,leave: true);
				}
				if(!CrucibleBestiaryReader.Hover(num)) {
					scanMisses++;
					scanIndex++;
					scanLastHover=0;
				}
				else {
					scanLastHover=num;
					scanReading=true;
					scanTries=0;
					scanSeen=null;
					scanHoverAt=DateTime.UtcNow;
				}
				return;
			}
			uint? num2=CrucibleBestiaryReader.ViewedMonsterId();
			int? num3=CrucibleBestiaryReader.ViewedRank();
			string text=CrucibleBestiaryReader.ViewedName();
			int? num4=CrucibleBestiaryReader.ViewedExpPercent();
			string name=new CrucibleBestiaryEntry(num,Selected: false,null).Name;
			bool flag=name.Length==0||string.Equals(text,name,StringComparison.OrdinalIgnoreCase);
			if(num3.HasValue) {
				int valueOrDefault=num3.GetValueOrDefault();
				if(num4.HasValue) {
					int valueOrDefault2=num4.GetValueOrDefault();
					int num5=valueOrDefault*1000+valueOrDefault2;
					if(scanSeen!=num5) {
						scanSeen=num5;
						scanSeenAt=DateTime.UtcNow;
					}
					else if(!(DateTime.UtcNow<scanSeenAt.AddMilliseconds(50.0))) {
						CrucibleBeastRank.Learn(configuration.CrucibleBeastRankLearned,num2.Value,valueOrDefault);
						configuration.CrucibleBeastExpLearned[num2.Value]=valueOrDefault2;
						(long,long)? tuple=CrucibleBestiaryReader.ViewedExpRaw();
						if(tuple.HasValue) {
							(long,long) valueOrDefault3=tuple.GetValueOrDefault();
							configuration.CrucibleBeastExpRaw[num2.Value]=$"{valueOrDefault3.Item1}/{valueOrDefault3.Item2}";
						}
						scanOk++;
						scanIndex++;
						scanReading=false;
						if(CrucibleBestiaryReader.IsRegistered(num)==true&&CrucibleBestiaryReader.Toggle(num)) {
							scanUnregId=num;
							scanUnregUntil=DateTime.UtcNow.AddSeconds(5.0);
							Status="Read "+FamiliarName(num)+"; unregistering it.";
						}
					}
					return;
				}
			}
			if(!num3.HasValue||!num4.HasValue) {
				if(scanSeen!=-1) {
					scanSeen=-1;
					scanSeenAt=DateTime.UtcNow;
				}
				else if(DateTime.UtcNow>=scanSeenAt.AddMilliseconds(200.0)) {
					scanLocked++;
					scanIndex++;
					scanReading=false;
					scanSeen=null;
				}
				return;
			}
			scanSeen=null;
			if(DateTime.UtcNow>scanHoverAt.AddSeconds(2.0)) {
				DalamudApi.Log.Warning("Bestiary hover for No.{Id} never showed it: pane id {Viewed}, name '{Name}', rank {Rank}.",num,num2,text,num3);
				scanMisses++;
				scanIndex++;
				scanReading=false;
			}
			return;
		}
		}
		if(scanPagesDone>=2||scanPageTry>=2) {
			FinishScan(null);
			return;
		}
		int num6=((scanPageTry!=0)?1:2);
		if(!scanPageClicked) {
			Status=$"Switching to page {num6}.";
			if(CrucibleBestiaryReader.SelectPage(num6)) {
				scanPageClicked=true;
				scanPageClickedAt=DateTime.UtcNow;
			}
			else {
				scanPageTry++;
			}
			return;
		}
		TimeSpan timeSpan=DateTime.UtcNow-scanPageClickedAt;
		if(timeSpan<Buffer) {
			return;
		}
		List<int> list2=(from c in CrucibleBestiaryReader.Cells()
			select c.CatalogueId).ToList();
		if(list2.Count>0&&!list2.OrderBy((int x) => x).SequenceEqual(scanIds.OrderBy((int x) => x))) {
			scanIds=list2;
			scanIndex=0;
			scanReading=false;
			scanTries=0;
			scanStage=1;
			{
				foreach(int scanId2 in scanIds) {
					familiarPage[scanId2]=PageOf(scanIds);
				}
				return;
			}
		}
		if(timeSpan.TotalMilliseconds<1500.0) {
			Status=$"Waiting for page {num6} to show.";
		}
		else {
			scanPageTry++;
			scanPageClicked=false;
		}
	}
	private void FinishScan(string? problem) {
		if(scanLastHover!=0&&CrucibleBestiaryReader.IsOpen) {
			CrucibleBestiaryReader.Hover(scanLastHover,leave: true);
		}
		scanLastHover=0;
		configuration.CrucibleLevelRunsSinceScan=1;
		configuration.Save();
		if(problem!=null) {
			DalamudApi.ChatGui.Print("[Beast Master Helper] Level Up Beasts: "+problem+". Using the ranks already known.");
		}
		levelAttempts=0;
		levelPending=0;
		levelActionAt=DateTime.MinValue;
		fillPage=0;
		Advance(Step.LevelFill,$"Read {scanOk} familiar rank(s); choosing the lowest-ranked familiars.",120);
	}
	private void LevelClose() {
		if(CrucibleBestiaryReader.IsOpen) {
			if(!(DateTime.UtcNow<levelActionAt)) {
				CrucibleBestiaryReader.Close();
				levelActionAt=DateTime.UtcNow.AddSeconds(1.5);
				if(++levelAttempts>6) {
					Stop("Level Up Beasts: could not close the Master's Bestiary.");
				}
			}
			return;
		}
		levelOpenedNotebook=false;
		if(levelRecheck) {
			if(DateTime.UtcNow<levelActionAt) {
				return;
			}
			levelRecheck=false;
			if(!levelRecheckDone) {
				levelRecheckDone=true;
				levelAttempts=0;
				Advance(Step.LevelOpen,"Reopening the Master's Bestiary to confirm the team.",90);
				return;
			}
		}
		List<CrucibleFamiliar> roster=CruciblePetParty.Familiars();
		HashSet<int> hashSet=LevelRegistered(roster);
		if(hashSet.Count==0) {
			Stop("Level Up Beasts: no familiar is registered after filling, so it stopped before challenging.");
			return;
		}
		string text=string.Join(", ",from id in hashSet
			orderby id
			select $"{FamiliarName(id)} ({CrucibleBeastRank.EffectiveRank(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,id)})");
		Advance(Step.Challenge,"Team set: "+text+". Waiting for Challenge This Board.");
	}
	private static bool PageShown(int page) {
		List<int> list=(from c in CrucibleBestiaryReader.Cells()
			select c.CatalogueId).ToList();
		if(list.Count>0) {
			return PageOf(list)==page;
		}
		return false;
	}
	private bool PageSwitchPending() {
		if(pageWaitTarget==0) {
			return false;
		}
		if(PageShown(pageWaitTarget)) {
			pageWaitTarget=0;
			levelActionAt=DateTime.UtcNow+Buffer;
			return false;
		}
		if(DateTime.UtcNow>pageWaitUntil) {
			pageWaitTarget=0;
			return false;
		}
		Status=$"Waiting for Bestiary page {pageWaitTarget}.";
		return true;
	}
	private void PressPage(int page) {
		CrucibleBestiaryReader.SelectPage(page);
		pageWaitTarget=page;
		pageWaitUntil=DateTime.UtcNow.AddMilliseconds(1500.0);
	}
	private void LevelFill() {
		if(!CrucibleBestiaryReader.IsOpen) {
			Stop("Level Up Beasts: the Master's Bestiary closed before the team was filled.");
			return;
		}
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		if(list.Count==0&&levelTeamOverride.Count==0&&CrucibleBestiaryReader.Cells().Count==0) {
			Status="Waiting for the team list to load.";
		}
		else {
			if(!LevelToggleSettled(list)) {
				return;
			}
			HashSet<int> registered=LevelRegistered(list);
			int num=CrucibleBeastRank.BoardRankCap(board);
			int num2=CrucibleBeastRank.BoardTeamSize(board);
			List<int> wanted;
			if(levelSuggested) {
				IReadOnlyList<int> guidePool=CrucibleSuggestedTeam.EntryPool(configuration);
				List<int> list2=new List<int>();
				foreach(int item in guidePool.Where(IsUnlockedFamiliar)) {
					list2.Add(item);
				}
				foreach(int item2 in guidePool.Where((int id) => !IsUnlockedFamiliar(id))) {
					BeastKin kin=BeastmasterKinship.KinOf(item2);
					int num3=((kin!=BeastKin.Unknown)?Enumerable.Range(1,50).FirstOrDefault((int n) => !list2.Contains(n)&&!guidePool.Contains(n)&&IsUnlockedFamiliar(n)&&BeastmasterKinship.KinOf(n)==kin):0);
					if(num3==0) {
						num3=(from n in Enumerable.Range(1,50)
							where !list2.Contains(n)&&!guidePool.Contains(n)&&IsUnlockedFamiliar(n)
							orderby CrucibleBeastRank.EffectiveRank(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,n).GetValueOrDefault() descending,n
							select n).FirstOrDefault();
					}
					if(num3!=0) {
						list2.Add(num3);
					}
				}
				wanted=list2.Take(num2).ToList();
			}
			else {
				wanted = new List<int>();
			
				List<(int catalogueId, int rank, int expPercent)> candidates = new List<(int, int, int)>();
				for(int catalogueId = 1; catalogueId <= 50; catalogueId++) {
					if(!IsUnlockedFamiliar(catalogueId)) continue;
				
					int? rank = CrucibleBeastRank.EffectiveRank(
						configuration.CrucibleBeastRankLearned,
						configuration.CrucibleBeastRankFallback,
						catalogueId);
				
					int expPercent = 100;
					if(rank.HasValue) {
						uint monsterId = CrucibleBeastRank.MonsterIdFor(catalogueId);
						if(configuration.CrucibleBeastRankLearned.ContainsKey(monsterId)) {
							expPercent = configuration.CrucibleBeastExpLearned.GetValueOrDefault(monsterId, 100);
						} else if(configuration.CrucibleBeastRankFallback.ContainsKey(catalogueId)) {
							expPercent = configuration.CrucibleBeastExpLearned.GetValueOrDefault(monsterId, 100);
						}
					}
				
					candidates.Add((catalogueId, rank ?? 1, expPercent));
				}
			
				wanted = candidates
					.Where(c => c.rank < num || (c.rank == num && c.expPercent < 100))
					.OrderBy(c => c.rank)
					.ThenBy(c => c.catalogueId)
					.Take(num2)
					.Select(c => c.catalogueId)
					.ToList();
			}
			if(!levelSuggested&&wanted.Count<num2) {
				IEnumerable<int> collection = Enumerable.Range(1, 50)
					.Where(id => !wanted.Contains(id) && IsUnlockedFamiliar(id))
					.Take(num2 - wanted.Count);
				wanted.AddRange(collection);
			}
			int changeId=registered.OrderBy((int id) => id).FirstOrDefault((int id) => !wanted.Contains(id));
			bool flag=false;
			if(levelSuggested&&!suggestedCleared) {
				IEnumerable<int> second=from e in CrucibleBestiaryReader.Page()
					where e.Selected
					select e.CatalogueId;
				changeId=(from id in registered.Union(second)
					where !levelTeamOverride.TryGetValue(id,out var value2)||value2
					orderby id
					select id).FirstOrDefault();
				if(changeId==0) {
					if(PageSwitchPending()) {
						return;
					}
					List<int> list3=(from c in CrucibleBestiaryReader.Cells()
						select c.CatalogueId).ToList();
					if(list3.Count==0) {
						Status="Waiting for the Bestiary page to draw.";
						return;
					}
					int num4=PageOf(list3);
					suggestedPagesClear|=num4;
					if(suggestedPagesClear==3) {
						suggestedCleared=true;
						levelActionAt=DateTime.UtcNow+Buffer;
						Status="The team is empty; registering the guide's team.";
					}
					else if(!(DateTime.UtcNow<levelActionAt)) {
						fillPage=((num4!=1)?1:2);
						PressPage(fillPage);
						Status=$"Checking page {fillPage} for anyone still selected.";
					}
					return;
				}
				if(CrucibleBestiaryReader.IsRegistered(changeId)==false) {
					levelTeamOverride[changeId]=false;
					return;
				}
			}
			else if(changeId==0) {
				if(wanted.Count==0) {
					Stop($"Level Up Beasts: every familiar is at this board's rank cap of {num} with a full exp bar, so nothing was selected.");
					return;
				}
				changeId=wanted.FirstOrDefault((int id) => !registered.Contains(id));
				flag=true;
			}
			if(changeId==0) {
				levelAttempts=0;
				Advance(Step.LevelClose,"Team filled; closing the Master's Bestiary.",60);
				return;
			}
			string text=FamiliarName(changeId);
			if(DateTime.UtcNow<levelActionAt) {
				return;
			}
			if(levelAttempts>=14) {
				Stop("Level Up Beasts: could not settle the team.");
			}
			else if(!CrucibleBestiaryReader.Cells().Any((CrucibleBestiaryReader.GridCell c) => c.CatalogueId==changeId)) {
				if(!PageSwitchPending()) {
					int num5=(familiarPage.TryGetValue(changeId,out var value)?value:((changeId<=25)?1:2));
					fillPage=((fillPage!=num5)?num5:((num5!=1)?1:2));
					levelAttempts++;
					PressPage(fillPage);
					Status=$"Looking for {text} on page {fillPage}.";
				}
			}
			else if(fillHovered!=changeId) {
				if(fillHovered!=0) {
					CrucibleBestiaryReader.Hover(fillHovered,leave: true);
				}
				CrucibleBestiaryReader.Hover(changeId);
				fillHovered=changeId;
				levelActionAt=DateTime.UtcNow+Buffer;
				Status="Pointing at "+text+".";
			}
			else {
				fillHovered=0;
				if(!CrucibleBestiaryReader.Toggle(changeId)) {
					Stop("Level Up Beasts: could not click "+text+" in the Master's Bestiary.");
					return;
				}
				levelAttempts++;
				levelPending=changeId;
				levelPendingSelected=flag;
				levelPendingCount=list.Count;
				levelPendingUntil=DateTime.UtcNow.AddSeconds(6.0);
				levelActionAt=DateTime.UtcNow+Buffer;
				Status=(flag?"Adding":"Removing")+" "+text+".";
			}
		}
	}
	private static IGameObject? FindLauda() {
		return DalamudApi.ObjectTable.FirstOrDefault((IGameObject a) => a.IsTargetable&&a.Name.TextValue.Equals("Lauda",StringComparison.OrdinalIgnoreCase)&&Vector3.Distance(a.Position,LaudaPosition)<15f);
	}
	public void Dispose() {
		Preparation.Dispose();
		Diagnostics.Dispose();
		Recorder.Dispose();
		CrucibleFeedFeedback.Shared.Dispose();
		DalamudApi.Framework.Update-=Update;
		Stop();
	}
}
