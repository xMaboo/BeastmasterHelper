using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
namespace Beastmaster;

public sealed class BeastmasterProgressService {
	private readonly BeastmasterConfiguration configuration;
	public string CurrentCharacterKey {
		get {
			if(!DalamudApi.ClientState.IsLoggedIn) {
				return string.Empty;
			}
			ulong contentId=DalamudApi.PlayerState.ContentId;
			if(contentId!=0L) {
				return contentId.ToString();
			}
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			if(localPlayer==null) {
				return string.Empty;
			}
			string text=localPlayer.HomeWorld.Value.Name.ExtractText();
			if(!string.IsNullOrWhiteSpace(text)) {
				return localPlayer.Name.TextValue+"@"+text;
			}
			return localPlayer.Name.TextValue;
		}
	}
	public string CurrentCharacterLabel {
		get {
			if(!DalamudApi.ClientState.IsLoggedIn) {
				return "Not logged in";
			}
			IPlayerCharacter localPlayer=DalamudApi.ObjectTable.LocalPlayer;
			if(localPlayer==null) {
				return "Character data is loading";
			}
			string text=localPlayer.HomeWorld.Value.Name.ExtractText();
			if(!string.IsNullOrWhiteSpace(text)) {
				return localPlayer.Name.TextValue+"@"+text;
			}
			return localPlayer.Name.TextValue;
		}
	}
	public BeastmasterProgressService(BeastmasterConfiguration configuration) {
		this.configuration=configuration;
	}
	public bool IsCompleted(string objectiveKey) {
		string currentCharacterKey=CurrentCharacterKey;
		if(currentCharacterKey.Length>0&&configuration.ProgressByCharacter.TryGetValue(currentCharacterKey,out BeastmasterCharacterProgress value)) {
			return value.CompletedObjectives.Contains(objectiveKey);
		}
		return false;
	}
	public void SetCompleted(string objectiveKey,bool completed) {
		string currentCharacterKey=CurrentCharacterKey;
		if(currentCharacterKey.Length==0) {
			return;
		}
		if(!configuration.ProgressByCharacter.TryGetValue(currentCharacterKey,out BeastmasterCharacterProgress value)) {
			if(!completed) {
				return;
			}
			value=new BeastmasterCharacterProgress();
			configuration.ProgressByCharacter[currentCharacterKey]=value;
		}
		if(completed?value.CompletedObjectives.Add(objectiveKey):value.CompletedObjectives.Remove(objectiveKey)) {
			configuration.Save();
		}
	}
	public int GetCompletedCount(BeastmasterStage stage) {
		return Enumerable.Count(stage.Objectives,(BeastmasterObjective objective) => IsCompleted(objective.Key));
	}
}
