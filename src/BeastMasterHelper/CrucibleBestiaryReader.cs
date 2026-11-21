using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CrucibleBestiaryReader {
	public sealed record GridCell(int CatalogueId,int Param);
	public const string Addon="XBMMonsterNotebook";
	public const int PageSize=25;
	private const int GridBase=24;
	private const int GridStride=8;
	private const int RankValue=258;
	private const int DetailMonsterIdValue=231;
	public static string LastResult { get; private set; }="not read";
	public unsafe static bool IsOpen => Unit()!=null;
	private unsafe static AtkUnitBase* Unit() {
		try {
			AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMMonsterNotebook").Address;
			return (address!=null&&address->IsVisible&&address->IsReady&&address->AtkValues!=null)?address:null;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not reach the Master's Bestiary window.");
			return null;
		}
	}
	public static bool OpenFromTeamComposition() {
		return CrucibleUi.ClickIn("XBMPetParty","Master's Bestiary");
	}
	public unsafe static bool Close() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return false;
		}
		ptr->Close(fireCallback: true);
		return true;
	}
	public unsafe static List<CrucibleBestiaryEntry> Page() {
		List<CrucibleBestiaryEntry> list=new List<CrucibleBestiaryEntry>();
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			LastResult="the Master's Bestiary is not open";
			return list;
		}
		for(int i=0;i<25;i++) {
			int num=24+i*8;
			if(num+8>ptr->AtkValuesCount) {
				break;
			}
			uint? num2=Number(ptr,num);
			uint? num3=Number(ptr,num+4);
			bool flag;
			switch(num2) {
			default:
			flag=true;
			break;
			case 1u:
			case 2u:
			case 3u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 9u:
			case 10u:
			case 11u:
			case 12u:
			case 13u:
			case 14u:
			case 15u:
			case 16u:
			case 17u:
			case 18u:
			case 19u:
			case 20u:
			case 21u:
			case 22u:
			case 23u:
			case 24u:
			case 25u:
			case 26u:
			case 27u:
			case 28u:
			case 29u:
			case 30u:
			case 31u:
			case 32u:
			case 33u:
			case 34u:
			case 35u:
			case 36u:
			case 37u:
			case 38u:
			case 39u:
			case 40u:
			case 41u:
			case 42u:
			case 43u:
			case 44u:
			case 45u:
			case 46u:
			case 47u:
			case 48u:
			case 49u:
			case 50u:
			flag=false;
			break;
			}
			if(flag||num3!=242000+num2) {
				break;
			}
			list.Add(new CrucibleBestiaryEntry((int)num2.Value,Bool(ptr,num+6),ParamOf(ptr,(int)num2.Value)));
		}
		LastResult=((list.Count==0)?"the Master's Bestiary is open but its grid did not match the verified layout":$"read {list.Count} entry(ies), {Enumerable.Count(list,(CrucibleBestiaryEntry e) => e.Selected)} registered");
		return list;
	}
	public unsafe static int? ViewedRank() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return null;
		}
		// Try multiple atkValue indices for rank, since game patches may shift the layout
		// Beast rank values are typically 1-50, so validate the result is in a sane range
		for(int idx=250; idx<=270; idx++) {
			if(idx>=ptr->AtkValuesCount) {
				continue;
			}
			AtkValue atkValue=ptr->AtkValues[idx];
			AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
			int? result=null;
			if((atkValueType==AtkValueType.String||atkValueType==AtkValueType.ConstString||atkValueType==AtkValueType.ManagedString)?true:false) {
				if(int.TryParse(atkValue.String.ToString(),out var parsed)) {
					result=parsed;
				}
			}
			else {
				result=atkValueType switch {
					AtkValueType.Int => (int?)atkValue.Int,
					AtkValueType.UInt => (int)atkValue.UInt,
					_ => null,
				};
			}
			if(result.HasValue && result.Value>=1 && result.Value<=50) {
				return result;
			}
		}
		return null;
	}
	public unsafe static int? ViewedExpPercent() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return null;
		}
		long? num=Numeric(ptr,261);
		long? num2=Numeric(ptr,262);
		bool flag=!num.HasValue;
		if(!flag) {
			bool flag2=((!num2.HasValue||num2.GetValueOrDefault()<=0)?true:false);
			flag=flag2;
		}
		if(flag) {
			return null;
		}
		return (int)Math.Min(100L,num.Value*100/num2.Value);
	}
	public unsafe static (long Current,long Max)? ViewedExpRaw() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return null;
		}
		long? num=Numeric(ptr,261);
		long? num2=Numeric(ptr,262);
		bool flag=!num.HasValue;
		if(!flag) {
			bool flag2=((!num2.HasValue||num2.GetValueOrDefault()<=0)?true:false);
			flag=flag2;
		}
		if(!flag) {
			return (num.Value,num2.Value);
		}
		return null;
	}
	public unsafe static string ViewedName() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null||230>=ptr->AtkValuesCount) {
			return string.Empty;
		}
		AtkValue atkValue=ptr->AtkValues[230];
		AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
		if((atkValueType!=AtkValueType.String&&atkValueType!=AtkValueType.ConstString&&atkValueType!=AtkValueType.ManagedString)||1==0) {
			return string.Empty;
		}
		return atkValue.String.ToString().Trim();
	}
	public unsafe static uint? ViewedMonsterId() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return null;
		}
		// Try multiple atkValue indices for monster ID, since game patches may shift the layout
		// Monster IDs for familiars are in range 242001-242050
		for(int idx=225; idx<=240; idx++) {
			if(idx>=ptr->AtkValuesCount) {
				continue;
			}
			uint? val=Number(ptr,idx);
			if(val.HasValue && val.Value>=242001 && val.Value<=242050) {
				return val;
			}
		}
		return null;
	}
	public unsafe static int? ViewedEntry() {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return null;
		}
		uint? num=Number(ptr,231);
		switch(num) {
		case 242001u:
		case 242002u:
		case 242003u:
		case 242004u:
		case 242005u:
		case 242006u:
		case 242007u:
		case 242008u:
		case 242009u:
		case 242010u:
		case 242011u:
		case 242012u:
		case 242013u:
		case 242014u:
		case 242015u:
		case 242016u:
		case 242017u:
		case 242018u:
		case 242019u:
		case 242020u:
		case 242021u:
		case 242022u:
		case 242023u:
		case 242024u:
		case 242025u:
		case 242026u:
		case 242027u:
		case 242028u:
		case 242029u:
		case 242030u:
		case 242031u:
		case 242032u:
		case 242033u:
		case 242034u:
		case 242035u:
		case 242036u:
		case 242037u:
		case 242038u:
		case 242039u:
		case 242040u:
		case 242041u:
		case 242042u:
		case 242043u:
		case 242044u:
		case 242045u:
		case 242046u:
		case 242047u:
		case 242048u:
		case 242049u:
		case 242050u:
		return (int)(num.Value-242000);
		default:
		return null;
		}
	}
	public static string Describe() {
		return Describe(null,null,0);
	}
	public static string Describe(IReadOnlyDictionary<uint,int>? learned,IReadOnlyDictionary<int,int>? fallback,int rankThreshold) {
		if(!IsOpen) {
			return "The Master's Bestiary is not open.";
		}
		List<CrucibleBestiaryEntry> source=Page();
		List<string> list=(from e in source
			where e.Selected
			select e.Name).ToList();
		int? num=ViewedEntry();
		int? value=ViewedRank();
		string text=((!num.HasValue)?string.Empty:(" Viewing "+new CrucibleBestiaryEntry(num.Value,Selected: false,null).Name+((!value.HasValue)?" (rank unreadable).":$" at Beast Rank {value}.")));
		string text2=string.Empty;
		if(learned!=null&&fallback!=null) {
			List<(int,int)> list2=CrucibleBeastRank.LevelingCandidates(source.Select((CrucibleBestiaryEntry e) => e.CatalogueId),learned,fallback,rankThreshold);
			text2=((list2.Count==0)?$" No known familiar is at or below rank {rankThreshold} yet.":($" At or below rank {rankThreshold}: "+string.Join(", ",list2.Select(((int CatalogueId,int Rank) f) => $"{new CrucibleBestiaryEntry(f.CatalogueId,Selected: false,null).Name} ({f.Rank})"))+"."));
		}
		return "Master's Bestiary: "+LastResult+"."+((list.Count==0)?" None registered on this page.":(" Registered here: "+string.Join(", ",list)+"."))+text+text2;
	}
	public unsafe static List<GridCell> Cells() {
		List<GridCell> list=new List<GridCell>();
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return list;
		}
		try {
			CollectCells(ptr,&ptr->UldManager,list,0);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read the Master's Bestiary grid.");
		}
		return list;
	}
	private unsafe static void CollectCells(AtkUnitBase* unit,AtkUldManager* manager,List<GridCell> cells,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			uint? num=IconIdIn(&component->UldManager,0);
			switch(num) {
			case 242001u:
			case 242002u:
			case 242003u:
			case 242004u:
			case 242005u:
			case 242006u:
			case 242007u:
			case 242008u:
			case 242009u:
			case 242010u:
			case 242011u:
			case 242012u:
			case 242013u:
			case 242014u:
			case 242015u:
			case 242016u:
			case 242017u:
			case 242018u:
			case 242019u:
			case 242020u:
			case 242021u:
			case 242022u:
			case 242023u:
			case 242024u:
			case 242025u:
			case 242026u:
			case 242027u:
			case 242028u:
			case 242029u:
			case 242030u:
			case 242031u:
			case 242032u:
			case 242033u:
			case 242034u:
			case 242035u:
			case 242036u:
			case 242037u:
			case 242038u:
			case 242039u:
			case 242040u:
			case 242041u:
			case 242042u:
			case 242043u:
			case 242044u:
			case 242045u:
			case 242046u:
			case 242047u:
			case 242048u:
			case 242049u:
			case 242050u: {
				AtkEvent* ptr2=RegisteredOn(&component->UldManager,AtkEventType.MouseOver,(AtkEventListener*)unit,0);
				if(ptr2!=null) {
					cells.Add(new GridCell((int)(num.Value-242000),(int)ptr2->Param));
				}
				break;
			}
			default:
			CollectCells(unit,&component->UldManager,cells,depth+1);
			break;
			}
		}
	}
	private unsafe static uint? IconIdIn(AtkUldManager* manager,int depth) {
		if(depth>4||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				if(component->GetComponentType()==ComponentType.Icon) {
					return ((AtkComponentIcon*)component)->IconId;
				}
				uint? result=IconIdIn(&component->UldManager,depth+1);
				if(result.HasValue) {
					return result;
				}
			}
		}
		return null;
	}
	private unsafe static AtkEvent* RegisteredOn(AtkUldManager* manager,AtkEventType type,AtkEventListener* listener,int depth) {
		if(depth>6||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			AtkEvent* ptr2=ptr->AtkEventManager.Event;
			int num=0;
			while(ptr2!=null&&num<128) {
				if(ptr2->State.EventType==type&&ptr2->Listener==listener) {
					return ptr2;
				}
				num++;
				ptr2=ptr2->NextEvent;
			}
			if((int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				AtkEvent* ptr3=RegisteredOn(&component->UldManager,type,listener,depth+1);
				if(ptr3!=null) {
					return ptr3;
				}
			}
		}
		return null;
	}
	public static bool Hover(int catalogueId,bool leave=false) {
		return Dispatch(catalogueId,leave?AtkEventType.MouseOut:AtkEventType.MouseOver);
	}
	public static bool Toggle(int catalogueId) {
		return Dispatch(catalogueId,AtkEventType.MouseDown);
	}
	private unsafe static bool Dispatch(int catalogueId,AtkEventType type) {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return false;
		}
		try {
			AtkEvent* ptr2=FindCellEvent(ptr,&ptr->UldManager,catalogueId,type,0);
			if(ptr2==null) {
				return false;
			}
			AtkEvent atkEvent=*ptr2;
			atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
			atkEvent.State.ReturnFlags=0;
			AtkEventData atkEventData=default;
			AtkResNode* node=ptr2->Node;
			if(node!=null) {
				atkEventData.MouseData.PosX=(short)Math.Clamp(node->ScreenX+(float)(int)node->Width*node->GetScaleX()/2f,-32768f,32767f);
				atkEventData.MouseData.PosY=(short)Math.Clamp(node->ScreenY+(float)(int)node->Height*node->GetScaleY()/2f,-32768f,32767f);
			}
			((AtkEventListener*)ptr)->ReceiveEvent(type,(int)atkEvent.Param,&atkEvent,&atkEventData);
			return true;
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not hover a Master's Bestiary cell.");
			return false;
		}
	}
	private unsafe static AtkEvent* FindCellEvent(AtkUnitBase* unit,AtkUldManager* manager,int catalogueId,AtkEventType type,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				if(IconIdIn(&component->UldManager,0)==(uint?)(242000+catalogueId)) {
					return RegisteredOn(&component->UldManager,type,(AtkEventListener*)unit,0);
				}
				AtkEvent* ptr2=FindCellEvent(unit,&component->UldManager,catalogueId,type,depth+1);
				if(ptr2!=null) {
					return ptr2;
				}
			}
		}
		return null;
	}
	public unsafe static bool SelectPage(int page) {
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			return false;
		}
		try {
			return PressPage(&ptr->UldManager,page.ToString(),0);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not press a Master's Bestiary page button.");
			return false;
		}
	}
	private unsafe static bool PressPage(AtkUldManager* manager,string label,int depth) {
		if(depth>6||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.RadioButton&&ptr->IsVisible()&&TextOf(&component->UldManager,0)==label) {
				for(AtkEvent* ptr2=ptr->AtkEventManager.Event;ptr2!=null;ptr2=ptr2->NextEvent) {
					if(ptr2->State.EventType==AtkEventType.ButtonClick&&ptr2->Listener!=null) {
						AtkEvent atkEvent=*ptr2;
						atkEvent.Node=null;
						atkEvent.State.StateFlags=AtkEventStateFlags.Unk3;
						atkEvent.State.ReturnFlags=0;
						AtkEventData atkEventData=default;
						ptr2->Listener->ReceiveEvent(AtkEventType.ButtonClick,(int)atkEvent.Param,&atkEvent,&atkEventData);
						return true;
					}
				}
				return false;
			}
			if(PressPage(&component->UldManager,label,depth+1)) {
				return true;
			}
		}
		return false;
	}
	private unsafe static string TextOf(AtkUldManager* manager,int depth) {
		if(depth>3||manager==null||manager->NodeList==null) {
			return string.Empty;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,64);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr!=null&&ptr->Type==NodeType.Text) {
				string text=((AtkTextNode*)ptr)->NodeText.ToString().Trim();
				if(text.Length>0) {
					return text;
				}
			}
		}
		return string.Empty;
	}
	private unsafe static uint? ParamOf(AtkUnitBase* unit,int catalogueId) {
		try {
			return ParamOf(&unit->UldManager,catalogueId,0);
		}
		catch(Exception exception) {
			DalamudApi.Log.Warning(exception,"Could not read a Bestiary entry's click parameter.");
			return null;
		}
	}
	private unsafe static uint? ParamOf(AtkUldManager* manager,int catalogueId,int depth) {
		if(depth>8||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,2048);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||!ptr->IsVisible()||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(HoldsIcon(&component->UldManager,(uint)(242000+catalogueId),0)) {
				uint? result=MouseDownParam(&component->UldManager,0);
				if(result.HasValue) {
					return result;
				}
			}
			uint? result2=ParamOf(&component->UldManager,catalogueId,depth+1);
			if(result2.HasValue) {
				return result2;
			}
		}
		return null;
	}
	private unsafe static bool HoldsIcon(AtkUldManager* manager,uint icon,int depth) {
		if(depth>6||manager==null||manager->NodeList==null) {
			return false;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null||(int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component==null) {
				continue;
			}
			if(component->GetComponentType()==ComponentType.Icon) {
				AtkComponentIcon* ptr2=(AtkComponentIcon*)component;
				if(ptr2->IconId==icon) {
					return true;
				}
			}
			if(HoldsIcon(&component->UldManager,icon,depth+1)) {
				return true;
			}
		}
		return false;
	}
	private unsafe static uint? MouseDownParam(AtkUldManager* manager,int depth) {
		if(depth>6||manager==null||manager->NodeList==null) {
			return null;
		}
		for(int i=0;i<Math.Min((int)manager->NodeListCount,512);i++) {
			AtkResNode* ptr=manager->NodeList[i];
			if(ptr==null) {
				continue;
			}
			AtkEvent* ptr2=ptr->AtkEventManager.Event;
			int num=0;
			while(ptr2!=null&&num<128) {
				if(ptr2->State.EventType==AtkEventType.MouseDown) {
					return ptr2->Param;
				}
				num++;
				ptr2=ptr2->NextEvent;
			}
			if((int)ptr->Type<1000) {
				continue;
			}
			AtkComponentBase* component=((AtkComponentNode*)ptr)->Component;
			if(component!=null) {
				uint? result=MouseDownParam(&component->UldManager,depth+1);
				if(result.HasValue) {
					return result;
				}
			}
		}
		return null;
	}
	private unsafe static uint? Number(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return null;
		}
		AtkValue atkValue=unit->AtkValues[i];
		if((atkValue.Type&AtkValueType.TypeMask)!=AtkValueType.UInt) {
			return null;
		}
		return atkValue.UInt;
	}
	private unsafe static long? Numeric(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return null;
		}
		AtkValue atkValue=unit->AtkValues[i];
		return (atkValue.Type&AtkValueType.TypeMask) switch {
			AtkValueType.UInt => (long?)atkValue.UInt,
			AtkValueType.Int => atkValue.Int,
			AtkValueType.Float => (long)atkValue.Float,
			_ => null,
		};
	}
	public static bool? IsRegistered(int catalogueId) {
		return Page().FirstOrDefault((CrucibleBestiaryEntry e) => e.CatalogueId==catalogueId)?.Selected;
	}
	private unsafe static bool Bool(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return false;
		}
		AtkValue atkValue=unit->AtkValues[i];
		if((atkValue.Type&AtkValueType.TypeMask)==AtkValueType.Bool) {
			return atkValue.Bool;
		}
		return false;
	}
}
