using System.Collections.Generic;

public class CharacterRelationship
{
    public int SourceCharacterId;
    public int TargetCharacterId;
    public int Attitude;
    public int Fear;
    public int Trust;
    public bool ActiveHostility;
    public string Reason;
    public int EstablishedDay;
    public int LastUpdatedDay;
    public List<string> Tags = new List<string>();
}
