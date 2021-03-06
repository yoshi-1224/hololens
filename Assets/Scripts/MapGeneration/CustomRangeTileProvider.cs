﻿using UnityEngine;
using Mapbox.Map;
using Mapbox.Unity.Map;
using System;
using Mapbox.Unity.Utilities;
using System.Collections.Generic;
using System.Collections;
using HoloToolkit.Unity;

public enum Direction {
    East,
    West,
    North,
    South
}

public enum ZoomDirection {
    In,
    Out
}

/// <summary>
/// This class commands the mapbox-related classes to load new tiles or destroy the current ones.
/// </summary>
public class CustomRangeTileProvider : AbstractTileProvider {
    [SerializeField]
    Vector4 _preLoadedRange;

    static int visibleRange = 2;

    [SerializeField]
    private int zoomResponsiveness = 1;
    [SerializeField]
    private int maxZoomLevel = 20;
    [SerializeField]
    private int minZoomLevel = 11;

    public static event Action OnAllTilesAdded;
    public static event Action<UnwrappedTileId> OnTileObjectAdded;

    private bool AtStart = true;

    /// <summary>
    /// UnityTile.cs will put the pair into this dictionary
    /// </summary>
    public static Dictionary<UnwrappedTileId, GameObject> InstantiatedTiles { get; private set; }

    public static Dictionary<UnwrappedTileId, Interpolator> InstantiatedTilesInterpolator { get; private set; }

    internal override void OnInitialized() {
        InstantiatedTiles = new Dictionary<UnwrappedTileId, GameObject>();
        InstantiatedTilesInterpolator = new Dictionary<UnwrappedTileId, Interpolator>();
        LocationHelper.onTileJump += LocationHelper_onTileJump;
        //OnAllTilesAdded += OnAllTilesLoadedHandler;
        StartCoroutine(LoadNewTiles());
    }

    private void LocationHelper_onTileJump(UnwrappedTileId obj) {
        JumpToTile(obj);
    }

    public void PanTowards(Direction direction) {
        updateMapCenterMercatorAndCenterCoord(direction);
        shiftTiles(direction);
        StartCoroutine(addVisibleTiles());
    }

    /// <summary>
    /// attempts to load all the tiles that should be visible. If a tile already exists 
    /// then it is skipped
    /// </summary>
    /// <returns></returns>
    private IEnumerator addVisibleTiles() {
        var centerTileId = CustomMap.Instance.CenterTileId;
        for (int i = centerTileId.X - visibleRange; i <= centerTileId.X + visibleRange; i++) {
            for (int j = centerTileId.Y - visibleRange; j <= centerTileId.Y + visibleRange; j++) {
                UnwrappedTileId tileToAdd = new UnwrappedTileId(CustomMap.Instance.Zoom, i, j);
                if (InstantiatedTiles.ContainsKey(tileToAdd))
                    continue;
                AddTile(tileToAdd);
                yield return null;
            }
        }
        OnAllTilesAdded.Invoke();
    }

    private void shiftTiles(Direction direction) {
        var centerTileId = CustomMap.Instance.CenterTileId;
        foreach (UnwrappedTileId key in InstantiatedTiles.Keys) {
            GameObject tileObject = InstantiatedTiles[key];
            Interpolator interpolator = InstantiatedTilesInterpolator[key];
            int xOffset = key.X - centerTileId.X;
            int yOffset = key.Y - centerTileId.Y;

            Vector3 newLocalPosition = new Vector3(xOffset * CustomMap.Instance.UnityTileLocalSize, 0, -yOffset * CustomMap.Instance.UnityTileLocalSize);
            Vector3 worldP = gameObject.transform.TransformPoint(newLocalPosition);

            interpolator.SetTargetPosition(worldP);

            AdjustVisibility(xOffset, yOffset, tileObject);
        }
    }

    /// <summary>
    /// Changes the layer mask of the given tileObject and its children according to whether it is within the visible range or not
    /// </summary>
    private static void AdjustVisibility(int xOffset, int yOffset, GameObject tileObject) {
        if (Math.Abs(xOffset) <= visibleRange && Math.Abs(yOffset) <= visibleRange) {
            HoloToolkit.Unity.Utils.SetLayerRecursively(tileObject, GameObjectNamesHolder.LAYER_VISIBLE_TILES);
        } else {
            HoloToolkit.Unity.Utils.SetLayerRecursively(tileObject, GameObjectNamesHolder.LAYER_INVISIBLE_TILES);
        }
    }

    private static void AdjustVisibility(GameObject tileObject) {
        string[] tileIds = tileObject.name.Split('/');
        var centerTileId = CustomMap.Instance.CenterTileId;
        int xOffset = int.Parse(tileIds[1]) - centerTileId.X;
        int yOffset = int.Parse(tileIds[2]) - centerTileId.Y;
        AdjustVisibility(xOffset, yOffset, tileObject);
    }

    public static void CacheTileObject(UnwrappedTileId tileId, GameObject tileObject) {
        InstantiatedTiles[tileId] = tileObject;
        InstantiatedTilesInterpolator[tileId] = tileObject.GetComponent<Interpolator>();
        AdjustVisibility(tileObject);
        OnTileObjectAdded.Invoke(tileId);
    }

    /// <summary>
    /// updates CenterMercator in CustomMap.Instance so that new tiles can be added
    /// in the correct positions relative to the parent.
    /// </summary>
    private void updateMapCenterMercatorAndCenterCoord(Direction panDirection) {
        UnwrappedTileId centerTileId = CustomMap.Instance.CenterTileId;
        int newCenterXId = centerTileId.X;
        int newCenterYId = centerTileId.Y;

        // simply increment the Y or X rather than taking the average of the range
        switch(panDirection) {
            case Direction.South:
                newCenterYId++;
                break;
            case Direction.North:
                newCenterYId--;
                break;
            case Direction.East:
                newCenterXId++;
                break;
            case Direction.West:
                newCenterXId--;
                break;
        }

        UnwrappedTileId newCenterTileId = new UnwrappedTileId(CustomMap.Instance.Zoom, newCenterXId, newCenterYId);
        updateMapCenterMercatorAndCenterCoord(newCenterTileId);
    }

    private void updateMapCenterMercatorAndCenterCoord(UnwrappedTileId newCenterTileId) {
        var referenceTileRect = Conversions.TileBounds(newCenterTileId);

        CustomMap.Instance.CenterMercator = referenceTileRect.Center;
        CustomMap.Instance.CenterLatitudeLongitude = Conversions.TileIdToCenterLatitudeLongitude(newCenterTileId.X, newCenterTileId.Y, CustomMap.Instance.Zoom);
    }

    public void ChangeZoom(ZoomDirection zoomDirection) {
        if (!isNextZoomLevelWithinLimit(zoomDirection))
            return;

        if (zoomDirection == ZoomDirection.In) {
            CustomMap.Instance.Zoom += zoomResponsiveness;
        } else if (zoomDirection == ZoomDirection.Out) {
            CustomMap.Instance.Zoom -= zoomResponsiveness;
        }
        InteractibleMap.Instance.HideTablesAndObjects();
        RemoveAllTiles();
        StartCoroutine(LoadNewTiles());
    }

    private bool isNextZoomLevelWithinLimit(ZoomDirection zoom) {
        int levelAfterZoom = 0;
        switch (zoom) {
            case ZoomDirection.Out:
                levelAfterZoom = CustomMap.Instance.Zoom - zoomResponsiveness;
                break;
            case ZoomDirection.In:
                levelAfterZoom = CustomMap.Instance.Zoom + zoomResponsiveness;
                break;
        }
        if (levelAfterZoom > maxZoomLevel || levelAfterZoom < minZoomLevel)
            return false;
        else
            return true;
    }

    /// <summary>
    /// loads new tiles upon zoom
    /// </summary>
    internal IEnumerator LoadNewTiles() {
        var centerTile = CustomMap.Instance.CenterTileId;
        yield return null;
        Vector4 rangeToLoad = _preLoadedRange;
        rangeToLoad /= 2; // just load half of it
        for (int x = (int)(centerTile.X - rangeToLoad.x); x <= (centerTile.X + rangeToLoad.z); x++) {
            for (int y = (int)(centerTile.Y - rangeToLoad.y); y <= (centerTile.Y + rangeToLoad.w); y++) {
                AddTile(new UnwrappedTileId(_map.Zoom, x, y));
                yield return null; // stop here and resume at next frame
            }
        }

        if (AtStart) {
            GameObject.Find("Toolbar").SetActive(false);
            // this is just a hack. Toolbar element cannot be disabled in Editor before Play
            // since it has many singleton classes attached that needs to be initialized
            // so we allow it to be active but immediately set active to false at start

            InteractibleMap.Instance.PlacementStart(); // allows the user to place the map
        } else {
            OnAllTilesAdded.Invoke();
        }
            
    }

    public void LoadBuildingsAfterMapPlaced() { // this is only at the start
        if (AtStart) {
            OnAllTilesAdded.Invoke();
            AtStart = false;
        }
    }

    /// <summary>
    /// jumps to the the tile with the given Id. Called by LocationHelper class in response to 
    /// a game object selected in dropdown lists.
    /// </summary>
    public void JumpToTile(UnwrappedTileId tileIdtoJumpTo) {
        UnwrappedTileId centerTileId = tileIdtoJumpTo;
        foreach (UnwrappedTileId key in InstantiatedTiles.Keys) {
            GameObject tileObject = InstantiatedTiles[key];
            Interpolator interpolator = InstantiatedTilesInterpolator[key];
            int xOffset = key.X - centerTileId.X;
            int yOffset = key.Y - centerTileId.Y;

            Vector3 newLocalPosition = new Vector3(xOffset * CustomMap.Instance.UnityTileLocalSize, 0, -yOffset * CustomMap.Instance.UnityTileLocalSize);
            Vector3 worldP = gameObject.transform.TransformPoint(newLocalPosition);

            interpolator.SetTargetPosition(worldP);

            AdjustVisibility(xOffset, yOffset, tileObject);
        }

        updateMapCenterMercatorAndCenterCoord(centerTileId);

        StartCoroutine(LoadNewTiles());
    }

}
