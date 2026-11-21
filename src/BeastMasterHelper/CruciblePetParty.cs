using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

internal static class CruciblePetParty {
	private const int Stride=77;
	public static string LastResult { get; private set; }="not attempted";
	public unsafe static bool IsOpen => Unit()!=null;
	public static IReadOnlyList<CrucibleFamiliar> LastKnown { get; private set; }=Array.Empty<CrucibleFamiliar>();
	public static bool IsCampsite {
		get {
			if(IsOpen) {
				return CrucibleUi.ButtonCaptions("XBMPetParty").Contains("Rest",StringComparer.OrdinalIgnoreCase);
			}
			return false;
		}
	}
	private unsafe static AtkUnitBase* Unit() {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMPetParty").Address;
		if(address==null||!address->IsVisible||!address->IsReady||address->AtkValues==null) {
			return null;
		}
		return address;
	}
	public unsafe static List<CrucibleFamiliar> Familiars() {
		List<CrucibleFamiliar> list=new List<CrucibleFamiliar>();
		AtkUnitBase* ptr=Unit();
		if(ptr==null) {
			LastResult="the team window is not open";
			return list;
		}
		uint? num=Number(ptr,5);
		bool flag;
		switch(num) {
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
		if(flag||82+(num.Value-1)*77>=ptr->AtkValuesCount) {
			LastResult="the team window layout is not recognised";
			return list;
		}
		for(int i=0;i<num.Value;i++) {
			int num2=i*77;
			uint? num3=Number(ptr,82+num2);
			uint? num4=Number(ptr,80+num2);
			uint? num5=Number(ptr,79+num2);
			bool restSelected=Flag(ptr,81+num2);
			uint? num6=Number(ptr,11+num2);
			uint? num7=Number(ptr,12+num2);
			string text=Text(ptr,9+num2);
			if(num3.HasValue&&num4.HasValue&&num6.HasValue&&num7.HasValue&&num7!=0&&!(num6>num7)&&text.Length!=0&&FamiliarCombatData.All.TryGetValue((int)num3.Value,out FamiliarCombatProfile value)) {
				list.Add(new CrucibleFamiliar(i,(int)num3.Value,text,num6.Value,num7.Value,(int)num4.Value,value) {
					FeedSlots = (int)num5.GetValueOrDefault(),
					RestSelected = restSelected
				});
			}
		}
		LastResult=$"read {list.Count} familiar(s)";
		if(list.Count>0) {
			LastKnown=list;
		}
		return list;
	}
	public static void Forget() {
		LastKnown=Array.Empty<CrucibleFamiliar>();
	}
	public static bool GiveTo(CrucibleFamiliar familiar,out string what) {
		what=string.Empty;
		if(!IsOpen) {
			what="the team window closed first";
			return false;
		}
		if(CrucibleListClick.Row("XBMPetParty",familiar.Name,out string result)) {
			what=result;
			return true;
		}
		if(CrucibleUi.ClickRowContaining("XBMPetParty",familiar.Name)) {
			what="selected "+familiar.Name;
			return true;
		}
		what="could not select "+familiar.Name+" - "+result;
		return false;
	}
	public static bool GiveToSelf(out string what) {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null) {
			what="the player could not be read";
			return false;
		}
		string textValue=localPlayer.Name.TextValue;
		if(!IsOpen) {
			what="the team window closed first";
			return false;
		}
		if(CrucibleListClick.Row("XBMPetParty",textValue,out string result)) {
			what="selected "+textValue;
			return true;
		}
		what="no row for "+textValue+" - "+result;
		return false;
	}
	public static bool OffersSelf() {
		IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
		if(localPlayer==null||!IsOpen) {
			return false;
		}
		string name=localPlayer.Name.TextValue;
		return CrucibleListClick.RowTexts("XBMPetParty").Any((string text) => text.Contains(name,StringComparison.OrdinalIgnoreCase));
	}
	public static bool Rest(out string what) {
		if(CrucibleUi.ClickIn("XBMPetParty","Rest")) {
			what="pressed Rest";
			return true;
		}
		what="no enabled Rest button was found";
		return false;
	}
	private unsafe static bool Flag(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return false;
		}
		AtkValue atkValue=unit->AtkValues[i];
		if((atkValue.Type&AtkValueType.TypeMask)==AtkValueType.Bool) {
			return atkValue.Bool;
		}
		return false;
	}
	private unsafe static string Text(AtkUnitBase* unit,int i) {
		if(unit==null||i<0||i>=unit->AtkValuesCount) {
			return string.Empty;
		}
		AtkValue atkValue=unit->AtkValues[i];
		AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
		if((atkValueType!=AtkValueType.String&&atkValueType!=AtkValueType.ConstString)||1==0) {
			return string.Empty;
		}
		return atkValue.String.ToString();
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
}
