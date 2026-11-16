using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoneTent : Building
{
    private Animator anim;

    public int workerCapacity = 2;
    public int workerCount = 0;
    private List<Worker> registeredWorkers = new List<Worker>();

    private bool someoneResting = false;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
    }
    protected override void Update()
    {
        base.Update();

        someoneResting = false;
        foreach (Worker worker in registeredWorkers)
        {
            if (worker.resting && worker.arrived)
                someoneResting = true;
        }
        anim.SetBool("resting", someoneResting);
    }


    public bool RegisterWorker(Worker worker)
    {
        if (registeredWorkers.Contains(worker))
        {
            return false;
        }
        if(workerCount < workerCapacity)
        {
            registeredWorkers.Add(worker);
            workerCount++;
            return true;
        }
        return false;
    }
    public void DeleteWorker(Worker worker)
    {
        registeredWorkers.Remove(worker);
        workerCount--;
    }

}
