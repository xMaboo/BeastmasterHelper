using System;
using System.Collections.Generic;
namespace Beastmaster;

[Serializable]
public sealed class QuickToggleBinding {
	public string Panel { get; set; }=string.Empty;
	public bool Show { get; set; }=true;
	public List<int> Keys { get; set; }=new List<int>();
}
