using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    #region Fields
    public MapGenerator mapGenerator;

    private Dictionary<Vector2Int, NPCGroup> npcGroupsByPosition = new Dictionary<Vector2Int, NPCGroup>();
    private Dictionary<Vector2Int, List<NPC>> npcsByCell = new Dictionary<Vector2Int, List<NPC>>();
    private List<NPCGroup> activeNPCGroups = new List<NPCGroup>();
    private List<NPC> allNPCs = new List<NPC>();
    private readonly HashSet<int> loggedMissingNeedNoops = new HashSet<int>();
    #endregion

    #region Singleton
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region NPC Registration
	public void RegisterNPC(NPC npc)
	{
		EnsureRegistered(npc);
	}

	public void UnregisterNPC(NPC npc)
	{
		EnsureDeregistered(npc);
	}


    public void RegisterNPCGroup(NPCGroup group, Vector2Int position)
    {
        if (npcGroupsByPosition.ContainsValue(group))
        {
            var oldPosition = npcGroupsByPosition.FirstOrDefault(x => x.Value == group).Key;
            npcGroupsByPosition.Remove(oldPosition);
            Debug.Log($"Moved NPC Group from {oldPosition} to {position}.");
        }

        npcGroupsByPosition[position] = group;
        Debug.Log($"Registered NPC Group at {position}.");

        mapGenerator.map[position.x, position.y].isNPCGroupPresent = true;
    }

    public void UnregisterNPCGroup(NPCGroup group)
    {
        activeNPCGroups.Remove(group);
    }
	
		private void EnsureRegistered(NPC npc)
	{
		if (npc == null) return;
		if (!allNPCs.Contains(npc)) allNPCs.Add(npc);

		if (!TurnOrchestrator.Instance.IsCharacterRegistered(npc))
		{
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogEvent("[REGISTRATION]", "NPCManager.EnsureRegistered before TurnOrchestrator.RegisterCharacter", null, npc);
			TurnOrchestrator.Instance.RegisterCharacter(npc);
			GameDebugger.Instance.LogInfo($"NPCManager: Registered '{npc.Name}' with TurnOrchestrator.");
		}
	}

	private void EnsureDeregistered(NPC npc)
	{
		if (npc == null) return;
		allNPCs.Remove(npc);

		if (TurnOrchestrator.Instance.IsCharacterRegistered(npc))
		{
			// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
			TurnDiagnosticsLogger.LogEvent("[DEREGISTRATION]", "NPCManager.EnsureDeregistered before TurnOrchestrator.DeregisterCharacter", null, npc);
			TurnOrchestrator.Instance.DeregisterCharacter(npc);
			GameDebugger.Instance.LogInfo($"NPCManager: Deregistered '{npc.Name}' from TurnOrchestrator.");
		}
	}

	
    #endregion

    #region NPC Updates
    public void UpdateNPCTurn(NPC npc)
    {
        while (npc.RemainingTurnTime > 0)
        {
            if (npc.Status == NPCStatus.Hostile)
            {
                npc.RemainingTurnTime -= 1.0f; // Assuming attacking takes a full turn
            }
            else
            {
                npc.Move(npc.DirectionFacing);
                npc.RemainingTurnTime -= 1.0f; // Assuming moving takes a full turn
            }

            if (npc.RemainingTurnTime < 0)
            {
                npc.RemainingTurnTime += 1.0f; // Reset for next turn
            }
        }
    }

    public void UpdateNPCGroupPosition(NPCGroup group, Vector2Int oldPosition, Vector2Int newPosition)
    {
        if (npcGroupsByPosition.ContainsKey(oldPosition))
        {
            npcGroupsByPosition.Remove(oldPosition);
        }
        npcGroupsByPosition[newPosition] = group;
    }

    public void UpdateNPCPosition(NPC npc, Vector2Int oldPosition, Vector2Int newPosition)
    {
        if (npcsByCell.ContainsKey(oldPosition))
        {
            npcsByCell[oldPosition].Remove(npc);
            if (npcsByCell[oldPosition].Count == 0)
            {
                npcsByCell.Remove(oldPosition);
            }
        }

        if (!npcsByCell.ContainsKey(newPosition))
        {
            npcsByCell[newPosition] = new List<NPC>();
        }
        npcsByCell[newPosition].Add(npc);
    }
    #endregion

    #region NPC Group Management
	public void UpdateGroupMembership(NPCGroup group)
	{
		foreach (var npc in group.NPCs.ToList())
		{
			if (!npc.IsActive)
			{
				group.NPCs.Remove(npc);
				Debug.Log($"NPC '{npc.Name}' removed from group '{group.GroupName}' due to inactivity.");
				EnsureDeregistered(npc); // was TurnOrchestrator.Instance.DeregisterCharacter(npc)
			}
		}
	}


    public void UpdateNPCGroupStatus(NPCGroup group)
    {
        if (group.NPCs.All(npc => !npc.IsActive))
        {
            group.IsActive = false;
            UnregisterNPCGroup(group);
            Debug.Log($"NPC Group '{group.GroupName}' is now inactive.");
        }
    }

    public void CheckAndRegisterActiveNPCGroups()
    {
        foreach (var kvp in npcGroupsByPosition)
        {
            NPCGroup group = kvp.Value;
            if (!activeNPCGroups.Contains(group) && group.IsActive)
            {
                activeNPCGroups.Add(group);
                Debug.Log($"NPC Group '{group.GroupName}' added to active groups.");
            }
        }
    }
    #endregion

    #region NPC Movements and Nested Areas
    public void ProcessNPCMovements()
    {
        Debug.Log($"Processing NPC Movements for {activeNPCGroups.Count} groups");

        foreach (var group in activeNPCGroups.ToList())
        {
            UpdateGroupMembership(group);
            UpdateNPCGroupStatus(group);

            if (group.IsInNestedArea && group.CurrentNestedArea != null)
            {
                RemoveNPCsFromNestedArea(group);
            }

            Vector2Int oldPosition = group.Position;
            group.Move(mapGenerator);
            UpdateGroupPositionAndFlags(group, oldPosition, group.Position);

            Debug.Log($"NPC Group '{group.GroupName}' moved from {oldPosition} to {group.Position}");
        }
    }

    public void RemoveNPCsFromNestedArea(NPCGroup group)
{
    if (group.IsInNestedArea && group.CurrentNestedArea != null)
    {
        var nestedArea = group.CurrentNestedArea;
        var nestedMap = nestedArea.GetNestedMap();

        foreach (var npc in group.NPCs)
        {
            GameDebugger.Instance.LogInfo($"Removing NPC '{npc.Name}' from nested area.");

            if (npc.NestedMapPosition.x >= 0 && npc.NestedMapPosition.x < nestedMap.GetLength(0) &&
                npc.NestedMapPosition.y >= 0 && npc.NestedMapPosition.y < nestedMap.GetLength(1))
            {
                Cell cell = nestedMap[npc.NestedMapPosition.x, npc.NestedMapPosition.y];
                cell.isNPCPresent = false;
                cell.isPassable = true;
                cell.Objects.Remove(npc);
            }

            // helper instead of direct call
            // CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
            TurnDiagnosticsLogger.LogEvent("[ENTITY REMOVAL]", "NPCManager.RemoveNPCsFromNestedArea removing NPC", null, npc);
            EnsureDeregistered(npc);
        }

        group.IsInNestedArea = false;
        group.CurrentNestedArea = null;
        GameDebugger.Instance.LogInfo($"NPCs removed from nested area for group '{group.GroupName}'");
    }
    else
    {
        Debug.LogWarning($"Group '{group.GroupName}' is not in a nested area.");
    }
}

private void TransferNPCGroupToNestedArea(NPCGroup npcGroup, INestedArea nestedArea, Vector2Int nestedPosition)
{
    foreach (NPC npc in npcGroup.NPCs)
    {
        Vector2Int p = DetermineNPCPositionInNestedArea(nestedArea);
        if (nestedArea.IsValidPosition(p) && nestedArea.IsPassable(p))
        {
            nestedArea.UpdateCharacterPosition(npc, p);
            UpdateNPCPosition(npc, npc.Position, p);
            EnsureRegistered(npc); // was RegisterCharacter
            GameDebugger.Instance.LogInfo($"NPC {npc.Name} moved into nested area and registered.");
        }
        else
        {
            Debug.LogError("Unable to transfer NPC to nested area due to collision or invalid position.");
        }
    }
}
    private void UpdateGroupPositionAndFlags(NPCGroup group, Vector2Int oldPosition, Vector2Int newPosition)
    {
        if (mapGenerator.map[oldPosition.x, oldPosition.y].isNPCGroupPresent)
        {
            mapGenerator.map[oldPosition.x, oldPosition.y].isNPCGroupPresent = false;
        }

        mapGenerator.map[newPosition.x, newPosition.y].isNPCGroupPresent = true;
        UpdateNPCGroupPosition(group, oldPosition, newPosition);
    }

    public void PlaceNPC(INestedArea nestedArea, NPC npc)
    {
        GameDebugger.Instance.LogInfo($"Placing NPC '{npc.Name}' in nested area: {nestedArea.NestedAreaID}");
        Vector2Int npcPosition = DetermineNPCPositionInNestedArea(nestedArea);

        int attempts = 0;
        while (!nestedArea.IsValidPosition(npcPosition) || !nestedArea.IsPassable(npcPosition) || HasCollision(nestedArea, npcPosition))
        {
            npcPosition = AdjustNPCPosition(nestedArea, npcPosition);
            attempts++;
            if (attempts > 5)
            {
                Debug.LogError($"Failed to place '{npc.Name}' after {attempts} attempts.");
                return;
            }
        }

        nestedArea.UpdateCharacterPosition(npc, npcPosition);
        npc.NestedMapPosition = npcPosition;
        npc.IsInNestedArea = true;
        npc.CurrentNestedArea = nestedArea;
        Debug.Log($"'{npc.Name}' placed at {npcPosition} within nested area.");
		// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
		TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "NPCManager.PlaceNPC placed NPC in nested area", $"NestedArea: {nestedArea?.Name} ({nestedArea?.NestedAreaID})", npc);

		EnsureRegistered(npc);
    }

		public void PlaceNPCs(INestedArea nestedArea, NPCGroup npcGroup)
		{
			GameDebugger.Instance.LogInfo($"Placing NPCs for group '{npcGroup.GroupName}'");
			npcGroup.IsInNestedArea = true;
			npcGroup.CurrentNestedArea = nestedArea;

			foreach (NPC npc in npcGroup.NPCs)
			{
				Vector2Int npcPosition = DetermineNPCPositionInNestedArea(nestedArea);
				int attempts = 0;
				while (!nestedArea.IsValidPosition(npcPosition) || !nestedArea.IsPassable(npcPosition) || HasCollision(nestedArea, npcPosition))
				{
					GameDebugger.Instance.LogInfo($"Adjusting '{npc.Name}' due to collision/invalid.");
					npcPosition = AdjustNPCPosition(nestedArea, npcPosition);
					if (++attempts > 5)
					{
						Debug.LogError($"Failed to place '{npc.Name}' after {attempts} attempts. Skipping.");
						goto NextNPC;
					}
				}

				nestedArea.UpdateCharacterPosition(npc, npcPosition);
				UpdateNPCPosition(npc, npc.Position, npcPosition);
				npc.NestedMapPosition = npcPosition;
				npc.IsInNestedArea = true;
				npc.CurrentNestedArea = nestedArea;

				GameDebugger.Instance.LogInfo($"'{npc.Name}' placed at {npcPosition} within nested area.");
				// CODEXLOG001_TURNLIFECYCLE: temporary turn lifecycle diagnostic call.
				TurnDiagnosticsLogger.LogEvent("[AREA ENTRY]", "NPCManager.PlaceNPCs placed NPC group member in nested area", $"NestedArea: {nestedArea?.Name} ({nestedArea?.NestedAreaID})\nGroup: {npcGroup.GroupName}", npc);

				// Use the helper (idempotent)
				EnsureRegistered(npc);

				NextNPC: ;
			}
		}


		public void PlaceVillageNPCs(Cell parentCell, INestedArea nestedArea)
		{
			if (parentCell == null || nestedArea == null) return;
			CallTrace.Mark(this);

			// If your project uses Village/AvailableVillageNPCs, keep that logic:
			var village = nestedArea as Village;
			if (village == null || village.VillageNPCs.Count == 0) return;

			foreach (var npc in village.AvailableVillageNPCs)
			{
				if (!IsNPCInNestedArea(npc, nestedArea))
				{
					npc.IsInVillage = true;
					PlaceNPC(nestedArea, npc); // PlaceNPC already guards registration
				}
			}

			// Optional audit (I’d usually move this to the coordinator)
			var reg = TurnOrchestrator.Instance.GetRegisteredCharacters();
			GameDebugger.Instance.LogInfo($"NPCManager: Village placed. Registered count = {reg.Count}");
		}


	public void PlaceNPCGroupInNestedArea(Cell parentCell, INestedArea nestedArea)
	{
		if (parentCell == null || nestedArea == null) return;
		CallTrace.Mark(this);

		var group = FindNPCGroupAtPosition(parentCell.Coordinates);
		if (group == null || !group.IsActive)
		{
			GameDebugger.Instance.LogInfo("NPCManager: No active group to place at this position.");
			return;
		}

		PlaceNPCs(nestedArea, group);       // uses your existing per-NPC placement
		UpdateGroupMembership(group);       // clean inactive members
		UpdateNPCGroupStatus(group);        // keep flags honest
		GameDebugger.Instance.LogInfo($"NPCManager: Placed NPC group '{group.GroupName}' in NestedArea {nestedArea.NestedAreaID}");
	}



    private Vector2Int DetermineNPCPositionInNestedArea(INestedArea nestedArea)
    {
        Cell[,] nestedMap = nestedArea.GetNestedMap();
        int width = nestedMap.GetLength(0);
        int height = nestedMap.GetLength(1);

        Vector2Int position;
        do
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            position = new Vector2Int(x, y);
        } while (!nestedArea.IsValidPosition(position) || !nestedArea.IsPassable(position) || HasCollision(nestedArea, position));

        return position;
    }

    private Vector2Int AdjustNPCPosition(INestedArea nestedArea, Vector2Int npcPosition)
    {
        int width = nestedArea.GetNestedMap().GetLength(0);
        int height = nestedArea.GetNestedMap().GetLength(1);

        int offsetX = Random.Range(-1, 2);
        int offsetY = Random.Range(-1, 2);
        Vector2Int adjustedPosition = npcPosition + new Vector2Int(offsetX, offsetY);

        adjustedPosition.x = Mathf.Clamp(adjustedPosition.x, 0, width - 1);
        adjustedPosition.y = Mathf.Clamp(adjustedPosition.y, 0, height - 1);

        return adjustedPosition;
    }

    public bool HasCollision(INestedArea nestedArea, Vector2Int position)
    {
        Cell cell = nestedArea.GetCellAtPosition(position);
        return cell != null && !cell.isPassable;
    }

    public bool IsNPCInNestedArea(NPC npc, INestedArea nestedArea)
    {
        return nestedArea.GetAllNPCsInArea().Contains(npc);
    }

    public void UpdateNPCsInNestedArea(INestedArea nestedArea)
    {
        List<NPC> npcsInArea = nestedArea.GetAllNPCsInArea();
        foreach (NPC npc in npcsInArea)
        {
            npc.UpdateLineOfSight();
            npc.Move(npc.DirectionFacing);

            if (npc.Status == NPCStatus.Hostile)
            {
                Debug.Log("Error Space UpdateNPCsInNestedAra");
            }
        }
    }
    #endregion

    #region NPC Retrieval
    public NPCGroup FindNPCGroupAtPosition(Vector2Int position)
    {
        if (npcGroupsByPosition.TryGetValue(position, out NPCGroup group))
        {
            return group;
        }
        return null;
    }

    public List<NPC> FindNPCsAtCell(Vector2Int cellPosition)
    {
        if (npcsByCell.TryGetValue(cellPosition, out List<NPC> npcs))
        {
            return npcs.Where(npc => npc.IsActive).ToList();
        }
        return new List<NPC>();
    }

    public NPC GetNPCByID(int npcID)
    {
        return PermaLists.Instance.AllNPCs.FirstOrDefault(npc => npc.NPCID == npcID);
    }
    #endregion

    #region Utility
    private float CalculateTurnDuration(float speed)
    {
        // Example calculation for turn duration based on speed
        return Mathf.Max(0.1f, 1.0f / speed);
    }

    public void UpdateNeedsForNPCs()
    {
        // Assuming PermaLists.Instance.AllNPCs is a list of all NPCs in the game
        List<NPC> allNPCs = PermaLists.Instance.AllNPCs;

        // Check if allNPCs is null
        if (allNPCs == null)
        {
            Debug.LogError("PermaLists.Instance.AllNPCs is null.");
            return;
        }

        // Iterate over each NPC in the list
        foreach (NPC npc in allNPCs)
        {
            // Check if npc is null before proceeding
            if (npc == null)
            {
                Debug.LogWarning("Encountered a null NPC reference in the list.");
                continue;
            }

            // Clear current need
            ClearNPCNeed(npc);

            // Generate a random number to decide whether to update the need
            int randomDecision = UnityEngine.Random.Range(0, 2); // Generates 0 or 1

            if (randomDecision == 1)
            {
                // Update the need for each NPC
                UpdateNPCNeed(npc);
            }
        }
    }

    public void ClearNPCNeed(NPC npc)
    {
        if (npc == null)
        {
            Debug.LogWarning("Attempted to clear need for null NPC.");
            return;
        }

        // Check if the NPC exists and has a current need
        if (npc.CurrentNeed != null)
        {
            // Clear the NPC's current need
            npc.CurrentNeed = null;
            loggedMissingNeedNoops.Remove(npc.NPCID);

            // Optionally, log the action for debugging or auditing purposes
            Debug.Log($"Cleared need for NPC '{npc.Name}' (ID: {npc.NPCID}).");
        }
        else
        {
            if (loggedMissingNeedNoops.Add(npc.NPCID))
            {
                string trackedInPermaList = (PermaLists.Instance != null && PermaLists.Instance.AllNPCs != null)
                    ? PermaLists.Instance.AllNPCs.Contains(npc).ToString()
                    : "NULL";
                // CODEXLOG001_TURNLIFECYCLE: temporary NPC needs idempotency diagnostic.
                TurnDiagnosticsLogger.LogEvent("[NPC NEEDS]", "NPCManager.ClearNPCNeed no-op for valid NPC with no active need",
                    $"NPC.Name: {npc.Name}\n" +
                    $"NPC.ID: {npc.NPCID}\n" +
                    $"IsActive: {npc.IsActive}\n" +
                    $"IsAlive: {npc.IsAlive}\n" +
                    $"CurrentNestedArea: {npc.CurrentNestedArea?.Name ?? "NULL"}\n" +
                    $"IsInNestedArea: {npc.IsInNestedArea}\n" +
                    $"TrackedInPermaList: {trackedInPermaList}\n" +
                    "Result: no-op");
            }
        }
    }

    public void UpdateNPCNeed(NPC npc)
    {
        // Check if npc is null
        if (npc == null)
        {
            Debug.LogError("NPC is null.");
            return;
        }

        if (!npc.IsAlive) return;

        if (npc.CurrentNeed == null || !npc.CurrentNeed.HasNeed)
        {
            // Find the role data for the NPC's role
            NPCRoleData roleData = PermaLists.Instance.RoleData?.FirstOrDefault(rd => rd.Role == npc.Role);

            if (roleData == null)
            {
                Debug.LogWarning($"No role data found for role {npc.Role} or RoleData list is null.");
                return;
            }

            // Generate a random decision to choose an item based on role data or a completely random item
            float itemDecision = UnityEngine.Random.Range(0f, 1f); // Generates a float between 0.0 and 1.0

            if (itemDecision <= 0.9f)
            {
                if (roleData != null)
                {
                    // Find items matching the frequent needs of the role
                    List<ItemCreationData> possibleNeeds = new List<ItemCreationData>();
                    foreach (var need in roleData.FrequentNeeds)
                    {
                        possibleNeeds.AddRange(
                            PermaLists.Instance.ItemCreationData?.Where(item => item.ItemTypes.Contains((ItemType)System.Enum.Parse(typeof(ItemType), need)))
                        );
                    }

                    // Select a random item from the possible needs
                    if (possibleNeeds.Count > 0)
                    {
                        var chosenItem = possibleNeeds[Random.Range(0, possibleNeeds.Count)];

                        // If the item is Fruit or Vegetable, apply region-based selection logic
                        if (chosenItem.ItemTypes.Contains(ItemType.Fruit) || chosenItem.ItemTypes.Contains(ItemType.Vegetable))
                        {
                            HandleFruitOrVegetableNeed(npc, chosenItem);
                        }
                        else
                        {
                            // Proceed with normal need assignment
                            AssignNeedToNPC(npc, chosenItem);
                        }
                    }
                    else
                    {
                        Debug.Log($"NPC {npc.Name} currently has no specific needs.");
                    }
                }
                else
                {
                    Debug.LogWarning($"No role data found for role {npc.Role}.");
                }
            }
            else
            {
                // Handle random item case
                HandleRandomItemNeed(npc);
            }
        }
    }

    // Handles assigning fruits or vegetables based on region
    private void HandleFruitOrVegetableNeed(NPC npc, ItemCreationData chosenItem)
    {
        RegionInfo npcRegion = RegionManager.Instance.GetRegionInfo(npc.RegionNumber);
        if (npcRegion == null)
        {
            Debug.LogWarning($"NPC {npc.Name} is in an invalid region ({npc.RegionNumber}).");
            return;
        }

        float regionSelectionChance = UnityEngine.Random.Range(0f, 1f);
        string fruitOrVeg = null;

        if (regionSelectionChance <= 0.7f) // 70% chance for opposite region fruit/veg
        {
            var oppositeDirection = RegionManager.Instance.GetOppositeCompassDirection(npcRegion.CompassDirection);
            var oppositeRegion = RegionManager.Instance.GetRegionsByDirection(oppositeDirection).FirstOrDefault();

            if (oppositeRegion != null)
            {
                fruitOrVeg = RegionManager.Instance.GetNativeFruitOrVegetable(oppositeRegion, chosenItem.ItemTypes.First());
            }
        }
        else if (regionSelectionChance <= 0.9f) // 20% chance for same region fruit/veg
        {
            fruitOrVeg = RegionManager.Instance.GetNativeFruitOrVegetable(npcRegion, chosenItem.ItemTypes.First());
        }
        else // 10% chance for random fruit/veg (as it is doing now)
        {
            var allItems = PermaLists.Instance.ItemCreationData;
            if (allItems != null && allItems.Count > 0)
            {
                var randomItem = allItems[Random.Range(0, allItems.Count)];
                fruitOrVeg = randomItem.Name;
            }
        }

        if (fruitOrVeg != null)
        {
            // Determine the number of items needed (1 to 3)
            int numberRequired = Random.Range(1, 4);

            // Determine if the reward is financial or a favour
            bool isFinance = Random.Range(0, 2) == 0;
            bool isFavour = !isFinance;

            // Assign the need to the NPC
            npc.CurrentNeed = new Need(true, fruitOrVeg, numberRequired, isFinance, isFavour);
            Debug.Log($"NPC {npc.Name} now needs {numberRequired} {fruitOrVeg}(s) and will reward with {(isFinance ? "Finance" : "Favour")}.");
        }
    }

    private void AssignNeedToNPC(NPC npc, ItemCreationData chosenItem)
    {
        int numberRequired = Random.Range(1, 4);
        bool isFinance = Random.Range(0, 2) == 0;
        bool isFavour = !isFinance;

        npc.CurrentNeed = new Need(true, chosenItem.Name, numberRequired, isFinance, isFavour);
        Debug.Log($"NPC {npc.Name} now needs {numberRequired} {chosenItem.Name}(s) and will reward with {(isFinance ? "Finance" : "Favour")}.");
    }

    private void HandleRandomItemNeed(NPC npc)
    {
        var allItems = PermaLists.Instance.ItemCreationData;
        if (allItems != null && allItems.Count > 0)
        {
            var randomItem = allItems[Random.Range(0, allItems.Count)];
            AssignNeedToNPC(npc, randomItem);
        }
    }


    #endregion  
}
