using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    public class NPCGroup
    {
        public string GroupName { get; set; }
        public virtual char Symbol { get; set; } = 'N'; // Default symbol, override in subclasses
        public virtual string Color { get; set; } = "#FFFFFF"; // Default color, override in subclasses
        public List<NPC> NPCs { get; private set; }
        public Vector2Int Position { get; set; }
        public Vector2Int PreviousPosition { get; set; }
        public bool IsActive { get; set; }
        public bool IsInNestedArea { get; set; } // Add this property
        public INestedArea CurrentNestedArea { get; set; } // Add this property
        public bool IsHostile { get; set; }
        private IMovementStrategy movementStrategy;
        public MapGenerator mapGenerator;


        public void SetMovementStrategy(IMovementStrategy strategy)
        {
            movementStrategy = strategy;
        }


        public void Move(MapGenerator mapGenerator)
        {
            if (Position != Vector2Int.zero)
            {
                PreviousPosition = Position;
            }

            if (movementStrategy != null)
            {
                Vector2Int nextPosition = movementStrategy.DetermineNextMove(Position);

                // Check if the next position is within bounds and passable
                if (IsValidPosition(nextPosition))
                {
                    Position = nextPosition; // Update position only if the next cell is valid and passable
                }
                else
                {
                    Position = PreviousPosition;
                }
            }
        }


        bool IsValidPosition(Vector2Int position)
        {
            return mapGenerator.IsPositionValid(position);
        }





        public NPCGroup(string groupName, MapGenerator mapGenerator)
        {
            GroupName = groupName;
            NPCs = new List<NPC>();
            IsHostile = false;
            this.mapGenerator = mapGenerator;
        }

        public void AddNPC(NPC npc)
        {
            NPCs.Add(npc);
        }

        public void RemoveNPC(NPC npc)
        {
            NPCs.Remove(npc);
        }

    }


    public class Bandit : NPC
    {
        public override char Symbol { get; set; } = 'B';
        public override string Color { get; set; } = "#FF0000";


        // Parameterless constructor
        public Bandit() { }

    }

    public class BanditGroup : NPCGroup
    {
        public BanditGroup(MapGenerator mapGenerator) : base("Bandit Group", mapGenerator)
        {
            SetMovementStrategy(new PatrollingEastWestMovement());
            IsActive = true; // Ensure the group is active upon creation
        }

        public override char Symbol { get; set; } = 'B';
        public override string Color { get; set; } = "#FF0000";
    }

    public class TraderGroup : NPCGroup
    {
        public Vector2Int Destination { get; set; } // Traders have a specific destination

        // Modify the constructor to pass a groupName to the base NPCGroup constructor
        public TraderGroup(string groupName, Vector2Int destination, MapGenerator mapGenerator) : base(groupName, mapGenerator)
        {
            Destination = destination;
            SetMovementStrategy(new StraightLineMovement());
        }
    }

    public interface IMovementStrategy
    {
        Vector2Int DetermineNextMove(Vector2Int currentPosition, Vector2Int destination = default);
}

public class StraightLineMovement : IMovementStrategy
{
    public Vector2Int DetermineNextMove(Vector2Int currentPosition, Vector2Int destination)
    {
        // Convert Vector2Int to Vector2 for normalization
        Vector2 direction = ((Vector2)(destination - currentPosition)).normalized;

        // Then convert back to Vector2Int after determining the direction
        return currentPosition + new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
    }
}

public class RandomRoamingMovement : IMovementStrategy
    {
        private Vector2Int lastDirection = Vector2Int.zero;
        private int stepsTaken = 0;
        private const int StepsBeforeChangingDirection = 2;

        public Vector2Int DetermineNextMove(Vector2Int currentPosition, Vector2Int destination = default)
        {
            if (stepsTaken >= StepsBeforeChangingDirection || lastDirection == Vector2Int.zero)
            {
                lastDirection = new Vector2Int(Random.Range(-1, 2), Random.Range(-1, 2)); // Random direction
                stepsTaken = 0;
            }
            stepsTaken++;
            return currentPosition + lastDirection;
        }


    }

    public class PatrollingNorthSouthMovement : IMovementStrategy
    {
        private int stepsTaken = 0;
        private const int StepsBeforeChangingDirection = 3;
        private int direction = 1; // 1 for moving north, -1 for moving south
        private bool waiting = false;

        public Vector2Int DetermineNextMove(Vector2Int currentPosition, Vector2Int destination = default)
        {
            if (waiting)
            {
                waiting = false;
                return currentPosition; // Stay in the same position for one turn
            }

            stepsTaken++;
            if (stepsTaken >= StepsBeforeChangingDirection)
            {
                direction *= -1; // Change direction
                stepsTaken = 0;
                waiting = true; // Start waiting
            }
            return currentPosition + new Vector2Int(0, direction);
        }
    }

    public class PatrollingEastWestMovement : IMovementStrategy
    {
        private int stepsTaken = 0;
        private const int StepsBeforeChangingDirection = 3;
        private int direction = 1; // 1 for moving east, -1 for moving west
        private bool waiting = false;

        public Vector2Int DetermineNextMove(Vector2Int currentPosition, Vector2Int destination = default)
        {
            if (waiting)
            {
                waiting = false;
                return currentPosition; // Stay in the same position for one turn
            }

            stepsTaken++;
            if (stepsTaken >= StepsBeforeChangingDirection)
            {
                direction *= -1; // Change direction
                stepsTaken = 0;
                waiting = true; // Start waiting
            }
            return currentPosition + new Vector2Int(direction, 0);
        }
    }


