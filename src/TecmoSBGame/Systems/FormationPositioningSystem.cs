using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Handles formation positioning for offensive and defensive plays.
/// Converts formation data from YAML into actual player positions on the field.
/// </summary>
public class FormationPositioningSystem
{
    private readonly World _world;
    
    // Standard field positions (in NES 256x224 coordinates)
    private const int LineOfScrimmageX = 128; // Center of field horizontally
    private const int StandardY = 112;        // Center vertically
    
    // Formation offsets (in pixels)
    private const int QBDepth = 15;           // QB stands 15 yards behind LOS
    private const int RBDepth = 25;           // RB is deeper
    private const int WRSpread = 40;          // WRs spread wide
    private const int OLSpacing = 8;          // Spacing between linemen

    public FormationPositioningSystem(World world)
    {
        _world = world;
    }

    /// <summary>
    /// Positions offensive players in a standard Pro formation.
    /// </summary>
    public void PositionProFormation(List<int> playerIds, int teamIndex, bool facingRight)
    {
        int direction = facingRight ? 1 : -1;
        int losX = LineOfScrimmageX;
        
        // Find player roles (simplified - would use actual player positions from data)
        var qb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "QB");
        var rb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "RB");
        var wr1 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "WR1");
        var wr2 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "WR2");
        var te = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "TE");
        var linemen = playerIds.Where(id => IsLineman(GetPlayerPosition(id))).ToList();

        // QB under center
        if (qb != 0)
            SetPosition(qb, new Vector2(losX - QBDepth * direction, StandardY));

        // RB offset behind QB
        if (rb != 0)
            SetPosition(rb, new Vector2(losX - RBDepth * direction, StandardY + 10));

        // WRs split wide
        if (wr1 != 0)
            SetPosition(wr1, new Vector2(losX - 5 * direction, StandardY - WRSpread));
        if (wr2 != 0)
            SetPosition(wr2, new Vector2(losX - 5 * direction, StandardY + WRSpread));

        // TE on the line
        if (te != 0)
            SetPosition(te, new Vector2(losX, StandardY + 25));

        // Offensive line
        PositionOffensiveLine(linemen, losX, StandardY, direction);
    }

    /// <summary>
    /// Positions players in a Shotgun formation.
    /// </summary>
    public void PositionShotgunFormation(List<int> playerIds, int teamIndex, bool facingRight)
    {
        int direction = facingRight ? 1 : -1;
        int losX = LineOfScrimmageX;
        
        var qb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "QB");
        var rb1 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "RB1");
        var rb2 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "RB2");
        var wr1 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "WR1");
        var wr2 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "WR2");
        var wr3 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "WR3");
        var linemen = playerIds.Where(id => IsLineman(GetPlayerPosition(id))).ToList();

        // QB in shotgun (deeper)
        if (qb != 0)
            SetPosition(qb, new Vector2(losX - (QBDepth + 10) * direction, StandardY));

        // RBs split beside QB
        if (rb1 != 0)
            SetPosition(rb1, new Vector2(losX - RBDepth * direction, StandardY - 15));
        if (rb2 != 0)
            SetPosition(rb2, new Vector2(losX - RBDepth * direction, StandardY + 15));

        // 3 WRs in spread
        if (wr1 != 0)
            SetPosition(wr1, new Vector2(losX, StandardY - WRSpread));
        if (wr2 != 0)
            SetPosition(wr2, new Vector2(losX - 5 * direction, StandardY + WRSpread / 2));
        if (wr3 != 0)
            SetPosition(wr3, new Vector2(losX, StandardY + WRSpread));

        // Offensive line
        PositionOffensiveLine(linemen, losX, StandardY, direction);
    }

    /// <summary>
    /// Positions players in a Goal Line formation.
    /// </summary>
    public void PositionGoalLineFormation(List<int> playerIds, int teamIndex, bool facingRight)
    {
        int direction = facingRight ? 1 : -1;
        int losX = LineOfScrimmageX;
        
        var qb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "QB");
        var rb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "RB");
        var fb = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "FB");
        var te1 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "TE1");
        var te2 = playerIds.FirstOrDefault(id => GetPlayerPosition(id) == "TE2");
        var linemen = playerIds.Where(id => IsLineman(GetPlayerPosition(id))).ToList();

        // QB under center (close)
        if (qb != 0)
            SetPosition(qb, new Vector2(losX - 8 * direction, StandardY));

        // FB leads, RB behind
        if (fb != 0)
            SetPosition(fb, new Vector2(losX - 12 * direction, StandardY));
        if (rb != 0)
            SetPosition(rb, new Vector2(losX - 18 * direction, StandardY));

        // Two TEs tight
        if (te1 != 0)
            SetPosition(te1, new Vector2(losX, StandardY - 20));
        if (te2 != 0)
            SetPosition(te2, new Vector2(losX, StandardY + 20));

        // Heavy line
        PositionOffensiveLine(linemen, losX, StandardY, direction, tightSpacing: true);
    }

    /// <summary>
    /// Positions defensive players in a 4-3 formation.
    /// </summary>
    public void Position43Defense(List<int> playerIds, int teamIndex, bool facingRight)
    {
        int direction = facingRight ? -1 : 1; // Defense faces opposite direction
        int losX = LineOfScrimmageX;
        int dLineDepth = 5;
        int lbDepth = 25;
        int dbDepth = 45;

        // Defensive line (4 players)
        var dLine = playerIds.Where(id => IsDefensiveLineman(GetPlayerPosition(id))).ToList();
        for (int i = 0; i < dLine.Count && i < 4; i++)
        {
            float yOffset = (i - 1.5f) * 10;
            SetPosition(dLine[i], new Vector2(losX + dLineDepth * direction, StandardY + yOffset));
        }

        // Linebackers (3 players)
        var lbs = playerIds.Where(id => IsLinebacker(GetPlayerPosition(id))).ToList();
        for (int i = 0; i < lbs.Count && i < 3; i++)
        {
            float yOffset = (i - 1) * 15;
            SetPosition(lbs[i], new Vector2(losX + lbDepth * direction, StandardY + yOffset));
        }

        // Defensive backs
        var dbs = playerIds.Where(id => IsDefensiveBack(GetPlayerPosition(id))).ToList();
        PositionDefensiveBacks(dbs, losX, StandardY, dbDepth, direction);
    }

    /// <summary>
    /// Positions kickoff formation.
    /// </summary>
    public void PositionKickoffFormation(List<int> playerIds, int teamIndex, bool kickingTeam)
    {
        if (kickingTeam)
        {
            // Kicker at 40-yard line
            var kicker = playerIds.FirstOrDefault();
            if (kicker != 0)
                SetPosition(kicker, new Vector2(40, StandardY));

            // Coverage team spread behind
            for (int i = 1; i < playerIds.Count; i++)
            {
                float xOffset = 20 + (i * 5);
                float yOffset = ((i % 3) - 1) * 30;
                SetPosition(playerIds[i], new Vector2(xOffset, StandardY + yOffset));
            }
        }
        else
        {
            // Return formation - returner deep, blockers in front
            var returner = playerIds.FirstOrDefault();
            if (returner != 0)
                SetPosition(returner, new Vector2(200, StandardY));

            for (int i = 1; i < playerIds.Count; i++)
            {
                float xOffset = 210 + ((i - 1) / 2) * 10;
                float yOffset = ((i % 3) - 1) * 40;
                SetPosition(playerIds[i], new Vector2(xOffset, StandardY + yOffset));
            }
        }
    }

    // Helper methods

    private void PositionOffensiveLine(List<int> linemen, int losX, int standardY, int direction, bool tightSpacing = false)
    {
        float spacing = tightSpacing ? 6 : OLSpacing;
        float centerX = losX;
        
        // Center is at losX, standardY
        if (linemen.Count > 0)
            SetPosition(linemen[0], new Vector2(centerX, standardY));

        // Guards
        if (linemen.Count > 1)
            SetPosition(linemen[1], new Vector2(centerX, standardY - spacing));
        if (linemen.Count > 2)
            SetPosition(linemen[2], new Vector2(centerX, standardY + spacing));

        // Tackles
        if (linemen.Count > 3)
            SetPosition(linemen[3], new Vector2(centerX, standardY - spacing * 2));
        if (linemen.Count > 4)
            SetPosition(linemen[4], new Vector2(centerX, standardY + spacing * 2));
    }

    private void PositionDefensiveBacks(List<int> dbs, int losX, int standardY, int depth, int direction)
    {
        // CBs wide, Safeties deep middle
        for (int i = 0; i < dbs.Count; i++)
        {
            string pos = GetPlayerPosition(dbs[i]);
            Vector2 position;

            if (pos.Contains("CB") || i < 2)
            {
                // Cornerbacks - wide
                float yOffset = (i % 2 == 0) ? -35 : 35;
                position = new Vector2(losX + depth * direction, standardY + yOffset);
            }
            else
            {
                // Safeties - deep middle
                float yOffset = (i % 2 == 0) ? -10 : 10;
                position = new Vector2(losX + (depth + 10) * direction, standardY + yOffset);
            }

            SetPosition(dbs[i], position);
        }
    }

    private void SetPosition(int entityId, Vector2 position)
    {
        var entity = _world.GetEntity(entityId);
        if (entity.Get<PositionComponent>() is { } posComponent)
        {
            posComponent.Position = position;
        }
        
        // Also update behavior target
        if (entity.Get<BehaviorComponent>() is { } behavior)
        {
            behavior.TargetPosition = position;
        }
    }

    private string GetPlayerPosition(int entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity.Get<PlayerAttributesComponent>() is { } attrs)
        {
            return attrs.Position;
        }
        return "";
    }

    private bool IsLineman(string position)
    {
        return position is "C" or "G" or "T" or "LG" or "RG" or "LT" or "RT" or "OL";
    }

    private bool IsDefensiveLineman(string position)
    {
        return position is "DE" or "DT" or "NT" or "DL";
    }

    private bool IsLinebacker(string position)
    {
        return position is "LB" or "MLB" or "OLB" or "ILB";
    }

    private bool IsDefensiveBack(string position)
    {
        return position is "CB" or "S" or "FS" or "SS" or "DB";
    }
}

/// <summary>
/// Predefined offensive formations.
/// </summary>
public enum OffensiveFormation
{
    Pro,
    Shotgun,
    ShotgunTrips,
    GoalLine,
    IForm,
    Singleback,
    Empty,
    Kickoff
}

/// <summary>
/// Predefined defensive formations.
/// </summary>
public enum DefensiveFormation
{
    Defense43,
    Defense34,
    DefenseNickel,
    DefenseDime,
    GoalLine,
    Prevent
}
