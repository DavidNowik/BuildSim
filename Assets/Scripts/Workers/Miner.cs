using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Miner : Worker
{
    private Tile accessResourceTile;
    private Tile resourceTile;
    private Tile unloadTile;
    private Tile restTile;
    private Building storage;
    private bool unloaded = false;

    private float workCounter = 0f;

    protected override void Awake()
    {
        carry = new RessourceCarry("stone", 0);
        base.Awake();
        anim.SetTrigger("miner");
    }

    protected override void CheckTasks()
    {
        if (!resting)
        {
            // Increase fatigue slowly based on intelligence
            fatigueCounter += 0.5f / intelligence;//TODO find a timer that feels good

            if (fatigueCounter >= restTimer)
            {
                resting = true;
                fatigueCounter = 0f;
            }

            Debug.Log($"{name}: Performing tasks. Inventory: {carry.yield}/{carryCapacity}");
            CheckGoToMine();
            CheckWork();
            CheckUnload();
        }
        else
        {
            Debug.Log($"{name}: Currently resting. Going to rest tile.");
            GoToRest();
        }
    }


    private void GoToRest()
    {
        if (restBuilding == null)
        {
            restBuilding = BuildingManager.instance.GetNearestBuilding(transform.position, "stonetent");
            if (restBuilding != null)
            {
                restBuilding.GetComponent<StoneTent>().RegisterWorker(this);
                Debug.Log($"{name} - Found rest building, registering worker at: {restBuilding.name}");
            }
            else
            {
                Debug.LogWarning($"{name} - No rest building found!");
                return;
            }
        }

        if (restTile == null)
        {
            restTile = BuildingManager.instance.GetAnyAdjacentWalkableTile(restBuilding);
            if (restTile != null)
                Debug.Log($"{name} - Assigned rest tile at {restTile.GridPosition}");
            else
            {
                Debug.LogWarning($"{name} - Could not find an adjacent walkable tile for rest!");
                return;
            }
        }

        if (restTile != null && !arrived)
        {
            GoTo(restTile);
            Debug.Log($"{name} - Moving towards rest tile at {restTile.GridPosition}");
        }

        if (PosToTile() == restTile && restTile != null)
        {
            arrived = true;
            Debug.Log($"{name} - Arrived at rest tile at {restTile.GridPosition}");
        }

        if (arrived && resting)
        {
            // recover faster with higher intelligence
            restCounter += (0.5f + intelligence / 10f);
            Debug.Log($"{name} - Resting... restCounter = {restCounter}/{restTimer}");

            if (transform.GetChild(0).GetComponent<SpriteRenderer>() != null)
                transform.GetChild(0).GetComponent<SpriteRenderer>().color = Color.black;

            if (restCounter >= restTimer)
            {
                resting = false;
                arrived = false;
                restCounter = 0f;

                if (transform.GetChild(0).GetComponent<SpriteRenderer>() != null)
                    transform.GetChild(0).GetComponent<SpriteRenderer>().color = Color.white;

                restTile = null; // reset tile for next rest
                Debug.Log($"{name} - Finished resting, ready to work again");
            }
        }
    }

    private void CheckWork()
    {
        Tile currentTile = PosToTile();
        if (currentTile == null)
        {
            Debug.Log($"{name}: No current tile found.");
            return;
        }

        if (currentTile == accessResourceTile && carry.yield < carryCapacity)
        {
            if (!IsWorking) Debug.Log($"{name}: Started working at resource tile.");
            IsWorking = true;
            anim.SetBool("working", true);

            workCounter += 0.5f;
            Debug.Log($"{name}: Work counter = {workCounter:F2}/{workTimer}");

            if (workCounter >= workTimer)
            {
                if (resourceTile != null && resourceTile.GetComponent<RessourceNode>() != null)
                {
                    Debug.Log($"{name}: Farming resource node at {resourceTile.name}.");
                    resourceTile.GetComponent<RessourceNode>().Farm();

                    carry.yield++;
                    Debug.Log($"{name}: Collected resource. Carry = {carry.yield}/{carryCapacity}");

                    workCounter = 0f;
                    FloatingTextManager.Instance.ShowText("+1", transform.position);
                    unloaded = false;
                }
                else
                {
                    Debug.LogWarning($"{name}: Resource tile missing or destroyed.");
                    resourceTile = null;
                    accessResourceTile = null;

                    if (carry.yield > 0)
                    {
                        Debug.Log($"{name}: Carrying something, going to unload.");
                        CheckUnload();
                    }
                    else
                    {
                        Debug.Log($"{name}: Empty hands, searching for new mine.");
                        CheckGoToMine();
                    }
                    return;
                }
            }
        }
        else
        {
            if (IsWorking) Debug.Log($"{name}: Stopped working.");
            IsWorking = false;
            anim.SetBool("working", false);
            workCounter = 0f;
        }
    }

    private void CheckGoToMine()
    {
        if (carry.yield < carryCapacity)
        {
            if (resourceTile == null || accessResourceTile == null)
            {
                Debug.Log($"{name}: Resource or access tile is null, searching for closest accessible resource.");
                SetRessourceTiles();

                if (resourceTile != null && accessResourceTile != null)
                {
                    Debug.Log($"{name}: Found resource tile {resourceTile.name} at {resourceTile.GridPosition}, access at {accessResourceTile.GridPosition}.");
                }
                else
                {
                    Debug.LogWarning($"{name}: Could not find any accessible resource tiles!");
                    return;
                }
            }

            if (accessResourceTile != null)
            {
                Debug.Log($"{name}: Heading to access tile at {accessResourceTile.GridPosition}.");
                GoTo(accessResourceTile);
            }
            else
            {
                Debug.LogWarning($"{name}: Access tile is null, cannot move.");
            }
        }
        else
        {
            Debug.Log($"{name}: Inventory full ({carry.yield}/{carryCapacity}), skipping mining.");
        }
    }


    private void SetRessourceTiles()
    {
        (Tile resource, Tile access) = FindClosestAccessibleResourceTile("stone");
        resourceTile = resource;
        accessResourceTile = access;
    }

    private void CheckUnload()
    {
        if (carry.yield >= carryCapacity)
        {
            if (storage == null)
            {
                storage = BuildingManager.instance.GetNearestBuilding(transform.position, "storage");
            }

            if (storage != null)
            {
                if (unloadTile == null)
                {
                    unloadTile = BuildingManager.instance.GetAnyAdjacentWalkableTile(storage);
                }

                if (unloadTile != null)
                {
                    GoTo(unloadTile);
                }
            }
        }

        if (PosToTile() == unloadTile && unloadTile != null && !unloaded)
        {
            FloatingTextManager.Instance.ShowText("+" + carry.yield.ToString(), transform.position);
            carry.yield = 0;

            SetRessourceTiles();
            unloaded = true;
        }
    }
}
