using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace Beastmaster;

public static class CrucibleBoardMap {
	public const uint FirstBoardTerritory=1339u;
	public const uint SecondBoardTerritory=1340u;
	public const uint ThirdBoardTerritory=1341u;
	public const uint FirstMastersBoardTerritory=1342u;
	public const uint SecondMastersBoardTerritory=1343u;
	private static readonly IReadOnlyDictionary<uint,IReadOnlyList<CruciblePlatform>> Boards=new Dictionary<uint,IReadOnlyList<CruciblePlatform>> {
		[1339u]=new CruciblePlatform[13] {
			new CruciblePlatform(new Vector3(-700f,0f,0f),CruciblePlatformKind.Entrance,"Entrance",0),
			new CruciblePlatform(new Vector3(-700f,0f,-9f),CruciblePlatformKind.Fight,"Fight 1",1),
			new CruciblePlatform(new Vector3(-705f,0f,-16.5f),CruciblePlatformKind.Fight,"Fight 2 (to campsite)",2) {
				Leads = CruciblePlatformKind.Campsite
			},
			new CruciblePlatform(new Vector3(-695f,0f,-16.5f),CruciblePlatformKind.Fight,"Fight 2 (to treasure)",2) {
				Leads = CruciblePlatformKind.Treasure
			},
			new CruciblePlatform(new Vector3(-705f,0f,-25.5f),CruciblePlatformKind.Campsite,"Campsite",3),
			new CruciblePlatform(new Vector3(-695f,0f,-25.5f),CruciblePlatformKind.Treasure,"Treasure",3),
			new CruciblePlatform(new Vector3(-700f,0f,-33f),CruciblePlatformKind.Fight,"Fight 3",4),
			new CruciblePlatform(new Vector3(-700f,0f,-42f),CruciblePlatformKind.Treasure,"Treasure",5),
			new CruciblePlatform(new Vector3(-700f,0f,-51f),CruciblePlatformKind.Fight,"Fight 4",6),
			new CruciblePlatform(new Vector3(-705f,0f,-58.5f),CruciblePlatformKind.Treasure,"Treasure",7),
			new CruciblePlatform(new Vector3(-695f,0f,-58.5f),CruciblePlatformKind.Campsite,"Campsite",7),
			new CruciblePlatform(new Vector3(-700f,0f,-66f),CruciblePlatformKind.Shop,"Item Shop",8),
			new CruciblePlatform(new Vector3(-700f,0f,-75f),CruciblePlatformKind.FinalFight,"Final fight",9)
		},
		[1340u]=new CruciblePlatform[14] {
			new CruciblePlatform(new Vector3(-700f,0f,0f),CruciblePlatformKind.Entrance,"Entrance",0),
			new CruciblePlatform(new Vector3(-700f,0f,-9f),CruciblePlatformKind.Fight,"Fight 1",1),
			new CruciblePlatform(new Vector3(-700f,0f,-18f),CruciblePlatformKind.Shop,"Shop 1",2),
			new CruciblePlatform(new Vector3(-705f,0f,-25.5f),CruciblePlatformKind.Fight,"Fight 2 (to treasure)",3) {
				Leads = CruciblePlatformKind.Treasure
			},
			new CruciblePlatform(new Vector3(-695f,0f,-25.5f),CruciblePlatformKind.Fight,"Fight 2 (to campsite)",3) {
				Leads = CruciblePlatformKind.Campsite
			},
			new CruciblePlatform(new Vector3(-705f,0f,-34.5f),CruciblePlatformKind.Treasure,"Treasure",4),
			new CruciblePlatform(new Vector3(-695f,0f,-34.5f),CruciblePlatformKind.Campsite,"Campsite",4),
			new CruciblePlatform(new Vector3(-700f,0f,-42f),CruciblePlatformKind.Fight,"Fight 3",5),
			new CruciblePlatform(new Vector3(-700f,0f,-51f),CruciblePlatformKind.Treasure,"Treasure",6),
			new CruciblePlatform(new Vector3(-700f,0f,-60f),CruciblePlatformKind.Fight,"Fight 4",7),
			new CruciblePlatform(new Vector3(-695f,0f,-67.5f),CruciblePlatformKind.Treasure,"Treasure",8),
			new CruciblePlatform(new Vector3(-705f,0f,-67.5f),CruciblePlatformKind.Random,"Fight 5 or Treasure",8),
			new CruciblePlatform(new Vector3(-700f,0f,-75f),CruciblePlatformKind.Shop,"Item Shop",9),
			new CruciblePlatform(new Vector3(-700f,0f,-84f),CruciblePlatformKind.FinalFight,"Final fight",10)
		},
		[1341u]=new CruciblePlatform[17] {
			new CruciblePlatform(new Vector3(-700f,0f,0f),CruciblePlatformKind.Entrance,"Entrance",0),
			new CruciblePlatform(new Vector3(-700f,0f,-9f),CruciblePlatformKind.Fight,"Fight 1",1),
			new CruciblePlatform(new Vector3(-700f,0f,-18f),CruciblePlatformKind.Fight,"Fight 2",2),
			new CruciblePlatform(new Vector3(-700f,0f,-27f),CruciblePlatformKind.Shop,"Item Shop 1",3),
			new CruciblePlatform(new Vector3(-695f,0f,-34.5f),CruciblePlatformKind.Fight,"Fight 3 (to campsite)",4) {
				Leads = CruciblePlatformKind.Campsite
			},
			new CruciblePlatform(new Vector3(-705f,0f,-34.5f),CruciblePlatformKind.Fight,"Fight 3 (to treasure)",4) {
				Leads = CruciblePlatformKind.Treasure
			},
			new CruciblePlatform(new Vector3(-695f,0f,-43.5f),CruciblePlatformKind.Campsite,"Campsite",5),
			new CruciblePlatform(new Vector3(-705f,0f,-43.5f),CruciblePlatformKind.Treasure,"Treasure",5),
			new CruciblePlatform(new Vector3(-700f,0f,-51f),CruciblePlatformKind.Fight,"Fight 4",6),
			new CruciblePlatform(new Vector3(-695f,0f,-58.5f),CruciblePlatformKind.Shop,"Item Shop 2",7),
			new CruciblePlatform(new Vector3(-705f,0f,-58.5f),CruciblePlatformKind.Treasure,"Treasure",7),
			new CruciblePlatform(new Vector3(-700f,0f,-66f),CruciblePlatformKind.Fight,"Fight 5",8),
			new CruciblePlatform(new Vector3(-700f,0f,-75f),CruciblePlatformKind.Treasure,"Treasure",9),
			new CruciblePlatform(new Vector3(-695f,0f,-82.5f),CruciblePlatformKind.Campsite,"Campsite",10),
			new CruciblePlatform(new Vector3(-705f,0f,-82.5f),CruciblePlatformKind.Random,"Fight or Treasure",10),
			new CruciblePlatform(new Vector3(-700f,0f,-90f),CruciblePlatformKind.Shop,"Item Shop 3",11),
			new CruciblePlatform(new Vector3(-700f,0f,-99f),CruciblePlatformKind.FinalFight,"Final fight",12)
		},
		[1342u]=new CruciblePlatform[15] {
			new CruciblePlatform(new Vector3(-700f,0f,0f),CruciblePlatformKind.Entrance,"Entrance",0) {
				Opens = new[] { "Fight 1" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-9f),CruciblePlatformKind.Fight,"Fight 1",1) {
				Opens = new string[2] { "Fight 2", "Random 1" }
			},
			new CruciblePlatform(new Vector3(-705f,0f,-16.5f),CruciblePlatformKind.Fight,"Fight 2",2) {
				Opens = new[] { "Item Shop 1" }
			},
			new CruciblePlatform(new Vector3(-695f,0f,-16.5f),CruciblePlatformKind.Random,"Random 1",2) {
				Opens = new[] { "Item Shop 1" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-24f),CruciblePlatformKind.Shop,"Item Shop 1",3) {
				Opens = new string[2] { "Campsite", "Random 2" }
			},
			new CruciblePlatform(new Vector3(-705f,0f,-31.5f),CruciblePlatformKind.Campsite,"Campsite",4) {
				Opens = new[] { "Fight 3" }
			},
			new CruciblePlatform(new Vector3(-695f,0f,-31.5f),CruciblePlatformKind.Random,"Random 2",4) {
				Opens = new[] { "Fight 3" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-39f),CruciblePlatformKind.Fight,"Fight 3",5) {
				Opens = new string[2] { "Treasure", "Random 3" }
			},
			new CruciblePlatform(new Vector3(-695f,0f,-46.5f),CruciblePlatformKind.Treasure,"Treasure",6) {
				Opens = new[] { "Fight 4 (normal boss)" }
			},
			new CruciblePlatform(new Vector3(-705f,0f,-46.5f),CruciblePlatformKind.Random,"Random 3",6) {
				Opens = new[] { "Fight 4 (medium boss)" }
			},
			new CruciblePlatform(new Vector3(-705f,0f,-55.5f),CruciblePlatformKind.Fight,"Fight 4 (medium boss)",7) {
				Opens = new[] { "Random 4" }
			},
			new CruciblePlatform(new Vector3(-695f,0f,-55.5f),CruciblePlatformKind.Fight,"Fight 4 (normal boss)",7) {
				Opens = new[] { "Item Shop 2" }
			},
			new CruciblePlatform(new Vector3(-695f,0f,-64.5f),CruciblePlatformKind.Shop,"Item Shop 2",8) {
				Opens = new[] { "Final fight" }
			},
			new CruciblePlatform(new Vector3(-705f,0f,-64.5f),CruciblePlatformKind.Random,"Random 4",8) {
				Opens = new[] { "Final fight" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-72f),CruciblePlatformKind.FinalFight,"Final fight",9) {
				Opens = Array.Empty<string>()
			}
		},
		[1343u]=new CruciblePlatform[26] {
			new CruciblePlatform(new Vector3(-700f,0f,0f),CruciblePlatformKind.Entrance,"Entrance",0) {
				Opens = new[] { "Fight 1" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-9f),CruciblePlatformKind.Fight,"Fight 1",1) {
				Opens = new string[2] { "Fight 2", "Item Shop 1" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-9f),CruciblePlatformKind.Fight,"Fight 2",2) {
				Opens = new string[2] { "Treasure 2", "Campsite 1" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-9f),CruciblePlatformKind.Shop,"Item Shop 1",2) {
				Opens = new string[2] { "Treasure 2", "Random 2" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-18f),CruciblePlatformKind.Campsite,"Campsite 1",3) {
				Opens = new[] { "Random 1" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-18f),CruciblePlatformKind.Treasure,"Treasure 2",3) {
				Opens = new[] { "Fight 3" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-18f),CruciblePlatformKind.Random,"Random 2",3) {
				Opens = new[] { "Treasure 1" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-27f),CruciblePlatformKind.Random,"Random 1",4) {
				Opens = new[] { "Fight 4" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-27f),CruciblePlatformKind.Fight,"Fight 3",4) {
				Opens = new[] { "Fight 4" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-27f),CruciblePlatformKind.Treasure,"Treasure 1",4) {
				Opens = new[] { "Fight 4" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-36f),CruciblePlatformKind.Fight,"Fight 4",5) {
				Opens = new[] { "Campsite 2" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-45f),CruciblePlatformKind.Campsite,"Campsite 2",6) {
				Opens = new string[2] { "Random 3", "Item Shop 2" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-45f),CruciblePlatformKind.Shop,"Item Shop 2",7) {
				Opens = new[] { "Treasure 3" }
			},
			new CruciblePlatform(new Vector3(-682f,0f,-45f),CruciblePlatformKind.Treasure,"Treasure 3",8) {
				Opens = new[] { "Fight 5" }
			},
			new CruciblePlatform(new Vector3(-682f,0f,-54f),CruciblePlatformKind.Fight,"Fight 5",9) {
				Opens = new[] { "Fight 7" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-45f),CruciblePlatformKind.Random,"Random 3",7) {
				Opens = new[] { "Treasure 4" }
			},
			new CruciblePlatform(new Vector3(-718f,0f,-45f),CruciblePlatformKind.Treasure,"Treasure 4",8) {
				Opens = new[] { "Fight 6" }
			},
			new CruciblePlatform(new Vector3(-718f,0f,-54f),CruciblePlatformKind.Fight,"Fight 6",9) {
				Opens = new[] { "Fight 7" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-54f),CruciblePlatformKind.Fight,"Fight 7",10) {
				Opens = new[] { "Item Shop 3" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-63f),CruciblePlatformKind.Shop,"Item Shop 3",11) {
				Opens = new string[3] { "Random 4", "Treasure 5", "Campsite 3" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-63f),CruciblePlatformKind.Treasure,"Treasure 5",12) {
				Opens = new[] { "Fight 8" }
			},
			new CruciblePlatform(new Vector3(-691f,0f,-72f),CruciblePlatformKind.Fight,"Fight 8",13) {
				Opens = new[] { "Final fight" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-63f),CruciblePlatformKind.Random,"Random 4",12) {
				Opens = new[] { "Random 5" }
			},
			new CruciblePlatform(new Vector3(-709f,0f,-72f),CruciblePlatformKind.Random,"Random 5",13) {
				Opens = new[] { "Final fight" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-72f),CruciblePlatformKind.Campsite,"Campsite 3",12) {
				Opens = new[] { "Final fight" }
			},
			new CruciblePlatform(new Vector3(-700f,0f,-81f),CruciblePlatformKind.FinalFight,"Final fight",14) {
				Opens = Array.Empty<string>()
			}
		}
	};
	public static bool HasMap(uint territory) {
		return Boards.ContainsKey(territory);
	}
	public static IReadOnlyList<CruciblePlatform> Platforms(uint territory) {
		if(!Boards.TryGetValue(territory,out IReadOnlyList<CruciblePlatform> value)) {
			return Array.Empty<CruciblePlatform>();
		}
		return value;
	}
	public static CruciblePlatform? Current(uint territory,Vector3 position) {
		foreach(CruciblePlatform item in Platforms(territory)) {
			if(item.Contains(position)) {
				return item;
			}
		}
		return null;
	}
	public static CruciblePlatform? Next(uint territory,Vector3 position,bool needsHealing) {
		IReadOnlyList<CruciblePlatform> readOnlyList=Platforms(territory);
		if(readOnlyList.Count==0) {
			return null;
		}
		CruciblePlatform cruciblePlatform=Current(territory,position) ?? Nearest(readOnlyList,position);
		if(cruciblePlatform==null) {
			return null;
		}
		IReadOnlyList<string> opens=cruciblePlatform.Opens;
		if(opens!=null) {
			List<CruciblePlatform> list=readOnlyList.Where((CruciblePlatform p) => opens.Contains(p.Label)).ToList();
			return list.Count switch {
				0 => null,
				1 => list[0],
				_ => (from platform in list
					orderby Preference(platform,needsHealing),DistanceTo(platform,position)
					select platform).First(),
			};
		}
		int nextStep=int.MaxValue;
		foreach(CruciblePlatform item in readOnlyList) {
			if(item.Step>cruciblePlatform.Step&&item.Step<nextStep) {
				nextStep=item.Step;
			}
		}
		if(nextStep==int.MaxValue) {
			return null;
		}
		List<CruciblePlatform> list2=readOnlyList.Where((CruciblePlatform p) => p.Step==nextStep).ToList();
		if(list2.Count==1) {
			return list2[0];
		}
		CruciblePlatformKind? leads=cruciblePlatform.Leads;
		if(leads.HasValue) {
			CruciblePlatformKind unlockedKind=leads.GetValueOrDefault();
			return list2.FirstOrDefault((CruciblePlatform platform) => platform.Kind==unlockedKind);
		}
		return (from platform in list2
			orderby Preference(platform,needsHealing),DistanceTo(platform,position)
			select platform).FirstOrDefault() ?? Nearest(list2,position);
	}
	public static bool OffersCampsite(uint territory,Vector3 position) {
		IReadOnlyList<CruciblePlatform> source=Platforms(territory);
		CruciblePlatform here=Current(territory,position);
		if(here==null||here.Leads.HasValue) {
			return false;
		}
		IReadOnlyList<string> opens=here.Opens;
		List<CruciblePlatform> list;
		if(opens!=null) {
			list=source.Where((CruciblePlatform p) => opens.Contains(p.Label)).ToList();
		}
		else {
			int nextStep=(from p in source
				where p.Step>here.Step
				select p.Step).DefaultIfEmpty(int.MaxValue).Min();
			list=((nextStep==int.MaxValue)?new List<CruciblePlatform>():source.Where((CruciblePlatform p) => p.Step==nextStep).ToList());
		}
		if(list.Count>1) {
			return list.Any((CruciblePlatform p) => (p.Leads ?? p.Kind)==CruciblePlatformKind.Campsite);
		}
		return false;
	}
	public static bool CampsiteLeadsToFinal(uint territory,Vector3 position) {
		IReadOnlyList<CruciblePlatform> source=Platforms(territory);
		CruciblePlatform here=Current(territory,position);
		if(here==null||here.Leads.HasValue) {
			return false;
		}
		IReadOnlyList<string> opens=here.Opens;
		List<CruciblePlatform> source2;
		if(opens!=null) {
			source2=source.Where((CruciblePlatform p) => opens.Contains(p.Label)).ToList();
		}
		else {
			int nextStep=(from p in source
				where p.Step>here.Step
				select p.Step).DefaultIfEmpty(int.MaxValue).Min();
			source2=((nextStep==int.MaxValue)?new List<CruciblePlatform>():source.Where((CruciblePlatform p) => p.Step==nextStep).ToList());
		}
		foreach(CruciblePlatform camp in source2.Where((CruciblePlatform p) => (p.Leads ?? p.Kind)==CruciblePlatformKind.Campsite)) {
			IReadOnlyList<string> campOpens=camp.Opens;
			if(((campOpens!=null)?source.Where((CruciblePlatform p) => campOpens.Contains(p.Label)):source.Where((CruciblePlatform p) => p.Step==camp.Step+1)).Any((CruciblePlatform p) => p.Kind==CruciblePlatformKind.FinalFight)) {
				return true;
			}
		}
		return false;
	}
	private static int Preference(CruciblePlatform platform,bool needsHealing) {
		CruciblePlatformKind cruciblePlatformKind=platform.Leads ?? platform.Kind;
		if(needsHealing) {
			return cruciblePlatformKind switch {
				CruciblePlatformKind.Campsite => 0,
				CruciblePlatformKind.Shop => 1,
				CruciblePlatformKind.Treasure => 2,
				CruciblePlatformKind.Random => 3,
				_ => 4,
			};
		}
		return cruciblePlatformKind switch {
			CruciblePlatformKind.Shop => 0,
			CruciblePlatformKind.Treasure => 1,
			CruciblePlatformKind.Random => 2,
			CruciblePlatformKind.Campsite => 3,
			_ => 4,
		};
	}
	public static CruciblePlatform? NearestTo(uint territory,Vector3 position) {
		return Nearest(Platforms(territory),position);
	}
	public static float DistanceTo(CruciblePlatform platform,Vector3 position) {
		return Vector2.Distance(new Vector2(position.X,position.Z),new Vector2(platform.Centre.X,platform.Centre.Z));
	}
	private static CruciblePlatform? Nearest(IReadOnlyList<CruciblePlatform> platforms,Vector3 position) {
		CruciblePlatform result=null;
		float num=float.MaxValue;
		foreach(CruciblePlatform platform in platforms) {
			float num2=Vector2.Distance(new Vector2(position.X,position.Z),new Vector2(platform.Centre.X,platform.Centre.Z));
			if(num2<num) {
				num=num2;
				result=platform;
			}
		}
		return result;
	}
}
