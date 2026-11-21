using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace Beastmaster;

public sealed class CrucibleLevelDebugger {
	private enum Mode {
		None,
		Reading,
		Scanning,
		Watching
	}
	public sealed class Row {
		public int No { get; init; }
		public string Name { get; set; }=string.Empty;
		public int? Rank { get; set; }
		public int? Exp { get; set; }
		public string ExpRaw { get; set; }=string.Empty;
		public bool? Registered { get; set; }
		public bool? InTeam { get; set; }
		public DateTime? Seen { get; set; }
		public string Source { get; set; }=string.Empty;
	}
	private readonly BeastmasterConfiguration configuration;
	private readonly List<string> log=new List<string>();
	private readonly object gate=new object();
	private Mode mode;
	private DateTime nextAt=DateTime.MinValue;
	private int subject;
	private int lastHover;
	private int tries;
	private long? seen;
	private List<int> scanIds=new List<int>();
	private int scanIndex;
	private int scanOk;
	private DateTime watchStart;
	private DateTime watchEnd;
	private bool watchSelected;
	private bool watchPartySeen;
	private bool watchGridSeen;
	private string[] watchBaseline=Array.Empty<string>();
	private HashSet<int> watchChanged=new HashSet<int>();
	private readonly Dictionary<int,Row> rows=new Dictionary<int,Row>();
	private HashSet<string> watchRoster=new HashSet<string>();
	private HashSet<int> watchGrid=new HashSet<int>();
	private string watchCounter=string.Empty;
	public string Status { get; private set; }="Idle.";
	public bool Busy => mode!=Mode.None;
	public string SavedPath { get; private set; }=string.Empty;
	public string Text {
		get {
			lock(gate) {
				return string.Join(Environment.NewLine,log);
			}
		}
	}
	public int Lines {
		get {
			lock(gate) {
				return log.Count;
			}
		}
	}
	private Row RowFor(int no) {
		if(!rows.TryGetValue(no,out Row value)) {
			value=new Row {
				No = no,
				Name = new CrucibleBestiaryEntry(no,Selected: false,null).Name
			};
			rows[no]=value;
		}
		return value;
	}
	public List<Row> Table() {
		Dictionary<int,Row> dictionary=new Dictionary<int,Row>(rows);
		foreach(var (num3,value) in configuration.CrucibleBeastRankLearned) {
			int num4=(int)(num3-242000);
			bool flag=((num4<1||num4>50)?true:false);
			if(!flag&&!dictionary.ContainsKey(num4)) {
				configuration.CrucibleBeastExpLearned.TryGetValue(num3,out var value2);
				dictionary[num4]=new Row {
					No = num4,
					Name = new CrucibleBestiaryEntry(num4,Selected: false,null).Name,
					Rank = value,
					Exp = (configuration.CrucibleBeastExpLearned.ContainsKey(num3)?new int?(value2):((int?)null)),
					Source = "saved"
				};
			}
		}
		return dictionary.Values.OrderBy((Row r) => r.No).ToList();
	}
	public void ClearTable() {
		rows.Clear();
		Add("Table cleared (session readings).");
	}
	public void ForgetSaved() {
		configuration.CrucibleBeastRankLearned.Clear();
		configuration.CrucibleBeastExpLearned.Clear();
		configuration.CrucibleBeastExpRaw.Clear();
		configuration.Save();
		rows.Clear();
		Add("Forgot all saved ranks and exp. Scan again to rebuild them.");
	}
	public CrucibleLevelDebugger(BeastmasterConfiguration configuration) {
		this.configuration=configuration;
	}
	public void Clear() {
		lock(gate) {
			log.Clear();
		}
		Status="Cleared.";
	}
	private void Add(string line) {
		lock(gate) {
			log.Add($"{DateTime.Now:HH:mm:ss.fff}  {line}");
			if(log.Count>4000) {
				log.RemoveAt(0);
			}
		}
	}
	private void Heading(string title) {
		Add("===== "+title+" =====");
	}
	public void ReadTeam() {
		Heading("Team window");
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		Add($"XBMPetParty: {CruciblePetParty.LastResult}; {list.Count} familiar(s) read.");
		foreach(CrucibleFamiliar item in list) {
			Add($"  row {item.Row,2}  No.{item.CatalogueId,2}  {item.Name,-16} slot={item.Slot} ({((item.Slot<3)?"IN TEAM":"bench")})  HP {item.HP}/{item.MaxHP}");
		}
		foreach(CrucibleFamiliar item2 in list) {
			RowFor(item2.CatalogueId).InTeam=item2.Slot<3;
		}
		Add($"Team size by slot<3: {Enumerable.Count(list,(CrucibleFamiliar f) => f.Slot<3)}");
		watchBaseline=Raw("XBMPetParty");
		Add($"(baseline of {watchBaseline.Length} raw team-window values captured for the click watch)");
		Status="Read the team.";
	}
	public void OpenBestiary() {
		Heading("Open Master's Bestiary");
		bool flag=CrucibleBestiaryReader.OpenFromTeamComposition();
		Add(flag?"Pressed Master's Bestiary in Team Composition. Wait a moment for it to open.":"Could not find/press the Master's Bestiary button (is Team Composition open?).");
		Status=(flag?"Pressed the Bestiary button.":"Button not found.");
	}
	public void CloseBestiary() {
		Heading("Close Master's Bestiary");
		Add(CrucibleBestiaryReader.Close()?"Close sent.":"Bestiary was not open.");
		Status="Close sent.";
	}
	public void SelectPage(int page) {
		Heading($"Bestiary page {page}");
		Add(CrucibleBestiaryReader.SelectPage(page)?$"Pressed page {page}.":$"Could not press page {page}.");
	}
	public void ReadGrid() {
		Heading("Bestiary grid");
		if(!CrucibleBestiaryReader.IsOpen) {
			Add("The Bestiary is not open.");
			return;
		}
		List<CrucibleBestiaryEntry> list=CrucibleBestiaryReader.Page();
		Add(CrucibleBestiaryReader.LastResult ?? "");
		foreach(CrucibleBestiaryEntry item in list) {
			RowFor(item.CatalogueId).Registered=item.Selected;
		}
		Add("Registered on this page: "+(list.Any((CrucibleBestiaryEntry e) => e.Selected)?string.Join(", ",from e in list
			where e.Selected
			select $"No.{e.CatalogueId} {e.Name}"):"none"));
	}
	public void ReadPane(int catalogueId) {
		if(mode!=Mode.None) {
			return;
		}
		if(!CrucibleBestiaryReader.IsOpen) {
			Add("The Bestiary is not open.");
			return;
		}
		Heading($"Read detail pane for No.{catalogueId}");
		if(lastHover!=0) {
			CrucibleBestiaryReader.Hover(lastHover,leave: true);
		}
		if(!CrucibleBestiaryReader.Hover(catalogueId)) {
			Add($"No.{catalogueId} is not on the page being shown (press the other page).");
			return;
		}
		lastHover=catalogueId;
		subject=catalogueId;
		tries=0;
		nextAt=DateTime.UtcNow.AddMilliseconds(350.0);
		mode=Mode.Reading;
		Status=$"Hovered No.{catalogueId}; reading shortly.";
	}
	public void ScanPage() {
		if(mode!=Mode.None) {
			return;
		}
		if(!CrucibleBestiaryReader.IsOpen) {
			Add("The Bestiary is not open.");
			return;
		}
		Heading("Scan current page");
		scanIds=(from c in CrucibleBestiaryReader.Cells()
			select c.CatalogueId).ToList();
		Add($"{scanIds.Count} familiar(s) on this page: {string.Join(", ",scanIds)}");
		scanIndex=0;
		scanOk=0;
		subject=0;
		tries=0;
		seen=null;
		nextAt=DateTime.UtcNow;
		mode=((scanIds.Count!=0)?Mode.Scanning:Mode.None);
	}
	public void ShowPlan() {
		Heading("Plan (what Level Up Beasts would field)");
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		if(list.Count==0) {
			Add("No team window roster to plan from. Open Team Composition first.");
			return;
		}
		if(configuration.CrucibleBeastRankLearned.Count==0) {
			Add("NOTE: no ranks are saved yet, so every familiar shows 'rank never read'. Open the Bestiary and use 'Scan current page' on both pages first, then Show plan again.");
		}
		int num=CrucibleBeastRank.BoardRankCap(configuration.CrucibleBoard);
		Add($"Board index {configuration.CrucibleBoard} -> rank cap {num}. Learned ranks: {configuration.CrucibleBeastRankLearned.Count}, learned exp: {configuration.CrucibleBeastExpLearned.Count}.");
		List<int> list2=(from c in CrucibleBeastRank.LevelingCandidatesForCap(list.Select((CrucibleFamiliar f) => f.CatalogueId),configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,configuration.CrucibleBeastExpLearned,num).Take(3)
			select c.CatalogueId).ToList();
		foreach(CrucibleFamiliar item in list.OrderBy((CrucibleFamiliar f) => CrucibleBeastRank.EffectiveRank(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,f.CatalogueId) ?? 999)) {
			int? num2=CrucibleBeastRank.EffectiveRank(configuration.CrucibleBeastRankLearned,configuration.CrucibleBeastRankFallback,item.CatalogueId);
			bool flag=configuration.CrucibleBeastExpLearned.TryGetValue(CrucibleBeastRank.MonsterIdFor(item.CatalogueId),out var value);
			string value2;
			if(num2.HasValue) {
				if(num2>num) {
					value2=$"skip - rank above cap {num}";
				}
				else if(((num2==num)&flag)&&value>=100) {
					value2="skip - at cap with a full exp bar";
				}
				else {
					value2=((!list2.Contains(item.CatalogueId))?"eligible, but three lower-ranked were picked first":"PICK");
				}
			}
			else {
				value2="SKIP - rank never read";
			}
			Add($"  No.{item.CatalogueId,2} {item.Name,-16} rank {num2?.ToString() ?? "?",2}  exp {(flag?(value+"%"):"unread"),-7}  in team now: {((item.Slot<3)?"yes":"no ")}  -> {value2}");
		}
		Add("Picked: "+((list2.Count==0)?"nobody":string.Join(", ",list2)));
	}
	public void ClickAndWatch(int catalogueId) {
		if(mode!=Mode.None) {
			return;
		}
		if(!CrucibleBestiaryReader.IsOpen) {
			Add("The Bestiary is not open.");
			return;
		}
		Heading($"Click No.{catalogueId} once and watch");
		bool? flag=CrucibleBestiaryReader.IsRegistered(catalogueId);
		if(!flag.HasValue) {
			Add($"No.{catalogueId} is not on the page being shown (press the other page).");
			return;
		}
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		CrucibleFamiliar? crucibleFamiliar=list.FirstOrDefault((CrucibleFamiliar f) => f.CatalogueId==catalogueId);
		bool value=(object)crucibleFamiliar!=null&&crucibleFamiliar.Slot<3;
		Add($"Before: grid says registered={flag}; team window says in team={value}. Expect both to become {!flag}.");
		watchBaseline=Raw("XBMPetParty");
		watchChanged=new HashSet<int>();
		watchRoster=list.Select((CrucibleFamiliar f) => f.Name+":"+((f.Slot<3)?"team":"bench")).ToHashSet();
		watchGrid=(from e in CrucibleBestiaryReader.Page()
			where e.Selected
			select e.CatalogueId).ToHashSet();
		string[] array=Raw("XBMPetParty");
		watchCounter=((array!=null&&array.Length>0)?array[^1]:string.Empty);
		Add("Team window before: "+Members(list));
		Add("Grid flags before (this page): "+string.Join(",",watchGrid));
		watchSelected=!flag.Value;
		watchPartySeen=(watchGridSeen=false);
		CrucibleBestiaryReader.Hover(catalogueId);
		if(!CrucibleBestiaryReader.Toggle(catalogueId)) {
			Add("The click could not be dispatched.");
			return;
		}
		subject=catalogueId;
		watchStart=DateTime.UtcNow;
		watchEnd=watchStart.AddSeconds(14.0);
		nextAt=watchStart;
		mode=Mode.Watching;
		Add("Clicked (MouseDown). Sampling for 14 seconds.");
		Status="Watching for the change...";
	}
	public void Tick() {
		if(mode==Mode.None||DateTime.UtcNow<nextAt) {
			return;
		}
		try {
			switch(mode) {
			case Mode.Reading:
			TickReading();
			break;
			case Mode.Scanning:
			TickScanning();
			break;
			case Mode.Watching:
			TickWatching();
			break;
			}
		}
		catch(Exception ex) {
			Add("ERROR: "+ex.Message);
			mode=Mode.None;
			DalamudApi.Log.Warning(ex,"Level Up debugger step failed.");
		}
	}
	private void TickReading() {
		DescribePane(subject);
		mode=Mode.None;
		Status="Read the pane.";
	}
	private void DescribePane(int id) {
		uint? num=CrucibleBestiaryReader.ViewedMonsterId();
		string value=CrucibleBestiaryReader.ViewedName();
		int? num2=CrucibleBestiaryReader.ViewedRank();
		int? num3=CrucibleBestiaryReader.ViewedExpPercent();
		Add($"pane monster id={num?.ToString() ?? "null"} (expected {CrucibleBeastRank.MonsterIdFor(id)}), name='{value}', rank={num2?.ToString() ?? "null"}, exp%={num3?.ToString() ?? "null"}");
		Add("raw values 255..266: "+string.Join(" | ",Raw("XBMMonsterNotebook",255,266)));
		if(num==CrucibleBeastRank.MonsterIdFor(id)) {
			Row row=RowFor(id);
			row.Rank=num2 ?? row.Rank;
			row.Exp=num3 ?? row.Exp;
			row.ExpRaw=string.Join("/",from t in Raw("XBMMonsterNotebook",261,262)
				select t.Substring(t.LastIndexOf(' ')+1));
			row.Seen=DateTime.Now;
			row.Source="single read (not saved)";
		}
	}
	private void TickScanning() {
		if(scanIndex>=scanIds.Count) {
			if(lastHover!=0) {
				CrucibleBestiaryReader.Hover(lastHover,leave: true);
			}
			lastHover=0;
			configuration.Save();
			Add($"Scan finished: {scanOk} of {scanIds.Count} read and stored.");
			mode=Mode.None;
			Status="Scan finished.";
			return;
		}
		int num=scanIds[scanIndex];
		if(subject!=num) {
			if(lastHover!=0) {
				CrucibleBestiaryReader.Hover(lastHover,leave: true);
			}
			if(!CrucibleBestiaryReader.Hover(num)) {
				Add($"No.{num}: could not hover.");
				scanIndex++;
			}
			else {
				lastHover=num;
				subject=num;
				tries=0;
				seen=null;
				nextAt=DateTime.UtcNow.AddMilliseconds(250.0);
			}
			return;
		}
		uint? num2=CrucibleBestiaryReader.ViewedMonsterId();
		int? num3=CrucibleBestiaryReader.ViewedRank();
		int? num4=CrucibleBestiaryReader.ViewedExpPercent();
		Status=$"Scanning {scanIndex+1}/{scanIds.Count}.";
		if(num2==CrucibleBeastRank.MonsterIdFor(num)&&num3.HasValue) {
			int valueOrDefault=num3.GetValueOrDefault();
			if(num4.HasValue) {
				int valueOrDefault2=num4.GetValueOrDefault();
				long num5=(long)valueOrDefault*1000L+valueOrDefault2;
				if(seen==num5) {
					CrucibleBeastRank.Learn(configuration.CrucibleBeastRankLearned,num2.Value,valueOrDefault);
					configuration.CrucibleBeastExpLearned[num2.Value]=valueOrDefault2;
					(long,long)? tuple=CrucibleBestiaryReader.ViewedExpRaw();
					if(tuple.HasValue) {
						(long,long) valueOrDefault3=tuple.GetValueOrDefault();
						configuration.CrucibleBeastExpRaw[num2.Value]=$"{valueOrDefault3.Item1}/{valueOrDefault3.Item2}";
					}
					Add($"No.{num,2} {new CrucibleBestiaryEntry(num,Selected: false,null).Name,-16} rank {valueOrDefault,2}  exp {valueOrDefault2}%   raw: {string.Join(" | ",Raw("XBMMonsterNotebook",261,262))}");
					Row row=RowFor(num);
					row.Rank=valueOrDefault;
					row.Exp=valueOrDefault2;
					row.ExpRaw=string.Join("/",from t in Raw("XBMMonsterNotebook",261,262)
						select t.Substring(t.LastIndexOf(' ')+1));
					row.Seen=DateTime.Now;
					row.Source="scan (saved)";
					scanOk++;
					scanIndex++;
					subject=0;
					return;
				}
				seen=num5;
				goto IL_05a1;
			}
		}
		seen=null;
		if(++tries>=8) {
			Add($"No.{num,2}: pane never settled (id {num2?.ToString() ?? "null"}, rank {num3?.ToString() ?? "null"}, exp {num4?.ToString() ?? "null"}). Raw: {string.Join(" | ",Raw("XBMMonsterNotebook",255,266))}");
			scanIndex++;
			subject=0;
			return;
		}
		goto IL_05a1;
		IL_05a1:
		nextAt=DateTime.UtcNow.AddMilliseconds(150.0);
	}
	private static string Members(IEnumerable<CrucibleFamiliar> roster) {
		return string.Join(", ",from f in roster
			orderby f.CatalogueId
			select f.Name+"("+((f.Slot<3)?$"team{f.Slot}":"bench")+")");
	}
	private void TickWatching() {
		double totalMilliseconds=(DateTime.UtcNow-watchStart).TotalMilliseconds;
		List<CrucibleFamiliar> list=CruciblePetParty.Familiars();
		HashSet<string> hashSet=list.Select((CrucibleFamiliar f) => f.Name+":"+((f.Slot<3)?"team":"bench")).ToHashSet();
		if(list.Count>0&&!hashSet.SetEquals(watchRoster)) {
			List<string> source=hashSet.Except(watchRoster).ToList();
			List<string> source2=watchRoster.Except(hashSet).ToList();
			Add($"+{totalMilliseconds,5:0} ms  TEAM WINDOW changed: {list.Count} row(s). now: {string.Join(", ",source.DefaultIfEmpty("-"))}   was: {string.Join(", ",source2.DefaultIfEmpty("-"))}");
			watchRoster=hashSet;
			if(list.Any((CrucibleFamiliar f) => f.CatalogueId==subject&&f.Slot<3==watchSelected)) {
				watchPartySeen=true;
			}
		}
		HashSet<int> hashSet2=(from e in CrucibleBestiaryReader.Page()
			where e.Selected
			select e.CatalogueId).ToHashSet();
		if(!hashSet2.SetEquals(watchGrid)) {
			Add($"+{totalMilliseconds,5:0} ms  GRID changed: newly flagged {string.Join(",",hashSet2.Except(watchGrid).DefaultIfEmpty(0))}; no longer flagged {string.Join(",",watchGrid.Except(hashSet2).DefaultIfEmpty(0))} (0 = none)");
			watchGrid=hashSet2;
			if(hashSet2.Contains(subject)==watchSelected) {
				watchGridSeen=true;
			}
		}
		string[] array=Raw("XBMPetParty");
		string text=((array!=null&&array.Length>0)?array[^1]:string.Empty);
		if(text!=watchCounter) {
			Add($"+{totalMilliseconds,5:0} ms  last team-window value (counter?): {text}");
			watchCounter=text;
		}
		if(DateTime.UtcNow>=watchEnd) {
			Add("Watch over. Team window now: "+Members(list));
			Add($"Subject No.{subject}: grid showed the expected state = {watchGridSeen}; team window showed it = {watchPartySeen}.");
			mode=Mode.None;
			Status="Watch finished.";
		}
		else {
			nextAt=DateTime.UtcNow.AddMilliseconds(150.0);
		}
	}
	private unsafe static string[] Raw(string addon,int from=0,int to=int.MaxValue) {
		AtkUnitBase* address=(AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(addon).Address;
		if(address==null||address->AtkValues==null) {
			return Array.Empty<string>();
		}
		int num=Math.Min(to,address->AtkValuesCount-1);
		List<string> list=new List<string>();
		for(int i=from;i<=num;i++) {
			AtkValue atkValue=address->AtkValues[i];
			AtkValueType atkValueType=atkValue.Type&AtkValueType.TypeMask;
			string text;
			switch(atkValueType) {
			case AtkValueType.UInt:
			text=atkValue.UInt.ToString();
			break;
			case AtkValueType.Int:
			text=atkValue.Int.ToString();
			break;
			case AtkValueType.Bool:
			text=atkValue.Bool.ToString();
			break;
			case AtkValueType.Float:
			text=atkValue.Float.ToString("0.###");
			break;
			case AtkValueType.String:
			case AtkValueType.ConstString:
			case AtkValueType.ManagedString:
			text=$"\"{atkValue.String}\"";
			break;
			default:
			text="-";
			break;
			}
			string text2=text;
			list.Add((from==0)?text2:$"[{i}] {atkValueType} {text2}");
		}
		return list.ToArray();
	}
	public string Save() {
		try {
			string pluginConfigDirectory=DalamudApi.PluginInterface.GetPluginConfigDirectory();
			Directory.CreateDirectory(pluginConfigDirectory);
			string text=Path.Combine(pluginConfigDirectory,$"levelup-debug-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
			File.WriteAllText(text,Text,Encoding.UTF8);
			SavedPath=text;
			Status="Saved.";
			return text;
		}
		catch(Exception ex) {
			SavedPath=string.Empty;
			Status="Could not save: "+ex.Message;
			return Status;
		}
	}
}
