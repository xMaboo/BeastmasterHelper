using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleContents {
	public const string BootyAddon="XBMContentsBooty";
	public const string ItemShopAddon="XBMContentsItemShop";
	public const string TreasureAddon="XBMContentsTreasure";
	public const string MainHudAddon="XBMContentsMainHUD";
	public const string PetPartyAddon="XBMPetParty";
	public const string StageDetailAddon="XBMStageDetailList";
	public const string ResultAddon="XBMResult";
	public const string PresetPreviewAddon="XBMPresetPreview";
	public const string PresetApplyLabel="Apply";
	private static readonly byte[] BoardLayoutClickPayload=new byte[4] { 167, 5, 13, 3 };
	public const int ItemSlotCount=10;
	private static readonly string[] ResultSteps=new string[3] { "Continue", "Leave", "Complete" };
	private static readonly string[] KnownWindows=new string[7] { "XBMContentsBooty", "XBMContentsItemShop", "XBMContentsTreasure", "XBMContentsMainHUD", "XBMPetParty", "XBMStageDetailList", "XBMResult" };
	private static readonly string[] AcknowledgeLabels=new string[7] { "Close", "OK", "Confirm", "Complete", "Continue", "Proceed", "Exit" };
	private static readonly string[] DismissLabels;
	private static readonly string[] PermanentHud;
	public static bool PresetPreviewWaiting => IsOpen("XBMPresetPreview");
	public static bool AnyWindowOpen {
		get {
			if(!IsOpen("XBMContentsBooty")&&!IsOpen("XBMContentsItemShop")&&!IsOpen("XBMContentsTreasure")&&!IsOpen("XBMPetParty")&&!IsOpen("XBMStageDetailList")) {
				return IsOpen("XBMResult");
			}
			return true;
		}
	}
	public static bool BoardFinished => IsOpen("XBMResult");
	public static bool LootWaiting => IsOpen("XBMContentsBooty");
	public static bool ShopWaiting => IsOpen("XBMContentsItemShop");
	public static bool TreasureWaiting => IsOpen("XBMContentsTreasure");
	public static string LastBuyPath { get; private set; }
	public unsafe static bool HeldItemsReadable => Unit("XBMContentsMainHUD")!=null;
	public static string LastUsePath { get; private set; }
	public static bool ApplyPresetTeam() {
		return CrucibleUi.ClickIn("XBMPresetPreview","Apply");
	}
	public unsafe static bool IsOpen(string addon) {
		return Unit(addon)!=null;
	}
	public static bool OpenBoardLayout() {
		return CrucibleClickRecorder.SendButtonClick("XBMContentsMainHUD",0,BoardLayoutClickPayload);
	}
	public unsafe static bool CloseBoardLayout() {
		bool result=false;
		string[] array=new string[2] { "XBMPetParty", "XBMStageDetailList" };
		for(int i=0;i<array.Length;i++) {
			AtkUnitBase* ptr=Unit(array[i]);
			if(ptr!=null) {
				ptr->Close(fireCallback: true);
				result=true;
			}
		}
		return result;
	}
	public unsafe static List<CrucibleItem> GearIn(string addonName) {
		List<CrucibleItem> list=new List<CrucibleItem>();
		if(!string.Equals(addonName,"XBMContentsMainHUD",StringComparison.Ordinal)) {
			return list;
		}
		AtkUnitBase* ptr=Unit("XBMContentsMainHUD");
		if(ptr==null||ptr->AtkValues==null) {
			return list;
		}
		int num=0;
		while(true) {
			int num2=60+num*5;
			int num3=63+num*5;
			if(num3>=ptr->AtkValuesCount) {
				break;
			}
			AtkValue atkValue=ptr->AtkValues[num2];
			if((atkValue.Type&AtkValueType.TypeMask)!=AtkValueType.Bool||!atkValue.Bool) {
				break;
			}
			AtkValue atkValue2=ptr->AtkValues[num3];
			AtkValueType atkValueType=atkValue2.Type&AtkValueType.TypeMask;
			uint num4=((atkValueType==AtkValueType.UInt)?atkValue2.UInt:((atkValueType==AtkValueType.Int&&atkValue2.Int>0)?((uint)atkValue2.Int):0u));
			CrucibleItem crucibleItem=((num4==0)?null:CrucibleItemCatalogue.Get((int)num4));
			if(crucibleItem!=null) {
				list.Add(crucibleItem);
			}
			num++;
		}
		return list;
	}
	public unsafe static int OccupiedItemSlots() {
		AtkUnitBase* ptr=Unit("XBMContentsMainHUD");
		if(ptr==null||ptr->AtkValues==null) {
			return -1;
		}
		int num=0;
		for(int i=0;i<10;i++) {
			int num2=12+i*5;
			if(num2>=ptr->AtkValuesCount) {
				return -1;
			}
			AtkValue atkValue=ptr->AtkValues[num2];
			AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
			int num3;
			if(atkValueType!=AtkValueType.UInt) {
				if(atkValueType!=AtkValueType.Int||atkValue.Int<=0) {
					continue;
				}
				num3=atkValue.Int;
			}
			else {
				num3=(int)atkValue.UInt;
			}
			if(num3!=0) {
				num++;
			}
		}
		return num;
	}
	public static int FreeItemSlots() {
		int num=OccupiedItemSlots();
		if(num>=0) {
			return Math.Max(0,10-num);
		}
		return -1;
	}
	private unsafe static AtkUnitBase* Unit(string name) {
		try {
			AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name).Address;
			return (address!=null&&address->IsVisible&&address->IsReady)?address:null;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not reach addon {Addon}.",name);
			return null;
		}
	}
	public unsafe static List<string> VisibleText(string addonName) {
		List<string> list=new List<string>();
		AtkUnitBase* ptr=Unit(addonName);
		if(ptr==null) {
			return list;
		}
		try {
			Collect(&ptr->UldManager,list,0);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the text of addon {Addon}.",addonName);
		}
		return list;
	}
	private unsafe static void Collect(AtkUldManager* manager,List<string> texts,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()) {
				continue;
			}
			if(ptr->Type==NodeType.Text) {
				string text=((AtkTextNode*)ptr)->NodeText.ToString();
				if(text.Length>0) {
					texts.Add(text);
				}
			}
			AtkComponentBase* component=ptr->GetComponent();
			if(component==null) {
				continue;
			}
			AtkComponentButton* asAtkComponentButton=ptr->GetAsAtkComponentButton();
			if(asAtkComponentButton!=null&&asAtkComponentButton->ButtonTextNode!=null) {
				string text2=asAtkComponentButton->ButtonTextNode->NodeText.ToString();
				if(text2.Length>0) {
					texts.Add(text2);
				}
			}
			Collect(&component->UldManager,texts,depth+1);
		}
	}
	public unsafe static List<CrucibleItem> ItemsIn(string addonName) {
		List<CrucibleItem> list=new List<CrucibleItem>();
		HashSet<int> hashSet=new HashSet<int>();
		if(string.Equals(addonName,"XBMContentsMainHUD",StringComparison.Ordinal)) {
			AtkUnitBase* ptr=Unit("XBMContentsMainHUD");
			if(ptr!=null&&ptr->AtkValues!=null) {
				for(int i=0;i<10;i++) {
					int num=12+i*5;
					if(num>=ptr->AtkValuesCount) {
						break;
					}
					AtkValue atkValue=ptr->AtkValues[num];
					AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
					uint num2=((atkValueType==AtkValueType.UInt)?atkValue.UInt:((atkValueType==AtkValueType.Int&&atkValue.Int>0)?((uint)atkValue.Int):0u));
					CrucibleItem crucibleItem=((num2==0)?null:CrucibleItemCatalogue.Get((int)num2));
					if(crucibleItem!=null) {
						list.Add(crucibleItem);
						hashSet.Add(crucibleItem.Id);
					}
				}
			}
		}
		List<string> list2=VisibleText(addonName);
		list2.AddRange(CrucibleListClick.RowTexts(addonName));
		foreach(string item in list2) {
			CrucibleItem crucibleItem2=CrucibleItemCatalogue.ByName(item);
			if(crucibleItem2!=null&&hashSet.Add(crucibleItem2.Id)) {
				list.Add(crucibleItem2);
			}
		}
		return list;
	}
	public static bool TakeLootItem(CrucibleItem item,out string how) {
		if(!IsOpen("XBMContentsBooty")) {
			how="Loot window closed";
			return false;
		}
		CrucibleTreasureOffer crucibleTreasureOffer=CrucibleTreasureReader.Offers("XBMContentsBooty").FirstOrDefault((CrucibleTreasureOffer o) => o.Item.Id==item.Id&&!o.Acquired);
		if(crucibleTreasureOffer==null) {
			how="no unclaimed card for "+item.Name+" - "+CrucibleTreasureReader.LastResult;
			return false;
		}
		if(CrucibleTreasureReader.Take("XBMContentsBooty",crucibleTreasureOffer)) {
			how=$"clicked the {item.Name} card (slot {crucibleTreasureOffer.Param})";
			return true;
		}
		how="the "+item.Name+" card refused the click - "+CrucibleTreasureReader.LastResult;
		return false;
	}
	public static bool LeaveBoard(out string what) {
		string[] resultSteps=ResultSteps;
		foreach(string text in resultSteps) {
			if(CrucibleUi.ClickIn("XBMResult",text)) {
				what="pressed "+text;
				return true;
			}
		}
		List<string> list=CrucibleUi.ButtonCaptions("XBMResult");
		what=((list.Count==0)?"the results window offers no button that can be pressed yet":("the results window offers "+string.Join(", ",list)));
		return false;
	}
	public static bool BuyFromShop(CrucibleShopOffer offer) {
		CrucibleItem item=offer.Item;
		if(!IsOpen("XBMContentsItemShop")) {
			return false;
		}
		if(CrucibleListClick.RowByIndex("XBMContentsItemShop",offer.Row,item.Name,out string result)) {
			LastBuyPath=result;
			return true;
		}
		if(CrucibleListClick.Row("XBMContentsItemShop",item.Name,out string result2)) {
			LastBuyPath=result+"; then "+result2;
			return true;
		}
		CrucibleRecordedClick lastSelection=CrucibleClickRecorder.Shared.LastSelection;
		LastBuyPath=((lastSelection==null)?(result+"; "+result2+"; no recorded click to fall back on"):(result+"; "+result2+"; replaying the recorded click"));
		if(lastSelection!=null&&string.Equals(lastSelection.Addon,"XBMContentsItemShop",StringComparison.Ordinal)&&CrucibleClickRecorder.Shared.Replay(lastSelection,item.Name)) {
			return true;
		}
		if(!CrucibleUi.ClickRowContaining("XBMContentsItemShop",item.Name)&&!CrucibleUi.Click(item.Name)) {
			return CrucibleUi.Click(item.Name,lists: true);
		}
		return true;
	}
	public static string LeftoverWindow() {
		foreach(string item in CrucibleUi.VisibleXbmWindows()) {
			if(!KnownWindows.Contains(item,StringComparer.OrdinalIgnoreCase)&&CrucibleUi.ButtonCaptions(item).Any((string caption) => AcknowledgeLabels.Contains(caption,StringComparer.OrdinalIgnoreCase))) {
				return item;
			}
		}
		return string.Empty;
	}
	public static string DescribeBlockingWindows() {
		List<string> list=(from name in CrucibleUi.VisibleWindows().Where(IsCandidateBlocker)
			select name+"["+string.Join("/",CrucibleUi.ButtonCaptions(name))+"]").ToList();
		if(list.Count!=0) {
			return string.Join(", ",list);
		}
		return string.Empty;
	}
	private static bool IsCandidateBlocker(string name) {
		if(name.StartsWith('_')||KnownWindows.Contains(name,StringComparer.OrdinalIgnoreCase)) {
			return false;
		}
		if(!PermanentHud.Contains(name,StringComparer.OrdinalIgnoreCase)) {
			return !name.StartsWith("ChatLog",StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
	public static bool DismissLeftover(out string what) {
		string text=LeftoverWindow();
		if(text.Length==0) {
			what=string.Empty;
			return false;
		}
		return Dismiss(text,out what);
	}
	public static bool Dismiss(string window,out string what) {
		string[] dismissLabels=DismissLabels;
		foreach(string text in dismissLabels) {
			if(CrucibleUi.ClickIn(window,text)) {
				what=text+" on "+window;
				return true;
			}
		}
		List<string> list=CrucibleUi.ButtonCaptions(window);
		what=((list.Count==0)?(window+" (no button in it could be pressed)"):(window+" (no button in it could be pressed; it offers "+string.Join(", ",list)+")"));
		return false;
	}
	public static bool UseHeldItemOn(CrucibleItem item,IGameObject? recipient) {
		if(recipient==null) {
			return UseHeldItem(item);
		}
		IGameObject target=DalamudApi.TargetManager.Target;
		try {
			DalamudApi.TargetManager.Target=recipient;
			bool flag=UseHeldItem(item);
			LastUsePath=(flag?(LastUsePath+", aimed at "+recipient.Name.TextValue):(LastUsePath+" (aimed at "+recipient.Name.TextValue+")"));
			return flag;
		}
		catch(Exception ex) {
			DalamudApi.Log.Warning(ex,"Could not aim a Crucible item at {Name}.",recipient.Name.TextValue);
			LastUsePath=$"could not aim {item.Name} at {recipient.Name.TextValue}: {ex.Message}";
			return false;
		}
		finally {
			DalamudApi.TargetManager.Target=target;
		}
	}
	public unsafe static bool UseHeldItem(CrucibleItem item) {
		if(item.Kind==CrucibleItemKind.BeastGear) {
			LastUsePath=item.Name+" is beast gear and is not used from a slot";
			return false;
		}
		AtkUnitBase* ptr=Unit("XBMContentsMainHUD");
		if(ptr==null||ptr->AtkValues==null) {
			LastUsePath="XBMContentsMainHUD is not open, so no carried item can be located";
			return false;
		}
		RaptureHotbarModule* ptr2=RaptureHotbarModule.Instance();
		if(ptr2==null||!ptr2->ModuleReady) {
			LastUsePath="the hotbar module is not ready";
			return false;
		}
		for(int i=0;i<10;i++) {
			int num=12+i*5;
			if(num>=ptr->AtkValuesCount) {
				break;
			}
			AtkValue atkValue=ptr->AtkValues[num];
			AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
			if((uint)((atkValueType==AtkValueType.UInt)?((int)atkValue.UInt):((atkValueType==AtkValueType.Int&&atkValue.Int>0)?atkValue.Int:0))!=item.Id) {
				continue;
			}
			for(int j=0;j<ptr2->Hotbars.Length;j++) {
				for(uint num2=0u;num2<16;num2++) {
					RaptureHotbarModule.HotbarSlot* hotbarSlot=ptr2->Hotbars[j].GetHotbarSlot(num2);
					if(hotbarSlot!=null&&hotbarSlot->CommandType==(RaptureHotbarModule.HotbarSlotType)36&&hotbarSlot->CommandId==i) {
						bool flag=ptr2->ExecuteSlot(hotbarSlot)!=0;
						LastUsePath=(flag?$"used {item.Name} from carried slot {i}":$"{item.Name} is in carried slot {i} but the hotbar refused it");
						return flag;
					}
				}
			}
			LastUsePath=$"{item.Name} is in carried slot {i}, but no hotbar slot points at it";
			return false;
		}
		LastUsePath=item.Name+" is not in any of the ten carried slots";
		return false;
	}
	public static string DescribeFull() {
		List<string> values=new List<string> {
			"=== recognised items ===",
			Describe(),
			string.Empty,
			"=== full node dump of every visible XBM window ===",
			CrucibleUi.Describe()
		};
		return string.Join(Environment.NewLine,values);
	}
	public static string Describe() {
		List<string> list=new List<string>();
		string[] array=new string[5] { "XBMContentsMainHUD", "XBMContentsBooty", "XBMContentsItemShop", "XBMContentsTreasure", "XBMPetParty" };
		foreach(string text in array) {
			if(!IsOpen(text)) {
				list.Add(text+": not open");
				continue;
			}
			List<string> list2=VisibleText(text);
			List<CrucibleItem> list3=ItemsIn(text);
			list.Add($"{text}: open, {list2.Count} text node(s), {list3.Count} recognised item(s)");
			foreach(CrucibleItem item in list3) {
				list.Add($"   ITEM {item.Id} {item.Name} [{item.Kind}] value={item.Value} heal={item.HealPercent}%");
			}
			foreach(string item2 in list2) {
				list.Add("   text: "+item2);
			}
		}
		return string.Join(Environment.NewLine,list);
	}
	static CrucibleContents() {
		string[] acknowledgeLabels=AcknowledgeLabels;
		int num=0;
		string[] array=new string[4+acknowledgeLabels.Length];
		ReadOnlySpan<string> readOnlySpan=new ReadOnlySpan<string>(acknowledgeLabels);
		readOnlySpan.CopyTo(new Span<string>(array).Slice(num,readOnlySpan.Length));
		num+=readOnlySpan.Length;
		array[num]="Return";
		num++;
		array[num]="Back";
		num++;
		array[num]="Cancel";
		num++;
		array[num]="Leave";
		DismissLabels=array;
		PermanentHud=new string[6] { "NamePlate", "CastBarEnemy", "ScenarioTree", "CursorAddon", "AreaMap", "Hud" };
		LastBuyPath="not attempted";
		LastUsePath="not attempted";
	}
}
