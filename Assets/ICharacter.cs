using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICharacter
{
    string CharacterName { get; set; }
    ICharacter Spouse { get; set; }
    List<ICharacter> Ancestors { get; set; }

}
