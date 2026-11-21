using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
namespace Beastmaster;

public sealed class CrucibleBoardService : IDisposable {
	private struct FightBuffState {
		public int Attempts;
		public DateTime RetryAfterUtc;
		public bool Confirmed;
		private string note;
		public string Note {
			get {
				return note ?? string.Empty;
			}
			set {
				note=value;
			}
		}
	}
	private readonly BeastmasterConfiguration configuration;
	private readonly CrucibleLootProgress lootProgress=new CrucibleLootProgress();
	private DateTime nextActionUtc=DateTime.MinValue;
	private DateTime nextTickUtc=DateTime.MinValue;
	private readonly HashSet<int> boughtThisVisit=new HashSet<int>();
	private bool shopWasOpen;
	private int shopScanRow;
	private int shopScanRows=-1;
	private DateTime shopScanFinishedUtc=DateTime.MinValue;
	private readonly HashSet<int> unaffordableThisVisit=new HashSet<int>();
	private readonly HashSet<int> ungiveableThisBoard=new HashSet<int>();
	private readonly HashSet<int> gearTakenThisBoard=new HashSet<int>();
	private DateTime lastBuyAttemptUtc=DateTime.MinValue;
	private int lastBuyAttemptId;
	private readonly BeastmasterNavigationService navigation;
	private readonly BeastmasterCrucibleService entry;
	private bool repeatPending;
	private DateTime repeatAfterUtc=DateTime.MinValue;
	private Vector3 advanceTarget;
	private DateTime advanceStartedUtc=DateTime.MinValue;
	private bool advancing;
	private uint advanceTerritory;
	private string platformLabel=string.Empty;
	private DateTime platformArrivedUtc=DateTime.MinValue;
	private bool platformEventSeen;
	private bool platformEventDone;
	private DateTime platformEventEndedUtc=DateTime.MinValue;
	private bool awaitingBoardReturn=true;
	private DateTime lastOnPlatformUtc=DateTime.MinValue;
	private uint lastTerritory;
	private readonly HashSet<int> tookThisTreasure=new HashSet<int>();
	private bool treasureWasOpen;
	private string feedItemName=string.Empty;
	private DateTime feedAskedSinceUtc=DateTime.MinValue;
	private bool campsiteWasOpen;
	private int campsitePendingId;
	private bool campsitePendingSelected;
	private DateTime campsitePendingUntilUtc=DateTime.MinValue;
	private DateTime confirmExpectedUntilUtc=DateTime.MinValue;
	private string confirmBecause=string.Empty;
	private readonly BossModBridge bossMod;
	private string tokenOfferKey=string.Empty;
	private int tokenAttempts;
	private bool tokensConfirmed;
	private static readonly Regex TokenChatLine=new Regex("You obtain ([\\d,\\.]+) territory tokens?(?:\\s*\\([^)]*\\))?\\s+as loot",RegexOptions.IgnoreCase|RegexOptions.Compiled|RegexOptions.CultureInvariant);
	private static readonly TimeSpan TokenChatLookback=TimeSpan.FromSeconds(8L);
	private DateTime lastTokenChatUtc=DateTime.MinValue;
	private DateTime tokenLastAttemptUtc=DateTime.MinValue;
	private bool lootWasOpen;
	private bool tokenCardSeenThisLoot;
	private DateTime lootOpenedUtc=DateTime.MinValue;
	private int lootWindowsSeen;
	private int tokenChatCount;
	private int tokenMissCount;
	private int lastTokenAmount;
	private DateTime tokenFirstSeenUtc=DateTime.MinValue;
	private const int TokenAttemptLimit=5;
	private FightBuffState tomeState;
	private FightBuffState essenceState;
	private FightBuffState reflectState;
	private FightBuffState autoPotionState;
	private FightBuffState merchantsEyeState;
	private FightBuffState thiefsEyeState;
	private DateTime featherRetryAfterUtc=DateTime.MinValue;
	private const int CrucibleFeatherGrant=100;
	private int fightsSinceRest;
	private string partyCheckKey=string.Empty;
	private int partyCheckStage;
	private int partyCheckAttempts;
	private DateTime partyCheckUntil;
	public string LootPlan { get; private set; }="No loot pending.";
	public bool Paused { get; private set; }
	private bool InLiveFight {
		get {
			if(!bossMod.IsInstalled) {
				return DalamudApi.Condition[ConditionFlag.InCombat];
			}
			if(bossMod.InEncounter) {
				return bossMod.InCrucibleEncounter;
			}
			return false;
		}
	}
	public string Status { get; private set; }="Idle";
	public string ShopPlan { get; private set; }="No shop visited yet.";
	public string TreasurePlan { get; private set; }="No treasure opened yet.";
	public IReadOnlyList<CrucibleItem> Held { get; private set; }=Array.Empty<CrucibleItem>();
	public IReadOnlyList<CrucibleItem> Gear { get; private set; }=Array.Empty<CrucibleItem>();
	public int OccupiedSlots { get; private set; }=-1;
	public string CapacityPlan {
		get {
			if(OccupiedSlots>=0) {
				return $"Crucible items: {OccupiedSlots}/{10} slots used ({10-OccupiedSlots} free). "+((Gear.Count==0)?"No beast gear worn.":($"Beast gear ({Gear.Count}, separate store, never counted against the ten): "+string.Join(", ",Gear.Select((CrucibleItem g) => g.Name))));
			}
			return "Crucible items: the board HUD is not readable, so capacity is unknown.";
		}
	}
	public bool IsEnabled => configuration.CrucibleBoardAssist;
	public string TokenStatus => $"Tokens: {tokenChatCount} confirmed in chat"+((lastTokenAmount>0)?$" (last +{lastTokenAmount})":string.Empty)+$", {tokenMissCount} possibly missed";
	public string BuffStatus => $"Blink: {Describe(tomeState)}. Reflect: {Describe(reflectState)}. Essence: {Describe(essenceState)}. Auto-potion: {Describe(autoPotionState)}. Merchant's Eye: {Describe(merchantsEyeState)}. Thief's Eye: {Describe(thiefsEyeState)}.";
	public string FeatherStatus { get; private set; }="not evaluated";
	public string BranchPlan { get; private set; }="No branch reached yet.";
	public void TogglePause() {
		Paused=!Paused;
		if(Paused&&advancing) {
			StopAdvance("paused");
		}
		Status=(Paused?"Paused - walk where you like; everything else still runs":"Resumed - routing to the next platform again");
	}
	private void ExpectConfirm(string because,DateTime now) {
		confirmBecause=because;
		confirmExpectedUntilUtc=now.AddSeconds(10.0);
	}
	public CrucibleBoardService(BeastmasterConfiguration configuration,BeastmasterNavigationService navigation,BeastmasterCrucibleService entry,BossModBridge bossMod) {
		this.configuration=configuration;
		this.navigation=navigation;
		this.entry=entry;
		this.bossMod=bossMod;
		DalamudApi.Framework.Update+=Update;
		DalamudApi.ChatGui.ChatMessage+=OnTokenChat;
	}
	public void Dispose() {
		DalamudApi.Framework.Update-=Update;
		DalamudApi.ChatGui.ChatMessage-=OnTokenChat;
		if(advancing) {
			StopAdvance("the plugin unloaded");
		}
	}
	private void Update(IFramework framework) {
		DateTime utcNow=DateTime.UtcNow;
		if(repeatPending&&!DalamudApi.Condition[ConditionFlag.BoundByDuty]&&utcNow>=repeatAfterUtc) {
			repeatPending=false;
			if(configuration.CrucibleLoopSelectedBoard) {
				entry.Start(Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1),configuration.CrucibleRecommendedTeam);
				Status="Repeating "+BeastmasterCrucibleService.BoardNames[Math.Clamp(configuration.CrucibleBoard,0,BeastmasterCrucibleService.BoardNames.Length-1)]+".";
			}
		}
		if(utcNow<nextTickUtc) {
			return;
		}
		nextTickUtc=utcNow.AddMilliseconds(250.0);
		if(advancing) {
			string text=AdvanceBlocker();
			if(text!=null) {
				StopAdvance(text);
				return;
			}
		}
		if(!configuration.CrucibleBoardAssist) {
			Status="Board assist is off";
			return;
		}
		try {
			TrackPlatformEvent(utcNow);
			Held=CrucibleContents.ItemsIn("XBMContentsMainHUD");
			Gear=CrucibleContents.GearIn("XBMContentsMainHUD");
			OccupiedSlots=CrucibleContents.OccupiedItemSlots();
			if(CruciblePetParty.IsOpen) {
				CruciblePetParty.Familiars();
			}
			if(utcNow<nextActionUtc) {
				return;
			}
			if(CrucibleUi.YesNoOpen) {
				HandleConfirmation(utcNow);
				return;
			}
			if(CrucibleContents.BoardFinished&&configuration.CrucibleLoopSelectedBoard) {
				repeatPending=true;
				repeatAfterUtc=utcNow.AddSeconds(5.0);
			}
			if(CrucibleContents.BoardFinished) {
				if(CrucibleContents.LeaveBoard(out string what)) {
					ExpectConfirm("finishing the board",utcNow);
					nextActionUtc=utcNow.AddMilliseconds(1500.0);
					Status="Board finished - "+what;
				}
				else {
					nextActionUtc=utcNow.AddSeconds(3.0);
					Status="Board finished but could not leave - "+what;
				}
			}
			else {
				if(HandleCampsite(utcNow)||HandleFeedTarget(utcNow)) {
					return;
				}
				if(CrucibleContents.LootWaiting) {
					if(!lootWasOpen) {
						lootWasOpen=true;
						lootOpenedUtc=utcNow;
						tokenCardSeenThisLoot=false;
						lootWindowsSeen++;
					}
					HandleLoot(utcNow);
					return;
				}
				if(lootWasOpen) {
					lootWasOpen=false;
					if(!tokensConfirmed&&tokenCardSeenThisLoot&&tokenChatCount>0&&lastTokenChatUtc<lootOpenedUtc-TokenChatLookback) {
						tokenMissCount++;
						DalamudApi.ChatGui.PrintError("[Beast Master Helper] The loot window closed without Territory Tokens being collected "+$"({tokenMissCount} missed so far; the last token line came "+$"{Math.Abs((lootOpenedUtc-lastTokenChatUtc).TotalSeconds):0}s "+((lastTokenChatUtc<lootOpenedUtc)?"before":"after")+" this window opened).");
						DalamudApi.Log.Warning("Territory Tokens miss #{Count}: window open {OpenSeconds:0.0}s, {Attempts} attempt(s), last card state read was during this window, offer '{Offer}', last chat {ChatAge:0.0}s {ChatDirection} window open.",tokenMissCount,(utcNow-lootOpenedUtc).TotalSeconds,tokenAttempts,tokenOfferKey,Math.Abs((lootOpenedUtc-lastTokenChatUtc).TotalSeconds),(lastTokenChatUtc<lootOpenedUtc)?"before":"after");
					}
				}
				lootProgress.Reset();
				tokenOfferKey="<loot closed>";
				tokensConfirmed=false;
				tokenAttempts=0;
				LootPlan="No loot pending.";
				bool treasureWaiting=CrucibleContents.TreasureWaiting;
				if(!treasureWaiting&&treasureWasOpen) {
					tookThisTreasure.Clear();
				}
				treasureWasOpen=treasureWaiting;
				if(treasureWaiting) {
					HandleTreasure(utcNow);
					return;
				}
				bool shopWaiting=CrucibleContents.ShopWaiting;
				if(shopWaiting&&!shopWasOpen) {
					shopScanRow=0;
					shopScanRows=-1;
					shopScanFinishedUtc=DateTime.MinValue;
				}
				if(!shopWaiting&&shopWasOpen) {
					boughtThisVisit.Clear();
					unaffordableThisVisit.Clear();
				}
				shopWasOpen=shopWaiting;
				if(shopWaiting) {
					if(CruciblePetParty.IsOpen) {
						string text2=((feedItemName.Length==0)?"an item":feedItemName);
						nextActionUtc=utcNow.AddMilliseconds(500.0);
						Status="Waiting to give away "+text2+" before buying anything else";
					}
					else {
						HandleShop(utcNow);
					}
				}
				else {
					if(HandleLeftover(utcNow)||HandleAutoPotion(utcNow)||HandleHealing(utcNow)||HandleFeather(utcNow)||HandleThiefsEye(utcNow)||HandleTome(utcNow)||HandleVampiric(utcNow)||HandleReflect(utcNow)||HandleMerchantsEye(utcNow)||HandleFang(utcNow)||HandlePartyCheck(utcNow)) {
						return;
					}
					if(Paused) {
						if(advancing) {
							StopAdvance("paused");
						}
						Status="Paused - walk where you like; everything else still runs";
					}
					else {
						HandleAdvance(utcNow);
					}
				}
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Crucible board assist failed this tick.");
			Status="Board assist hit an error - see the log";
		}
	}
	private void HandleConfirmation(DateTime now) {
		string value=((now<=confirmExpectedUntilUtc&&confirmBecause.Length>0)?confirmBecause:"nothing here declared it, and a confirmation means what it says");
		if(CrucibleUi.ConfirmYes()) {
			confirmExpectedUntilUtc=DateTime.MinValue;
			nextActionUtc=now.AddMilliseconds(800.0);
			Status=$"Confirmed - {value} ({CrucibleUi.LastYesNo})";
		}
		else {
			nextActionUtc=now.AddSeconds(2.0);
			Status="A confirmation is open but could not be handled - "+CrucibleUi.LastYesNo;
		}
	}
	private void HandleLoot(DateTime now) {
		if(!configuration.CrucibleTakeAllLoot) {
			Status="Loot is waiting - automatic loot selection is off";
			return;
		}
		List<CrucibleItem> source=CrucibleContents.ItemsIn("XBMContentsBooty");
		string offerKey=string.Join(",",source.Select((CrucibleItem i) => i.Id).Distinct().Order());
		if(!HandleTokens(now,offerKey)) {
			return;
		}
		HashSet<int> heldGearLoot=HeldGearIds();
		List<CrucibleItemPick> list=CrucibleItemChoice.TreasurePlan((from item in source
			where !item.IsUnusedByPlugin
			where item.Kind!=CrucibleItemKind.BeastGear||!heldGearLoot.Contains(item.Id)
			where item.Kind!=CrucibleItemKind.Feed||(!ungiveableThisBoard.Contains(item.Id)&&CrucibleItemChoice.AnyoneCanReceive(CruciblePetParty.LastKnown,item,configuration.CrucibleFeedMinHpPercent))
			select item).ToList(),Enumerable.Count(Held,(CrucibleItem i) => i.IsHealingStock),configuration.CrucibleHealingStock,PlayerHpPercent()<configuration.CrucibleDesperateHpPercent);
		string text=(source.Any((CrucibleItem item) => item.IsTemporalSand)?" Temporal Sand skipped.":string.Empty);
		LootPlan="Individual loot ranking: "+string.Join(" > ",list.Select((CrucibleItemPick p) => p.Item.Name))+text;
		if(list.Count==0) {
			nextActionUtc=now.AddSeconds(1.0);
			Status=(CrucibleContents.Dismiss("XBMContentsBooty",out string what)?("No wanted loot remains."+text+" Closed with "+what):("No wanted loot remains but the loot window would not close: "+what));
			return;
		}
		CrucibleItemPick crucibleItemPick=list.FirstOrDefault((CrucibleItemPick p) => lootProgress.TryBegin(offerKey+":"+p.Item.Id));
		string how;
		if(!crucibleItemPick.HasItem) {
			nextActionUtc=now.AddSeconds(1.0);
			Status=(CrucibleContents.Dismiss("XBMContentsBooty",out string what2)?("Finished wanted loot; discarded unselected cards with "+what2+"."+text):("Every wanted card was tried but the loot window would not close: "+what2));
		}
		else if(CrucibleContents.TakeLootItem(crucibleItemPick.Item,out how)) {
			if(crucibleItemPick.Item.Kind==CrucibleItemKind.BeastGear) {
				gearTakenThisBoard.Add(crucibleItemPick.Item.Id);
			}
			ExpectConfirm("taking "+crucibleItemPick.Item.Name,now);
			nextActionUtc=now.AddSeconds(2.0);
			Status="Requested "+crucibleItemPick.Item.Name+"; waiting for loot to change. "+crucibleItemPick.Reason;
		}
		else {
			Status="Could not pick "+crucibleItemPick.Item.Name+"; stopped without retrying. "+how;
		}
	}
	private void OnTokenChat(IHandleableChatMessage message) {
		try {
			Match match=TokenChatLine.Match(message.Message.TextValue);
			if(match.Success) {
				lastTokenChatUtc=DateTime.UtcNow;
				tokenChatCount++;
				int.TryParse(match.Groups[1].Value.Replace(",",string.Empty).Replace(".",string.Empty),out lastTokenAmount);
			}
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read a loot chat line.");
		}
	}
	private bool HandleTokens(DateTime now,string offerKey) {
		if(tokenOfferKey!=offerKey) {
			tokenOfferKey=offerKey;
			tokenAttempts=0;
			tokensConfirmed=false;
			tokenFirstSeenUtc=now;
		}
		if(tokensConfirmed) {
			return true;
		}
		if(lastTokenChatUtc>=tokenFirstSeenUtc-TokenChatLookback) {
			tokensConfirmed=true;
			Status=$"Territory Tokens confirmed in chat (+{lastTokenAmount}) after {Math.Max(1,tokenAttempts)} attempt(s).";
			return true;
		}
		switch(CrucibleTreasureReader.TerritoryTokenState()) {
		case CrucibleTreasureReader.TokenCardState.Acquired:
		if(WaitingForTokenChat(now)) {
			return false;
		}
		tokensConfirmed=true;
		Status=$"Territory Tokens confirmed collected after {Math.Max(1,tokenAttempts)} attempt(s).";
		return true;
		case CrucibleTreasureReader.TokenCardState.Absent:
		if(tokenAttempts==0) {
			int num=((offerKey.Length!=0)?1:8);
			if(now<tokenFirstSeenUtc.AddSeconds(num)) {
				nextActionUtc=now.AddMilliseconds(400.0);
				Status="Loot is open; waiting for its cards before collecting Territory Tokens.";
				return false;
			}
		}
		if(tokenAttempts>0&&WaitingForTokenChat(now)) {
			return false;
		}
		tokensConfirmed=true;
		return true;
		case CrucibleTreasureReader.TokenCardState.Unknown:
		nextActionUtc=now.AddMilliseconds(500.0);
		Status="Waiting for the loot window before collecting Territory Tokens.";
		return false;
		default: {
			tokenCardSeenThisLoot=true;
			tokenLastAttemptUtc=now;
			tokenAttempts++;
			if(tokenAttempts%5==0) {
				DalamudApi.ChatGui.PrintError($"[Beast Master Helper] Territory Tokens still not collected after {tokenAttempts} attempts. "+"Still retrying - take the token card by hand if this keeps happening; the shop budget depends on it.");
			}
			if(CrucibleTreasureReader.TakeTerritoryTokens(out string result)) {
				ExpectConfirm("taking Territory Tokens",now);
				nextActionUtc=now.AddMilliseconds(1200.0);
				Status=$"Taking post-fight tokens, attempt {tokenAttempts} - {result}";
				DalamudApi.Log.Information("Territory Tokens: click dispatched (attempt {Attempt}, offer '{Offer}') - {Result}",tokenAttempts,offerKey,result);
				return false;
			}
			nextActionUtc=now.AddSeconds(1.0);
			Status=$"Territory Tokens card would not accept a click (attempt {tokenAttempts}) - {result}";
			DalamudApi.Log.Warning("Territory Tokens: click refused (attempt {Attempt}, offer '{Offer}') - {Result}",tokenAttempts,offerKey,result);
			return false;
		}
		}
	}
	private bool WaitingForTokenChat(DateTime now) {
		if((tokenChatCount<=0&&lootWindowsSeen>=3)||now>=tokenLastAttemptUtc.AddSeconds(2.5)) {
			return false;
		}
		nextActionUtc=now.AddMilliseconds(300.0);
		Status="Territory Tokens look taken - waiting for the chat line to confirm.";
		return true;
	}
	private void HandleTreasure(DateTime now) {
		if(!configuration.CrucibleAutoTreasure) {
			Status="A treasure is open - automatic choosing is switched off";
			return;
		}
		List<CrucibleTreasureOffer> list=CrucibleTreasureReader.Offers();
		if(list.Count==0) {
			nextActionUtc=now.AddSeconds(2.0);
			Status="Treasure is open but nothing could be read - "+CrucibleTreasureReader.LastResult;
			return;
		}
		if(tookThisTreasure.Count>0||list.Any((CrucibleTreasureOffer offer) => offer.Acquired)) {
			nextActionUtc=now.AddSeconds(1.0);
			Status=(CrucibleContents.Dismiss("XBMContentsTreasure",out string what)?("Treasure taken - closed it with "+what):("Treasure taken but it will not close - "+what+". Still open: "+CrucibleContents.DescribeBlockingWindows()));
			return;
		}
		List<CrucibleTreasureOffer> list2=list.Where((CrucibleTreasureOffer offer) => !offer.Acquired).ToList();
		if(list2.Count==0) {
			nextActionUtc=now.AddSeconds(1.0);
			Status=(CrucibleContents.Dismiss("XBMContentsTreasure",out string what2)?("Nothing left on the treasure - closed it with "+what2):("Nothing left on the treasure and it will not close - "+what2));
			return;
		}
		int healingStockHeld=Enumerable.Count(Held,(CrucibleItem item) => item.IsHealingStock);
		bool hurt=PlayerHpPercent()<configuration.CrucibleDesperateHpPercent;
		HashSet<int> heldGearTreasure=HeldGearIds();
		List<CrucibleItem> list3=(from offer in list2
			select offer.Item into item
			where item.Kind!=CrucibleItemKind.BeastGear||!heldGearTreasure.Contains(item.Id)
			where item.Kind!=CrucibleItemKind.Feed||(!ungiveableThisBoard.Contains(item.Id)&&CrucibleItemChoice.AnyoneCanReceive(CruciblePetParty.LastKnown,item,configuration.CrucibleFeedMinHpPercent))
			select item).ToList();
		if(list3.Count==0) {
			list3=list2.Select((CrucibleTreasureOffer offer) => offer.Item).ToList();
		}
		TreasurePlan=CrucibleItemChoice.DescribeTreasurePlan(list3,healingStockHeld,configuration.CrucibleHealingStock,hurt);
		foreach(CrucibleItemPick pick in CrucibleItemChoice.TreasurePlan(list3,healingStockHeld,configuration.CrucibleHealingStock,hurt)) {
			CrucibleTreasureOffer crucibleTreasureOffer=list2.FirstOrDefault((CrucibleTreasureOffer candidate) => candidate.Item.Id==pick.Item.Id);
			if(!(crucibleTreasureOffer==null)&&CrucibleTreasureReader.Take(crucibleTreasureOffer)) {
				tookThisTreasure.Add(crucibleTreasureOffer.Param);
				if(crucibleTreasureOffer.Item.Kind==CrucibleItemKind.BeastGear) {
					gearTakenThisBoard.Add(crucibleTreasureOffer.Item.Id);
				}
				ExpectFeedTarget(crucibleTreasureOffer.Item,now);
				ExpectConfirm("taking "+pick.Item.Name,now);
				nextActionUtc=now.AddMilliseconds(1200.0);
				Status=$"Taking {pick.Item.Name} - best of {list.Count}: {pick.Reason}";
				return;
			}
		}
		nextActionUtc=now.AddSeconds(3.0);
		Status="Could not take a treasure card - "+CrucibleTreasureReader.LastResult;
	}
	private void HandleShop(DateTime now) {
		if(!configuration.CrucibleAutoTreasure) {
			Status="Shop is open - automatic buying is switched off";
			return;
		}
		int num=CrucibleListClick.RowCount("XBMContentsItemShop");
		if(num<=0) {
			nextActionUtc=now.AddMilliseconds(250.0);
			Status="Shop opened; waiting for its item list.";
			return;
		}
		if(shopScanRows!=num) {
			shopScanRows=num;
			shopScanRow=0;
			shopScanFinishedUtc=DateTime.MinValue;
		}
		if(shopScanRow<shopScanRows) {
			if(CrucibleListClick.RealiseRow("XBMContentsItemShop",shopScanRow,out var _)) {
				shopScanRow++;
				nextActionUtc=now.AddMilliseconds(100.0);
				Status=$"Reading complete shop list first: {shopScanRow}/{shopScanRows} rows shown.";
			}
			else {
				nextActionUtc=now.AddMilliseconds(250.0);
				Status=$"Waiting to show shop row {shopScanRow+1}/{shopScanRows}.";
			}
		}
		else if(shopScanFinishedUtc==DateTime.MinValue) {
			shopScanFinishedUtc=now;
			nextActionUtc=now.AddMilliseconds(300.0);
			Status=$"All {shopScanRows} shop rows shown; waiting one render before pricing.";
		}
		else {
			if((now-shopScanFinishedUtc).TotalMilliseconds<250.0) {
				return;
			}
			if(lastBuyAttemptId!=0&&CrucibleFeedFeedback.Shared.RefusedForTokensSince(lastBuyAttemptUtc)) {
				unaffordableThisVisit.Add(lastBuyAttemptId);
				boughtThisVisit.Remove(lastBuyAttemptId);
				string text=CrucibleItemCatalogue.Get(lastBuyAttemptId)?.Name ?? $"item {lastBuyAttemptId}";
				lastBuyAttemptId=0;
				nextActionUtc=now.AddMilliseconds(600.0);
				Status="The game refused "+text+" for want of tokens - its price was read wrong, moving on";
				return;
			}
			List<CrucibleShopOffer> list=CrucibleShopReader.Offers();
			if(list.Count==0) {
				nextActionUtc=now.AddSeconds(2.0);
				Status="Shop is open but the shelf could not be read";
				return;
			}
			int tokens=CrucibleShopReader.Tokens;
			int num2=Enumerable.Count(Held,(CrucibleItem item) => item.IsHealingStock);
			bool flag=PlayerHpPercent()<configuration.CrucibleDesperateHpPercent;
			int num3=configuration.CrucibleHealingStock+(flag?2:0);
			List<CrucibleItem> source=(from item in boughtThisVisit.Select(CrucibleItemCatalogue.Get)
				where item!=null
				select item).ToList();
			HashSet<int> heldGear=HeldGearIds();
			IReadOnlyList<CrucibleFamiliar> roster=CruciblePetParty.LastKnown;
			List<CrucibleShopOffer> offers=list.Where((CrucibleShopOffer offer) => !boughtThisVisit.Contains(offer.Item.Id)&&!unaffordableThisVisit.Contains(offer.Item.Id)&&!ungiveableThisBoard.Contains(offer.Item.Id)&&!heldGear.Contains(offer.Item.Id)&&(offer.Item.Kind!=CrucibleItemKind.Feed||CrucibleItemChoice.AnyoneCanReceive(roster,offer.Item,configuration.CrucibleFeedMinHpPercent))).ToList();
			int num4=Math.Max(0,configuration.CruciblePlayerBuyLimit-Enumerable.Count(source,(CrucibleItem item) => CrucibleShopPlan.TierOf(item)==CrucibleShopTier.PlayerGear));
			int num5=Math.Max(0,configuration.CrucibleFamiliarBuffTarget-Enumerable.Count(source,(CrucibleItem item) => CrucibleShopPlan.TierOf(item)==CrucibleShopTier.FamiliarBuff));
			List<CrucibleShopOffer> list2=CrucibleShopPlan.Order(offers,num2,num3,num4,num5,tokens);
			int num6=CrucibleContents.FreeItemSlots();
			int num7=((num6<0)?int.MaxValue:Math.Max(0,num6-Enumerable.Count(source,TakesAnItemSlot)));
			ShopPlan=$"{tokens} tokens, holding {num2} healing of {num3} wanted, {num4} player gear and {num5} familiar buff slot(s) remain. "+((num6<0)?"Carried-slot count unreadable, so no capacity cap. ":$"{num6} of {10} carried slots free ({num7} consumable purchase(s) still fit). ")+CrucibleShopReader.LastRead+". Order: "+string.Join(" -> ",list2.Select((CrucibleShopOffer o) => $"{o.Item.Name}({o.Price},{CrucibleShopPlan.TierOf(o.Item)})"));
			foreach(CrucibleShopOffer item in list2) {
				if((num7>0||!TakesAnItemSlot(item.Item))&&!boughtThisVisit.Contains(item.Item.Id)&&!unaffordableThisVisit.Contains(item.Item.Id)&&(tokens<0||item.Price<=0||item.Price<=tokens)&&CrucibleContents.BuyFromShop(item)) {
					boughtThisVisit.Add(item.Item.Id);
					if(item.Item.Kind==CrucibleItemKind.BeastGear) {
						gearTakenThisBoard.Add(item.Item.Id);
					}
					lastBuyAttemptUtc=now;
					lastBuyAttemptId=item.Item.Id;
					ExpectFeedTarget(item.Item,now);
					ExpectConfirm("buying "+item.Item.Name,now);
					nextActionUtc=now.AddMilliseconds(1200.0);
					Status=$"Buying {item.Item.Name} for {item.Price}: {CrucibleShopPlan.Reason(item)}";
					return;
				}
			}
			nextActionUtc=now.AddSeconds(3.0);
			string value=CrucibleContents.LastBuyPath+"; "+CrucibleUi.LastRowClick;
			string text2=((tokens>=0)?$"Nothing left worth buying - {tokens} tokens, {boughtThisVisit.Count} bought this visit ({value})":$"Nothing further bought this visit ({boughtThisVisit.Count}; {value})");
			Status=(CrucibleContents.Dismiss("XBMContentsItemShop",out string what)?(text2+" - closed it with "+what):text2);
		}
	}
	private bool HandleLeftover(DateTime now) {
		if(CrucibleContents.LeftoverWindow().Length==0) {
			return false;
		}
		if(CrucibleContents.DismissLeftover(out string what)) {
			nextActionUtc=now.AddMilliseconds(800.0);
			Status="Dismissed "+what;
			return true;
		}
		nextActionUtc=now.AddSeconds(3.0);
		Status="A window is blocking the walk and could not be dismissed: "+what;
		return true;
	}
	private bool HandleFeedTarget(DateTime now) {
		if(!CruciblePetParty.IsOpen||CrucibleContents.IsOpen("XBMStageDetailList")) {
			feedAskedSinceUtc=DateTime.MinValue;
			return false;
		}
		if(feedAskedSinceUtc==DateTime.MinValue) {
			feedAskedSinceUtc=now;
		}
		string text=((feedItemName.Length==0)?"an item":feedItemName);
		if((now-feedAskedSinceUtc).TotalSeconds>20.0) {
			feedAskedSinceUtc=DateTime.MinValue;
			CrucibleItem crucibleItem=CrucibleItemCatalogue.ByName(feedItemName);
			if(crucibleItem!=null) {
				ungiveableThisBoard.Add(crucibleItem.Id);
			}
			nextActionUtc=now.AddSeconds(2.0);
			Status=(CrucibleContents.Dismiss("XBMPetParty",out string what)?("Gave up giving away "+text+" and closed the team window with "+what):("Stuck giving away "+text+" and the team window will not close - "+what));
			return true;
		}
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		if(list.Count==0) {
			nextActionUtc=now.AddSeconds(1.0);
			Status="The team window opened for "+text+" but could not be read - "+CruciblePetParty.LastResult;
			return true;
		}
		CrucibleItem crucibleItem2=CrucibleItemCatalogue.ByName(feedItemName);
		if(crucibleItem2!=null&&crucibleItem2.Kind!=CrucibleItemKind.Feed) {
			return GiveConsumable(crucibleItem2,list,now);
		}
		CrucibleFamiliar crucibleFamiliar=CrucibleItemChoice.FeedTarget(list,crucibleItem2,configuration.CrucibleFeedMinHpPercent);
		if(crucibleFamiliar==null) {
			feedAskedSinceUtc=DateTime.MinValue;
			nextActionUtc=now.AddSeconds(2.0);
			if(crucibleItem2!=null) {
				ungiveableThisBoard.Add(crucibleItem2.Id);
			}
			string text2=CrucibleFeedFeedback.Shared.Describe();
			int value=Enumerable.Count(list,(CrucibleFamiliar f) => f.MaxHP!=0&&(float)f.HP*100f/(float)f.MaxHP>=configuration.CrucibleFeedMinHpPercent);
			string text3=$"none of the {list.Count} familiars can take {text} - {value} above {configuration.CrucibleFeedMinHpPercent:0}% HP, {CrucibleFeedSuitability.Describe(crucibleItem2)}"+((text2.Length==0)?string.Empty:("; "+text2));
			Status=(CrucibleContents.Dismiss("XBMPetParty",out string what2)?(text3+" - closed the team window with "+what2):(text3+" - and the team window will not close: "+what2));
			return true;
		}
		if(CruciblePetParty.GiveTo(crucibleFamiliar,out string what3)) {
			feedAskedSinceUtc=DateTime.MinValue;
			CrucibleFeedFeedback.Shared.Ate(crucibleFamiliar.Name);
			ExpectConfirm("giving "+text+" to "+crucibleFamiliar.Name,now);
			nextActionUtc=now.AddMilliseconds(1200.0);
			Status=CrucibleItemChoice.FeedReason(crucibleFamiliar,CrucibleItemCatalogue.ByName(feedItemName));
			return true;
		}
		nextActionUtc=now.AddSeconds(2.0);
		Status=$"Wanted to give {feedItemName} to {crucibleFamiliar.Name} but {what3}";
		return true;
	}
	private bool HandleCampsite(DateTime now) {
		if(!CruciblePetParty.IsCampsite||CrucibleContents.IsOpen("XBMStageDetailList")) {
			campsitePendingId=0;
			if(campsiteWasOpen) {
				CrucibleFeedFeedback.Shared.ResetCampsiteCapacity();
				campsiteWasOpen=false;
			}
			return false;
		}
		campsiteWasOpen=true;
		List<CrucibleFamiliar> list=WithLiveHp(CruciblePetParty.Familiars());
		if(list.Count==0) {
			nextActionUtc=now.AddSeconds(1.0);
			Status="Campsite team could not be read - "+CruciblePetParty.LastResult;
			return true;
		}
		if(campsitePendingId!=0) {
			CrucibleFamiliar crucibleFamiliar=list.FirstOrDefault((CrucibleFamiliar f) => f.CatalogueId==campsitePendingId);
			if(!(crucibleFamiliar!=null)||crucibleFamiliar.RestSelected!=campsitePendingSelected) {
				if(now<=campsitePendingUntilUtc) {
					Status="Waiting for the campsite familiar selection to update.";
					return true;
				}
				nextActionUtc=now.AddSeconds(3.0);
				Status=$"Campsite: clicked {crucibleFamiliar?.Name ?? "a familiar"} but the window never showed it {(campsitePendingSelected?"ticked":"unticked")} - {CruciblePetParty.LastResult}";
				campsitePendingId=0;
				return true;
			}
			campsitePendingId=0;
		}
		int campsiteCapacity=CrucibleFeedFeedback.Shared.CampsiteCapacity;
		HashSet<int> desired=(from f in CrucibleTeamPlanner.RestSuggestion(list,campsiteCapacity)
			select f.CatalogueId).ToHashSet();
		CrucibleFamiliar crucibleFamiliar2=list.FirstOrDefault((CrucibleFamiliar f) => f.RestSelected&&!desired.Contains(f.CatalogueId)) ?? list.FirstOrDefault((CrucibleFamiliar f) => !f.RestSelected&&desired.Contains(f.CatalogueId));
		if(crucibleFamiliar2!=null) {
			bool flag=!crucibleFamiliar2.RestSelected;
			if(!CruciblePetParty.GiveTo(crucibleFamiliar2,out string what)) {
				nextActionUtc=now.AddSeconds(2.0);
				Status=$"Could not {(flag?"select":"unselect")} {crucibleFamiliar2.Name} at the campsite - {what}";
				return true;
			}
			campsitePendingId=crucibleFamiliar2.CatalogueId;
			campsitePendingSelected=flag;
			campsitePendingUntilUtc=now.AddSeconds(3.0);
			nextActionUtc=now.AddMilliseconds(500.0);
			Status=$"Campsite: {(flag?"selecting":"unselecting")} {crucibleFamiliar2.Name} ({crucibleFamiliar2.HP}/{crucibleFamiliar2.MaxHP} HP, {(float)crucibleFamiliar2.HP*100f/(float)crucibleFamiliar2.MaxHP:0}%).";
			return true;
		}
		CrucibleFamiliar[] array=list.Where((CrucibleFamiliar f) => f.RestSelected).ToArray();
		if(CruciblePetParty.Rest(out string what2)) {
			ExpectConfirm("resting at the campsite",now);
			nextActionUtc=now.AddMilliseconds(1200.0);
			string text=((campsiteCapacity<3)?$" This campsite accommodates {campsiteCapacity}.":string.Empty);
			Status=((array.Length==0)?("Resting with no familiar ticked - none is missing HP, so only you are healed ("+what2+")"+text):("Resting "+string.Join(", ",array.Select((CrucibleFamiliar f) => $"{f.Name} ({f.HP}/{f.MaxHP} HP)"))+" - "+what2+text));
			return true;
		}
		nextActionUtc=now.AddSeconds(2.0);
		Status="Campsite familiars are ticked, but Rest could not be pressed - "+what2;
		return true;
	}
	private static List<CrucibleFamiliar> WithLiveHp(List<CrucibleFamiliar> familiars) {
		IBattleNpc summoned=Familiar();
		if(summoned==null||summoned.MaxHp==0) {
			return familiars;
		}
		string name=summoned.Name.TextValue;
		return familiars.Select((CrucibleFamiliar f) => (!string.Equals(f.Name,name,StringComparison.OrdinalIgnoreCase))?f:f with {
			HP = Math.Min(summoned.CurrentHp,f.MaxHP)
		}).ToList();
	}
	private bool FamiliarIsTanking() {
		if(!CrucibleEnmityPlan.Applies(configuration)) {
			return RotationToggleOverrides.Get("KeepCovered",configuration.RotationKeepCovered);
		}
		return BeastmasterDutyActions.IsCovered();
	}
	private bool GiveConsumable(CrucibleItem item,List<CrucibleFamiliar> familiars,DateTime now) {
		bool flag=FamiliarIsTanking();
		if(!flag&&CruciblePetParty.OffersSelf()&&CruciblePetParty.GiveToSelf(out string what)) {
			feedAskedSinceUtc=DateTime.MinValue;
			ExpectConfirm("using "+item.Name+" on yourself",now);
			nextActionUtc=now.AddMilliseconds(1200.0);
			Status="Using "+item.Name+" on yourself - Keep Covered is off, so you are holding enmity. "+what;
			return true;
		}
		CrucibleFamiliar crucibleFamiliar=CrucibleItemChoice.ConsumableTarget(familiars);
		if(crucibleFamiliar==null) {
			feedAskedSinceUtc=DateTime.MinValue;
			nextActionUtc=now.AddSeconds(2.0);
			Status=(CrucibleContents.Dismiss("XBMPetParty",out string what2)?("No familiar could take "+item.Name+" - closed the window with "+what2):("No familiar could take "+item.Name+" and the window will not close: "+what2));
			return true;
		}
		if(CruciblePetParty.GiveTo(crucibleFamiliar,out string what3)) {
			feedAskedSinceUtc=DateTime.MinValue;
			ExpectConfirm("using "+item.Name+" on "+crucibleFamiliar.Name,now);
			nextActionUtc=now.AddMilliseconds(1200.0);
			string value=(flag?"Keep Covered is on, so the familiar is holding enmity":"the window offered no self row, so the familiar takes it");
			Status=$"Using {item.Name} on {crucibleFamiliar.Name} - {value}";
			return true;
		}
		nextActionUtc=now.AddSeconds(2.0);
		Status=$"Wanted to use {item.Name} on {crucibleFamiliar.Name} but {what3}";
		return true;
	}
	private static bool TakesAnItemSlot(CrucibleItem? item) {
		if(item!=null&&item.Kind!=CrucibleItemKind.BeastGear) {
			return item.Kind!=CrucibleItemKind.Feed;
		}
		return false;
	}
	private HashSet<int> HeldGearIds() {
		HashSet<int> hashSet=new HashSet<int>(gearTakenThisBoard);
		foreach(CrucibleItem item in Gear) {
			hashSet.Add(item.Id);
		}
		foreach(CrucibleItem item2 in Held) {
			if(item2.Kind==CrucibleItemKind.BeastGear) {
				hashSet.Add(item2.Id);
			}
		}
		return hashSet;
	}
	private void ExpectFeedTarget(CrucibleItem item,DateTime now) {
		feedItemName=item.Name;
	}
	private bool HandleTome(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsBlinkTome,CrucibleStatuses.Blink,"five stacks of Blink",ref tomeState);
	}
	private bool HandleReflect(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsReflectTome,CrucibleStatuses.Reflect,"Reflect",ref reflectState);
	}
	private bool HandleAutoPotion(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsAutoPotionKit,CrucibleStatuses.AutoPotion,"Auto-potion",ref autoPotionState,() => PlayerHpPercent()<configuration.CrucibleAutoPotionHpPercent,onPlayer: true);
	}
	private bool HandleMerchantsEye(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsMerchantsEye,CrucibleStatuses.TokenBenefits,"doubled territory tokens",ref merchantsEyeState);
	}
	private bool HandleThiefsEye(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsThiefsEye,CrucibleStatuses.WinnersKeepers,"tripled loot",ref thiefsEyeState,null,onPlayer: true);
	}
	private bool HandleVampiric(DateTime now) {
		return HandleFightBuff(now,(CrucibleItem item) => item.IsVampiricEssence,CrucibleStatuses.LifeSiphon,"Life Siphon",ref essenceState,null,onPlayer: true);
	}
	private bool HandleFightBuff(DateTime now,Func<CrucibleItem,bool> matches,IReadOnlyList<uint> statusIds,string what,ref FightBuffState state,Func<bool>? onlyWhen=null,bool onPlayer=false) {
		if(!configuration.CrucibleUseTomeInFight) {
			state.Note="switched off";
			return false;
		}
		if(!InLiveFight) {
			state.Note="not in combat";
			return false;
		}
		if(state.Confirmed) {
			return false;
		}
		if(BeastmasterStatusReader.EitherHasAny(statusIds)) {
			state.Confirmed=true;
			state.Note=what+" up on "+(BeastmasterStatusReader.HasAny(statusIds)?"you":"the familiar");
			return false;
		}
		if(onlyWhen!=null&&!onlyWhen()) {
			state.Note="held until it is worth using";
			return false;
		}
		if(now<state.RetryAfterUtc) {
			return false;
		}
		if(state.Attempts>=3) {
			state.Note=$"gave up after {state.Attempts} attempts";
			return false;
		}
		CrucibleItem crucibleItem=Held.FirstOrDefault((CrucibleItem held) => matches(held));
		if(crucibleItem==null) {
			if(!CrucibleContents.HeldItemsReadable) {
				state.Note="carried items unreadable";
				state.Note="carried-items HUD is not open";
				return false;
			}
			state.Note="none held";
			state.Note="none held";
			return false;
		}
		IGameObject gameObject;
		if(!FamiliarIsTanking()||onPlayer) {
			IGameObject localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			gameObject=localPlayer;
		}
		else {
			IGameObject localPlayer=Familiar();
			gameObject=localPlayer;
		}
		IGameObject recipient=gameObject;
		if(!CrucibleContents.UseHeldItemOn(crucibleItem,recipient)) {
			state.Attempts++;
			state.RetryAfterUtc=now.AddSeconds(3.0);
			state.Note="use refused";
			state.Note=$"{CrucibleContents.LastUsePath} (try {state.Attempts}/3)";
			nextActionUtc=now.AddSeconds(1.0);
			return true;
		}
		state.Attempts++;
		state.RetryAfterUtc=now.AddSeconds(4.0);
		state.Note="used, waiting for the status";
		nextActionUtc=now.AddMilliseconds(1500.0);
		string value=((FamiliarIsTanking()&&!onPlayer)?"the familiar taking the hits":"you");
		state.Note=$"used on {value}, waiting for {what} (try {state.Attempts}/3)";
		Status=$"Used {crucibleItem.Name} for {what} on {value}";
		return true;
	}
	private static string Describe(FightBuffState state) {
		if(state.Note.Length!=0) {
			return state.Note;
		}
		return "not evaluated";
	}
	private bool HandleFeather(DateTime now) {
		if(!DalamudApi.Condition[ConditionFlag.InCombat]) {
			FeatherStatus="not in combat";
			return false;
		}
		CrucibleItem crucibleItem=Held.FirstOrDefault((CrucibleItem item) => item.IsCrucibleFeather);
		if(crucibleItem==null) {
			FeatherStatus=(CrucibleContents.HeldItemsReadable?"none held":"carried items unreadable");
			return false;
		}
		if(now<featherRetryAfterUtc) {
			return false;
		}
		BeastmasterGaugeSnapshot beastmasterGaugeSnapshot=BeastmasterGaugeSnapshot.Read();
		int num=150;
		if(beastmasterGaugeSnapshot.Tp>num) {
			FeatherStatus=$"held at {beastmasterGaugeSnapshot.Tp}/{250} Tenacity - above {num}, so part of the {100} would be wasted";
			return false;
		}
		if(!CrucibleContents.UseHeldItemOn(crucibleItem,DalamudApi.ObjectTable.LocalPlayer)) {
			featherRetryAfterUtc=now.AddSeconds(3.0);
			FeatherStatus="use refused - "+CrucibleContents.LastUsePath;
			return false;
		}
		featherRetryAfterUtc=now.AddSeconds(3.0);
		nextActionUtc=now.AddMilliseconds(1500.0);
		FeatherStatus=$"used at {beastmasterGaugeSnapshot.Tp}/{250} Tenacity";
		Status=$"Used {crucibleItem.Name} at {beastmasterGaugeSnapshot.Tp}/{250} Tenacity - the whole {100} fits";
		return true;
	}
	private bool HandleFang(DateTime now) {
		if(!DalamudApi.Condition[ConditionFlag.InCombat]||DalamudApi.TargetManager.Target==null) {
			return false;
		}
		CrucibleItem crucibleItem=Held.Where((CrucibleItem item) => item.IsFang||item.IsCelestialSand).OrderBy(Priority).ThenByDescending((CrucibleItem item) => item.Value)
			.FirstOrDefault();
		if(crucibleItem==null||!CrucibleContents.UseHeldItem(crucibleItem)) {
			return false;
		}
		nextActionUtc=now.AddMilliseconds(1500.0);
		Status="Used "+crucibleItem.Name+" on the current enemy.";
		return true;
		static int Priority(CrucibleItem item) {
			return item.Id switch {
				139 => 0,
				129 => 1,
				128 => 2,
				134 => 3,
				_ => 4,
			};
		}
	}
	private bool HandleHealing(DateTime now) {
		if(!configuration.CrucibleAutoHeal||Held.Count==0) {
			return false;
		}
		bool flag=DalamudApi.Condition[ConditionFlag.InCombat];
		float num=FamiliarHpPercent();
		bool flag2=num>=0f;
		bool ashesUsable=flag&flag2;
		CrucibleItemChoice.CrucibleHealChoice crucibleHealChoice=CrucibleItemChoice.HealEither(from i in Held
			where i.Kind!=CrucibleItemKind.BeastGear
			where ashesUsable||!i.HealsArea
			select i,PlayerHpPercent(),ashesUsable?num:(-1f),(flag&flag2)?configuration.CruciblePlayerPotionReserve:int.MaxValue);
		if(!crucibleHealChoice.HasItem) {
			Status=crucibleHealChoice.Reason;
			return false;
		}
		object obj;
		if(!crucibleHealChoice.ForFamiliar) {
			obj=null;
		}
		else {
			IGameObject gameObject=Familiar();
			obj=gameObject;
		}
		IGameObject recipient=(IGameObject)obj;
		if(CrucibleContents.UseHeldItemOn(crucibleHealChoice.Item,recipient)) {
			nextActionUtc=now.AddMilliseconds(1500.0);
			Status=$"Used {crucibleHealChoice.Item.Name} through its direct Crucible hotbar slot: {crucibleHealChoice.Reason}.";
			return true;
		}
		nextActionUtc=now.AddSeconds(2.0);
		Status="Wanted "+crucibleHealChoice.Item.Name+" but could not find its occupied Crucible hotbar slot";
		return false;
	}
	private void TrackPlatformEvent(DateTime now) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return;
		}
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		CruciblePlatform cruciblePlatform=(CrucibleBoardMap.HasMap(territoryType)?CrucibleBoardMap.Current(territoryType,localPlayer.Position):null);
		if(territoryType!=lastTerritory) {
			lastTerritory=territoryType;
			awaitingBoardReturn=true;
			Paused=false;
			CrucibleFeedFeedback.Shared.Reset();
			CruciblePetParty.Forget();
			fightsSinceRest=0;
			ungiveableThisBoard.Clear();
			gearTakenThisBoard.Clear();
		}
		if(DalamudApi.Condition[ConditionFlag.InCombat]||DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]||DalamudApi.Condition[ConditionFlag.WatchingCutscene]||DalamudApi.Condition[ConditionFlag.WatchingCutscene78]) {
			awaitingBoardReturn=true;
		}
		else if(cruciblePlatform!=null) {
			awaitingBoardReturn=false;
			lastOnPlatformUtc=now;
		}
		string text=cruciblePlatform?.Label ?? string.Empty;
		if(text.Length==0) {
			return;
		}
		if(text!=platformLabel) {
			if(cruciblePlatform.Kind==CruciblePlatformKind.Campsite) {
				fightsSinceRest=0;
			}
			else {
				CruciblePlatformKind kind=cruciblePlatform.Kind;
				if((kind==CruciblePlatformKind.Fight||kind==CruciblePlatformKind.FinalFight)?true:false) {
					fightsSinceRest++;
				}
			}
			platformLabel=text;
			platformArrivedUtc=now;
			platformEventSeen=false;
			platformEventDone=false;
			platformEventEndedUtc=DateTime.MinValue;
			return;
		}
		if(!DalamudApi.Condition[ConditionFlag.InCombat]) {
			tomeState=default;
			essenceState=default;
			reflectState=default;
			autoPotionState=default;
			merchantsEyeState=default;
			thiefsEyeState=default;
			featherRetryAfterUtc=DateTime.MinValue;
		}
		if(DalamudApi.Condition[ConditionFlag.InCombat]||CrucibleContents.AnyWindowOpen||CrucibleUi.YesNoOpen) {
			platformEventSeen=true;
			platformEventDone=false;
		}
		else if(platformEventSeen&&!platformEventDone) {
			platformEventDone=true;
			platformEventEndedUtc=now;
		}
	}
	private bool PlatformFinished(DateTime now,CruciblePlatform here) {
		if(here.Kind==CruciblePlatformKind.Entrance) {
			return true;
		}
		if(platformEventDone) {
			return (now-platformEventEndedUtc).TotalSeconds>=2.0;
		}
		if(platformEventSeen) {
			return false;
		}
		int num=Math.Max(3,configuration.CrucibleEventGraceSeconds);
		return (now-platformArrivedUtc).TotalSeconds>=(double)num;
	}
	private string? AdvanceBlocker() {
		if(!configuration.CrucibleBoardAssist||!configuration.CrucibleAutoAdvance) {
			return "advance switched off";
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return "no character";
		}
		if(DalamudApi.ClientState.TerritoryType!=advanceTerritory) {
			return "the zone changed";
		}
		if(DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]||DalamudApi.Condition[ConditionFlag.WatchingCutscene]||DalamudApi.Condition[ConditionFlag.WatchingCutscene78]) {
			return "a transition or cutscene started";
		}
		if(DalamudApi.Condition[ConditionFlag.InCombat]) {
			return "combat started";
		}
		if(CrucibleUi.YesNoOpen||CrucibleContents.AnyWindowOpen) {
			return "a window opened";
		}
		if(awaitingBoardReturn) {
			return "no longer on the mapped board";
		}
		if(advanceTarget!=default(Vector3)&&Vector2.Distance(new Vector2(localPlayer.Position.X,localPlayer.Position.Z),new Vector2(advanceTarget.X,advanceTarget.Z))<=1f) {
			return "arrived at the centre";
		}
		if((DateTime.UtcNow-advanceStartedUtc).TotalSeconds>60.0) {
			return "the walk took too long";
		}
		return null;
	}
	private void StopAdvance(string reason) {
		try {
			navigation.Stop();
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not stop navigation.");
		}
		advancing=false;
		advanceTarget=default;
		advanceStartedUtc=DateTime.MinValue;
		Status="Stopped walking - "+reason;
	}
	private void HandleAdvance(DateTime now) {
		if(!configuration.CrucibleAutoAdvance) {
			return;
		}
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null||DalamudApi.Condition[ConditionFlag.InCombat]||CrucibleUi.YesNoOpen||CrucibleContents.AnyWindowOpen||DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]||DalamudApi.Condition[ConditionFlag.WatchingCutscene]||DalamudApi.Condition[ConditionFlag.WatchingCutscene78]) {
			return;
		}
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		if(!CrucibleBoardMap.HasMap(territoryType)) {
			Status="No surveyed map for this board - not advancing";
		}
		else if(!navigation.IsVnavmeshInstalled) {
			Status="Advancing needs vnavmesh, which is not loaded";
		}
		else if(awaitingBoardReturn) {
			Status="Waiting to be back on a mapped platform before moving again";
		}
		else {
			if(advancing) {
				return;
			}
			CruciblePlatform cruciblePlatform=CrucibleBoardMap.Current(territoryType,localPlayer.Position);
			if(cruciblePlatform==null) {
				CruciblePlatform cruciblePlatform2=CrucibleBoardMap.NearestTo(territoryType,localPlayer.Position);
				if(cruciblePlatform2==null||CrucibleBoardMap.DistanceTo(cruciblePlatform2,localPlayer.Position)>30f) {
					Status="Not on a mapped platform - waiting rather than guessing a direction";
				}
				else {
					StartAdvance(now,cruciblePlatform2,territoryType,"Returning to "+cruciblePlatform2.Label);
				}
				return;
			}
			if(!PlatformFinished(now,cruciblePlatform)) {
				string text=CrucibleContents.DescribeBlockingWindows();
				string text2=((text.Length==0)?string.Empty:(" (also open: "+text+")"));
				Status=(platformEventSeen?("Waiting for "+cruciblePlatform.Label+" to finish"+text2):("Waiting for "+cruciblePlatform.Label+" to start"+text2));
				return;
			}
			CruciblePlatform cruciblePlatform3=CrucibleBoardMap.Next(territoryType,localPlayer.Position,NeedsHealing());
			if(cruciblePlatform3==null) {
				Status="At the end of the board";
			}
			else if(!cruciblePlatform3.AtCentre(localPlayer.Position)) {
				StartAdvance(now,cruciblePlatform3,territoryType,"Walking to "+cruciblePlatform3.Label);
			}
		}
	}
	private void StartAdvance(DateTime now,CruciblePlatform target,uint territory,string what) {
		if(navigation.MoveTo(target.Centre)) {
			advancing=true;
			advanceTarget=target.Centre;
			advanceTerritory=territory;
			advanceStartedUtc=now;
			Status=what;
		}
		else {
			nextActionUtc=now.AddSeconds(3.0);
			Status="Could not start walking to "+target.Label;
		}
	}
	private bool HandlePartyCheck(DateTime now) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		bool flag=localPlayer==null||DalamudApi.Condition[ConditionFlag.InCombat]||DalamudApi.Condition[ConditionFlag.BetweenAreas]||DalamudApi.Condition[ConditionFlag.BetweenAreas51]||DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent]||DalamudApi.Condition[ConditionFlag.WatchingCutscene]||DalamudApi.Condition[ConditionFlag.WatchingCutscene78];
		if(partyCheckStage!=0&&(flag||!configuration.CrucibleCheckTeamBeforeFork||Paused)) {
			CrucibleContents.CloseBoardLayout();
			CruciblePreparationService.SuppressUntilUtc=now.AddSeconds(3.0);
			partyCheckStage=0;
			return false;
		}
		if(flag||!configuration.CrucibleCheckTeamBeforeFork||!configuration.CrucibleCampsiteWhenHurt||!configuration.CrucibleAutoAdvance||Paused||advancing||awaitingBoardReturn) {
			return false;
		}
		uint territoryType=DalamudApi.ClientState.TerritoryType;
		if(!CrucibleBoardMap.HasMap(territoryType)) {
			return false;
		}
		CruciblePlatform cruciblePlatform=CrucibleBoardMap.Current(territoryType,localPlayer.Position);
		if(cruciblePlatform==null) {
			return false;
		}
		string text=$"{cruciblePlatform.Label}@{platformArrivedUtc.Ticks}";
		if(text==partyCheckKey) {
			return false;
		}
		if(partyCheckStage==0) {
			if(CrucibleUi.YesNoOpen||CrucibleContents.AnyWindowOpen||!PlatformFinished(now,cruciblePlatform)||!CrucibleBoardMap.OffersCampsite(territoryType,localPlayer.Position)) {
				return false;
			}
			if(partyCheckAttempts>=3) {
				partyCheckKey=text;
				partyCheckAttempts=0;
				Status="Could not open Board Layout to check the team - deciding on the last known HP";
				return false;
			}
			partyCheckAttempts++;
			CruciblePreparationService.SuppressUntilUtc=now.AddSeconds(5.0);
			if(CrucibleContents.OpenBoardLayout()) {
				partyCheckStage=1;
				partyCheckUntil=now.AddSeconds(4.0);
				nextActionUtc=now.AddMilliseconds(600.0);
				Status="Checking the team's HP before leaving "+cruciblePlatform.Label+" (Board Layout)";
				return true;
			}
			nextActionUtc=now.AddSeconds(1.0);
			Status="Board Layout button could not be pressed";
			return true;
		}
		CruciblePreparationService.SuppressUntilUtc=now.AddSeconds(4.0);
		if(partyCheckStage==1) {
			if(CruciblePetParty.IsOpen&&CruciblePetParty.Familiars().Count>0) {
				partyCheckStage=2;
				partyCheckAttempts=0;
				nextActionUtc=now.AddMilliseconds(300.0);
				Status=$"Read the team: {CruciblePetParty.LastKnown.Count} familiar(s). Closing Board Layout.";
				return true;
			}
			if(now>partyCheckUntil) {
				partyCheckStage=0;
				CrucibleContents.CloseBoardLayout();
			}
			return true;
		}
		if(!CrucibleContents.CloseBoardLayout()) {
			partyCheckKey=text;
			partyCheckStage=0;
			CruciblePreparationService.SuppressUntilUtc=now.AddSeconds(3.0);
			return false;
		}
		nextActionUtc=now.AddMilliseconds(500.0);
		return true;
	}
	private bool NeedsHealing() {
		if(!configuration.CrucibleCampsiteWhenHurt) {
			BranchPlan="Campsite branching is off - always taking the treasure";
			return false;
		}
		float num=PlayerHpPercent();
		float crucibleCampsiteHpPercent=configuration.CrucibleCampsiteHpPercent;
		if(num<crucibleCampsiteHpPercent) {
			BranchPlan=$"Campsite - you are at {num:0}%, below {crucibleCampsiteHpPercent:0}%";
			return true;
		}
		List<(string,float)> list=RosterHp();
		float num2=(from m in list.Select(((string Name,float Percent) f) => 100f-f.Percent)
			where m>0f
			orderby m descending
			select m).Take(3).Sum();
		float num3=Math.Max(0f,100f-num)+num2;
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		bool flag=localPlayer!=null&&CrucibleBoardMap.CampsiteLeadsToFinal(DalamudApi.ClientState.TerritoryType,localPlayer.Position);
		int num4=(flag?Math.Max(30,configuration.CrucibleCampsiteTotalMissing/2):configuration.CrucibleCampsiteTotalMissing);
		if(list.Count>0&&num3>=(float)num4) {
			string value=string.Join(", ",list.OrderBy(((string Name,float Percent) f) => f.Percent).Take(3).Select(((string Name,float Percent) f) => $"{f.Name} {f.Percent:0}%"));
			BranchPlan=$"Campsite - a rest could restore {num3:0} points of HP (you {num:0}%, {value}), over the {num4} needed{(flag?" (halved: this is the last campsite before the final fight)":string.Empty)}";
			return true;
		}
		float teamBar=configuration.CrucibleCampsiteFamiliarHpPercent;
		int crucibleCampsiteHurtFamiliars=configuration.CrucibleCampsiteHurtFamiliars;
		List<(string,float)> list2=FightingTeamHp();
		if(list2.Count>0) {
			int num5=Enumerable.Count(list2,((string Name,float Percent) f) => f.Percent<teamBar);
			string value2=string.Join(", ",list2.Select(((string Name,float Percent) f) => $"{f.Name} {f.Percent:0}%"));
			if(num5>=crucibleCampsiteHurtFamiliars) {
				BranchPlan=$"Campsite - {num5} of {list2.Count} familiars below {teamBar:0}% ({value2})";
				return true;
			}
			if(list2.Count<crucibleCampsiteHurtFamiliars&&list2.Count>0&&num5==list2.Count&&fightsSinceRest>=2) {
				BranchPlan=$"Campsite - every readable familiar is below {teamBar:0}% ({value2}) and {fightsSinceRest} fights "+"have passed since the last rest, so the others are assumed as worn";
				return true;
			}
			List<(string,float)> list3=RosterHp();
			int num6=Enumerable.Count(list3,((string Name,float Percent) f) => f.Percent<teamBar);
			int num7=Math.Max(crucibleCampsiteHurtFamiliars,3);
			if(num6>=num7) {
				string value3=string.Join(", ",list3.Where(((string Name,float Percent) f) => f.Percent<teamBar).OrderBy(((string Name,float Percent) f) => f.Percent).Take(6)
					.Select(((string Name,float Percent) f) => $"{f.Name} {f.Percent:0}%"));
				BranchPlan=$"Campsite - {num6} of {list3.Count} familiars in total are below {teamBar:0}% ({value3})";
				return true;
			}
			string value4=((list2.Count<crucibleCampsiteHurtFamiliars)?$"; only {list2.Count} familiar(s) readable":string.Empty);
			BranchPlan=$"Treasure - you are at {num:0}% of {crucibleCampsiteHpPercent:0}%, {num5} of {list2.Count} familiars below {teamBar:0}%, {crucibleCampsiteHurtFamiliars} needed ({value2}){value4}";
			return false;
		}
		List<(string,float)> list4=RosterHp();
		if(list4.Count>0) {
			int num8=Enumerable.Count(list4,((string Name,float Percent) f) => f.Percent>=teamBar);
			string value5=string.Join(", ",list4.OrderBy(((string Name,float Percent) f) => f.Percent).Take(6).Select(((string Name,float Percent) f) => $"{f.Name} {f.Percent:0}%"));
			if(num8<3) {
				BranchPlan=$"Campsite - only {num8} of {list4.Count} familiars are at or above {teamBar:0}%, so a healthy team of {3} cannot be fielded ({value5})";
				return true;
			}
			BranchPlan=$"Treasure - you are at {num:0}% of {crucibleCampsiteHpPercent:0}% and {num8} of {list4.Count} familiars are at or above {teamBar:0}%, so a healthy team can still be fielded ({value5})";
			return false;
		}
		BranchPlan=$"Treasure - you are at {num:0}% of {crucibleCampsiteHpPercent:0}%; team HP unknown (the team window has not been read this board)";
		return false;
	}
	private static List<(string Name,float Percent)> RosterHp() {
		return (from f in WithLiveHp(CruciblePetParty.LastKnown.Where((CrucibleFamiliar f) => f.MaxHP!=0).ToList())
			select (Name: f.Name,Percent: (float)f.HP*100f/(float)f.MaxHP)).ToList();
	}
	private static List<(string Name,float Percent)> FightingTeamHp() {
		List<(string,float)> list=(from f in WithLiveHp(CruciblePetParty.LastKnown.Where((CrucibleFamiliar f) => f.Slot<3&&f.MaxHP!=0).ToList())
			select (Name: f.Name,Percent: (float)f.HP*100f/(float)f.MaxHP)).ToList();
		if(list.Count>0) {
			return list;
		}
		IBattleNpc battleNpc=Familiar();
		if(battleNpc!=null&&battleNpc.MaxHp!=0) {
			int num=1;
			List<(string,float)> list2=new List<(string,float)>(num);
			CollectionsMarshal.SetCount(list2,num);
			CollectionsMarshal.AsSpan(list2)[0]=(battleNpc.Name.TextValue,(float)battleNpc.CurrentHp*100f/(float)battleNpc.MaxHp);
			return list2;
		}
		return new List<(string,float)>();
	}
	private static IBattleNpc? Familiar() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return null;
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&battleNpc.OwnerId==localPlayer.EntityId&&battleNpc.MaxHp!=0&&battleNpc.EntityId!=localPlayer.EntityId) {
				return battleNpc;
			}
		}
		return null;
	}
	private static float FamiliarHpPercent() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			return -1f;
		}
		foreach(IGameObject item in DalamudApi.ObjectTable) {
			if(item is IBattleNpc battleNpc&&battleNpc.OwnerId==localPlayer.EntityId&&battleNpc.MaxHp!=0&&battleNpc.EntityId!=localPlayer.EntityId) {
				return (float)battleNpc.CurrentHp*100f/(float)battleNpc.MaxHp;
			}
		}
		return -1f;
	}
	private static float PlayerHpPercent() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null||localPlayer.MaxHp==0) {
			return 100f;
		}
		return (float)localPlayer.CurrentHp*100f/(float)localPlayer.MaxHp;
	}
}
