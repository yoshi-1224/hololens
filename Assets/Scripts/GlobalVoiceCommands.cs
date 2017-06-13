﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.InputModule;
using System;

public class GlobalVoiceCommands : MonoBehaviour, ISpeechHandler {
    [Tooltip("Adjust the scaling sensitivity applied on voice commands")]
    public float ScalingFactor;
    [Tooltip("The prefab for the tool menu to show for planning")]
    public GameObject toolMenuPrefab;
    private GameObject toolMenuObject;

    private GameObject map;
    public const string COMMAND_MOVE_MAP = "move map";
    public const string COMMAND_MAP_BIGGER = "map bigger";
    public const string COMMAND_MAP_SMALLER = "map smaller";
    public const string COMMAND_SCALE_MAP = "scale map";
    public const string COMMAND_SHOW_TOOLS = "show tools";
    public const string COMMAND_HIDE_TOOLS = "hide tools";
    public const bool IS_ENLARGE = true;

    void Start () {
        if (InputManager.Instance == null) {
            return;
        }
        InputManager.Instance.AddGlobalListener(gameObject);
        toolMenuObject = null;
	}
	
	void Update () {
		
	}

    private void OnDestroy() {
        if (InputManager.Instance == null)
            return;
        InputManager.Instance.RemoveGlobalListener(gameObject);
    }

    /// <summary>
    /// handler for "move map" voice command. Has the same effect as selecting the map
    /// </summary>
    public void moveMap() {
        if (map == null)
            map = GameObject.Find("CustomizedMap");
        map.SendMessage("OnInputClicked", new InputClickedEventData(null));
    }

    public void OnSpeechKeywordRecognized(SpeechKeywordRecognizedEventData eventData) {
        switch (eventData.RecognizedText.ToLower()) {
            case COMMAND_MOVE_MAP:
                moveMap();
                break;
            case COMMAND_MAP_BIGGER:
                enlargeMap(IS_ENLARGE);
                break;
            case COMMAND_MAP_SMALLER:
                enlargeMap(!IS_ENLARGE);
                break;
            case COMMAND_SCALE_MAP:
                scaleMap();
                break;
            case COMMAND_SHOW_TOOLS:
                showTools();
                break;
            case COMMAND_HIDE_TOOLS:
                hideTools();
                break;
            default:
                // just ignore
                break;
        }
    }

    /// <summary>
    /// scales the map together with the buildings by ScalingFactor directly via voice commands
    /// </summary>
    private void enlargeMap(bool enlarge) {
        if (map == null)
            map = GameObject.Find("CustomizedMap");
        bool isPlacing = map.GetComponent<InteractibleMap>().IsPlacing;

        // if enlarge == true, make the map bigger. else smaller
        int sign = enlarge ? 1 : -1;
        if (!isPlacing)
            // make the buildings follow the same scaling as the parent map
            map.SendMessage("MakeSiblingsChildren");
        map.transform.localScale += new Vector3(sign * ScalingFactor, sign * ScalingFactor, sign * ScalingFactor);
        if (!isPlacing)
            map.SendMessage("MakeChildrenSiblings");
    }

    /// <summary>
    /// enable manipulation gesture to scale the map together with the buildings
    /// </summary>
    private void scaleMap() {
        if (map == null)
            map = GameObject.Find("CustomizedMap");
        map.SendMessage("RegisterForScaling");
    }

    /// <summary>
    /// activates the tools menu
    /// </summary>
    private void showTools() {
        if (toolMenuObject.activeSelf)
            return;
        toolMenuObject.SetActive(true);
    }
    
    private void hideTools() {
        if (!toolMenuObject.activeSelf)
            return;
        toolMenuObject.SetActive(false);
    }
}
