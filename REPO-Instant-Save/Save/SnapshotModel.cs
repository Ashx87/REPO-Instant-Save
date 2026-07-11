using System.Collections.Generic;
using UnityEngine;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Plain data-transfer objects for an Instant Save snapshot. Serialized to JSON
    /// (Newtonsoft) and stored next to the run's .es3 so it survives quitting the game.
    /// Kept behaviour-free; all game interaction lives in WorldCapture / WorldRestore.
    /// </summary>
    public sealed class WorldSnapshot
    {
        public int version { get; set; } = 1;
        public string levelName { get; set; } = "";
        public long takenUtcTicks { get; set; }

        /// <summary>Every child under LevelGenerator.LevelParent (modules, connect/block objects).</summary>
        public List<SceneObjectDto> level { get; set; } = new();
        public List<ValuableDto> valuables { get; set; } = new();
        public List<ExtractionDto> extraction { get; set; } = new();
        public List<PlayerDto> players { get; set; } = new();
        public RoundDto round { get; set; } = new();
        public List<HingeDto> hinges { get; set; } = new();
        public GridDto grid { get; set; } = new();
    }

    public sealed class SceneObjectDto
    {
        public string name { get; set; } = "";
        /// <summary>"module", "connect", or "other".</summary>
        public string kind { get; set; } = "other";
        public Vec3Dto pos { get; set; } = new();
        public Vec3Dto euler { get; set; } = new();
        // Grid cell + module type — only meaningful for kind == "module" (cross-session rebuild).
        public int gridX { get; set; } = -1;
        public int gridY { get; set; } = -1;
        public int moduleType { get; set; } = -1;
    }

    /// <summary>The generated level grid (cross-session map rebuild).</summary>
    public sealed class GridDto
    {
        public bool present { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public List<TileDto> tiles { get; set; } = new();
    }

    public sealed class TileDto
    {
        public int x { get; set; }
        public int y { get; set; }
        public bool active { get; set; }
        public bool first { get; set; }
        public int connections { get; set; }
        public bool cTop { get; set; }
        public bool cRight { get; set; }
        public bool cBot { get; set; }
        public bool cLeft { get; set; }
        public int type { get; set; }
    }

    public sealed class ValuableDto
    {
        public string name { get; set; } = "";
        /// <summary>"valuable", "item", or "cart".</summary>
        public string kind { get; set; } = "valuable";
        public Vec3Dto pos { get; set; } = new();
        public Vec3Dto euler { get; set; } = new();
        public float value { get; set; }
    }

    public sealed class ExtractionDto
    {
        public string name { get; set; } = "";
        public Vec3Dto pos { get; set; } = new();
        public int haulCurrent { get; set; }
        public int haulGoal { get; set; }
        /// <summary>ExtractionPoint.State as int (None/Idle/Active/…/Complete).</summary>
        public int state { get; set; }
    }

    /// <summary>RoundDirector haul/extraction accounting so extractions can be un-completed.</summary>
    public sealed class RoundDto
    {
        public bool present { get; set; }
        public int currentHaul { get; set; }
        public int totalHaul { get; set; }
        public int currentHaulMax { get; set; }
        public int haulGoal { get; set; }
        public int haulGoalMax { get; set; }
        public int extractionPoints { get; set; }
        public int extractionPointSurplus { get; set; }
        public int extractionPointsCompleted { get; set; }
        public int extractionHaulGoal { get; set; }
        public bool extractionPointActive { get; set; }
        public bool allExtractionPointsCompleted { get; set; }
    }

    /// <summary>A door/drawer hinge: its leaf transform plus open/broken flags.</summary>
    public sealed class HingeDto
    {
        public string name { get; set; } = "";
        /// <summary>The fixed pivot point — used to match hinges (the leaf moves, the pivot doesn't).</summary>
        public Vec3Dto pivot { get; set; } = new();
        public Vec3Dto pos { get; set; } = new();
        public Vec3Dto euler { get; set; } = new();
        public bool closed { get; set; }
        public bool broken { get; set; }
        public bool dead { get; set; }
    }

    public sealed class PlayerDto
    {
        public string steamId { get; set; } = "";
        public string name { get; set; } = "";
        public Vec3Dto pos { get; set; } = new();
        public Vec3Dto euler { get; set; } = new();
        public int health { get; set; }
    }

    /// <summary>Serializable Vector3. Newtonsoft can't round-trip Unity's struct cleanly.</summary>
    public sealed class Vec3Dto
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public Vec3Dto() { }

        public Vec3Dto(Vector3 v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3 ToVector3() => new(x, y, z);
    }
}
