using Cog;
using UnityEngine;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;
using System.Linq;

public class MapInteractionManager : MonoBehaviour
{
    public static MapInteractionManager instance;
    public static bool clickedPlayerCell;

    public static Vector3Int CurrentSelectedCell; // Offset odd r coords
    public static Vector3Int CurrentMouseCell; // Offset odd r coords
    public TravelMarkerController travelMarkerController;

    [SerializeField]
    Transform cursor,
        selectedMarker1;

    Vector3Int selectedCellPos;

    Plane m_Plane;

    bool validPosition;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        m_Plane = new Plane(Vector3.forward, 0);
        Cog.PluginController.Instance.EventStateUpdated += OnStateUpdated;

        selectedMarker1.gameObject.SetActive(false);
    }

    private void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        //Initialise the enter variable
        float enter = 0.0f;

        if (m_Plane.Raycast(ray, out enter))
        {
            //Get the point that is clicked
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3Int cubePos = GridExtensions.GridToCube(
                MapManager.instance.grid.WorldToCell(hitPoint)
            );
            validPosition =
                !MapManager.isMakingMove
                || (
                    TileHelper.GetTileNeighbours(selectedCellPos).Contains(cubePos)
                    || cubePos == selectedCellPos
                );
            if (validPosition)
            {
                CurrentMouseCell = MapManager.instance.grid.WorldToCell(hitPoint);
                cursor.position = MapManager.instance.grid.CellToWorld(
                    MapManager.instance.grid.WorldToCell(hitPoint)
                );
            }
        }
        if (Input.GetMouseButtonDown(0))
        {
            MapClicked();
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (clickedPlayerCell)
            {
                if (validPosition && IsDiscoveredTile(GridExtensions.GridToCube(CurrentMouseCell)))
                    MapClicked();
                else
                {
                    MapManager.isMakingMove = false;
                    selectedMarker1.gameObject.SetActive(true);
                    travelMarkerController.HideLine();
                }
            }
            clickedPlayerCell = false;
        }
        if (Input.GetMouseButtonDown(1))
        {
            MapClicked2();
        }
    }

    void MapClicked()
    {
        // CurrentMouseCell is using Odd R offset coords
        var cellPosCube = GridExtensions.GridToCube(CurrentMouseCell);
        if (!IsDiscoveredTile(cellPosCube))
            return;
        selectedCellPos = cellPosCube;
        Cog.PluginController.Instance.SendTileInteractionMsg(cellPosCube);
    }

    void MapClicked2()
    {
        var cellPosOddR = MapManager.instance.grid.WorldToCell(cursor.position);
        var cellPosCube = GridExtensions.GridToCube(cellPosOddR);

        // --  Debug

        if (SeekerManager.Instance.Seeker != null)
        {
            // function SCOUT_SEEKER(uint32 sid, int16 q, int16 r, int16 s) external;
            Cog.PluginController.Instance.DispatchAction("SCOUT_SEEKER", "0x" + SeekerManager.Instance.Seeker.Key, cellPosCube.x, cellPosCube.y, cellPosCube.z);
        }
    }

    private void MoveSeeker(Seeker seeker, Vector3Int cellPosCube)
    {
        // function MOVE_SEEKER(uint32 sid, int16 q, int16 r, int16 s) external;
        Cog.PluginController.Instance.DispatchAction("MOVE_SEEKER", "0x" + SeekerManager.Instance.Seeker.Key, cellPosCube.x, cellPosCube.y, cellPosCube.z);
    }

    // -- TODO: Obviously this won't scale, need to hold tiles in a dictionary
    private bool IsDiscoveredTile(Vector3Int cellPosCube)
    {
        if (Cog.PluginController.Instance.WorldState != null)
        {
            foreach (var tile in Cog.PluginController.Instance.WorldState.Game.Tiles)
            {
                if (TileHelper.GetTilePosCube(tile) == cellPosCube)
                    return true;
            }
        }

        return false;
    }

    // -- LISTENERS

    private void OnStateUpdated(State state)
    {
        if (state.UI.Selection.Tiles.Count > 0)
        {
            var tile = state.UI.Selection.Tiles.ToList()[0];
            var cellPosCube = TileHelper.GetTilePosCube(tile);
            var gridCoords = GridExtensions.CubeToGrid(cellPosCube);
            if (CurrentSelectedCell != gridCoords)
            {
                OnTileInteraction(cellPosCube);
            }
        }
    }

    private void OnTileInteraction(Vector3Int cellPosCube)
    {
        CurrentSelectedCell = GridExtensions.CubeToGrid(cellPosCube);

        bool isPlayerAtPosition = SeekerManager.Instance.IsPlayerAtPosition(cellPosCube);
        if (isPlayerAtPosition)
        {
            clickedPlayerCell = true;
            if (MapManager.isMakingMove)
            {
                // Seeker already selected so deselect
                MapManager.isMakingMove = false;

                selectedMarker1.gameObject.SetActive(true);
            }
            else
            {
                // Select the seeker
                MapManager.isMakingMove = true;

                selectedMarker1.gameObject.SetActive(true);
                selectedMarker1.position = MapManager.instance.grid.CellToWorld(
                    CurrentSelectedCell
                );
            }
        }
        else
        {
            // If a seeker is selected then show the destination selection
            if (MapManager.isMakingMove && SeekerManager.Instance.Seeker != null)
            {
                // TODO: Show Marker 2 until the move has completed
                // selectedMarker2.gameObject.SetActive(true);
                // selectedMarker2.position = MapManager.instance.grid.CellToWorld(CurrentSelectedCell);
                selectedMarker1.gameObject.SetActive(false);

                MapManager.isMakingMove = false;
                MoveSeeker(SeekerManager.Instance.Seeker, cellPosCube);
            }
            else
            {
                selectedMarker1.gameObject.SetActive(true);

                selectedMarker1.position = MapManager.instance.grid.CellToWorld(
                    CurrentSelectedCell
                );
            }
        }
    }
}
