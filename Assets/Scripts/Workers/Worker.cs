using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Worker : MonoBehaviour
{
    private List<Tile> path;
    private int currentIndex;

    [Header("Movement")]
    protected float moveSpeed;

    [Header("Work Settings")]
    protected int carryCapacity;
    protected float workTimer;

    [Header("Rest Settings")]
    protected float restTimer = 10f; 
    protected float fatigueCounter = 0f;
    protected float restCounter = 0f; 
    [HideInInspector] public bool resting;
    [HideInInspector] public bool arrived;
    [HideInInspector] public Building restBuilding;

    [Header("Attributes")]
    public int strength = 10;
    public int dexterity = 10;
    public int intelligence = 10;

    [Header("Carry Data")]
    [HideInInspector] public RessourceCarry carry;
    protected Animator anim;

    // State flags
    public bool IsRunning { get; protected set; }
    public bool IsWorking { get; protected set; }

    protected virtual void Awake()
    {
       UpdateStats();
       anim = transform.GetChild(0).GetComponent<Animator>();

       StartCoroutine(CheckTasksCoroutine());
    }
    protected void UpdateStats()
    {
        carryCapacity = strength / 2;
        moveSpeed = dexterity / 2;
        workTimer = 40f / (intelligence*2);
    }
    IEnumerator CheckTasksCoroutine()
    {
        while (GridManager.instance == null || !GridManager.instance.IsInitialized)
        {
            yield return null; // wait a frame
        }
        while (true)
        {
            CheckTasks();
            yield return new WaitForSeconds(0.5f);
        }
    }
    protected virtual void CheckTasks()
    {
        Debug.LogWarning("NO TASKS CODED");
    }
    public void GoTo(Tile target)
    {
        Tile start = PosToTile();
        IsWorking = false;
        IsRunning = true;
        path = PathFinder.FindPath(start, target);
        currentIndex = 0;
    }
    protected Tile PosToTile()
    {
        return GridManager.instance.GetTile(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );
    }
    void Update()
    {
        HandleMovement();
        anim.SetBool("running", IsRunning);
        anim.SetBool("working", IsWorking);
    }
    private void HandleMovement()
    {
        if (path == null || currentIndex >= path.Count)
        {
            IsRunning = false;
            return;
        }

        IsRunning = true;

        Vector3 targetPos = path[currentIndex].transform.position;
        Vector3 moveDir = (targetPos - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPos);

        // Flip sprite based on horizontal movement
        if (moveDir.x > 0.01f)
            transform.localScale = new Vector3(1, transform.localScale.y, transform.localScale.z);
        else if (moveDir.x < -0.01f)
            transform.localScale = new Vector3(-1, transform.localScale.y, transform.localScale.z);

        // Snap if we're very close to prevent jitter
        if (distance <= moveSpeed * Time.deltaTime || distance < 0.05f)
        {
            transform.position = targetPos;
            currentIndex++;
        }
        else
        {
            transform.position += moveDir * moveSpeed * Time.deltaTime;
        }

        if (currentIndex >= path.Count)
            IsRunning = false;
    }


    // Working state can be toggled from outside
    public void StartWorking()
    {
        IsWorking = true;
        IsRunning = false;
    }
    public void StopWorking()
    {
        IsWorking = false;
    }
    private void OnDrawGizmos()
    {
        if (path == null || path.Count == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 from = path[i].transform.position + Vector3.up * 0.1f;
            Vector3 to = path[i + 1].transform.position + Vector3.up * 0.1f;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(from, 0.1f);
        }
        Gizmos.DrawSphere(path[path.Count - 1].transform.position + Vector3.up * 0.1f, 0.1f);
    }
    protected (Tile resourceTile, Tile standTile) FindClosestAccessibleResourceTile(string resourceType, int maxAttempts = 10)
    {
        GameObject[] resources = GameObject.FindGameObjectsWithTag(resourceType);
        if (resources.Length == 0)
        {
            Debug.LogWarning($"No resources with tag '{resourceType}' found!");
            return (null, null);
        }

        Vector3 pos = transform.position;

        // Sort resources by distance
        List<GameObject> sortedResources = new List<GameObject>(resources);
        sortedResources.Sort((a, b) =>
        {
            float distA = Vector3.Distance(pos, a.transform.position);
            float distB = Vector3.Distance(pos, b.transform.position);
            return distA.CompareTo(distB);
        });

        int attempts = 0;

        foreach (GameObject res in sortedResources)
        {
            if (attempts >= maxAttempts) break;
            attempts++;

            Tile resourceTile = res.GetComponent<Tile>();
            if (resourceTile == null) continue;

            // Check neighbors for free tile
            Tile freeNeighbor = PathFinder.GetFreeNeighborTile(resourceTile);
            if (freeNeighbor != null)
            {
                return (resourceTile, freeNeighbor);
            }
        }

        return (null, null);
    }
    



}
