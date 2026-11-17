using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An inspector-style panel that opens by itself whenever something is selected (worker, building, deposit, crate)
/// and can only be closed with its X button (which also deselects). It slides in through a <see cref="Collapsible"/>.
/// </summary>
public class AutoOpenPanel : MonoBehaviour
{
    public Collapsible slide;
    public Text title;
    public Text body;
    [Header("Workplace controls (shown for buildings with worker slots)")]
    public GameObject controls;
    public Text workersValue, priorityValue;

    /// <summary>Wired to the workers -/+ buttons.</summary>
    public void ChangeWorkers(int delta)
    {
        var b = SelectionController.Instance != null ? SelectionController.Instance.SelectedBuilding : null;
        if (b != null && b.IsWorkplace) b.SetWanted(b.wanted + delta);
    }

    /// <summary>Wired to the priority -/+ buttons.</summary>
    public void ChangePriority(int delta)
    {
        var b = SelectionController.Instance != null ? SelectionController.Instance.SelectedBuilding : null;
        if (b != null && b.IsWorkplace) b.SetPriority(b.priority + delta);
    }

    /// <summary>Wired to the X button.</summary>
    public void Close()
    {
        if (SelectionController.Instance != null) SelectionController.Instance.Clear();
        if (slide != null) slide.SetCollapsed(true, false);
    }

    private void Update()
    {
        var s = SelectionController.Instance;
        bool has = s != null && (s.HasSelection || s.SelectedCrate != null);
        if (slide != null) slide.SetCollapsed(!has, false);
        if (controls != null) controls.SetActive(has && s.SelectedWorker == null && s.SelectedBuilding != null && s.SelectedBuilding.IsWorkplace);
        if (!has) return;

        if (s.SelectedWorker != null)
        {
            title.text = s.SelectedWorker.workerName;
            body.text = s.SelectedWorker.JobTitle + "\n" + s.SelectedWorker.StatusText;
        }
        else if (s.SelectedBuilding != null)
        {
            title.text = s.SelectedBuilding.displayName;
            var b = s.SelectedBuilding;
            if (b.IsWorkplace)
            {
                body.text = "Working now: " + BuildingManager.instance.Crew(b);
                workersValue.text = b.wanted + " / " + b.maxWorkers;
                priorityValue.text = b.priority.ToString();
            }
            else body.text = "";
        }
        else if (s.SelectedNode != null)
        {
            title.text = ResourceInfo.Name(s.SelectedNode.resource) + (s.SelectedNode.resource == ResourceKind.Fish ? " (fishing spot)" : " deposit");
            body.text = s.SelectedNode.resource == ResourceKind.Fish ? "Never runs out" : "Remaining " + s.SelectedNode.yield + " / " + s.SelectedNode.maxYield;
        }
        else if (s.SelectedCrate != null)
        {
            title.text = s.SelectedCrate.Opened ? "Opened crate" : "Pearl crate";
            body.text = s.SelectedCrate.Opened ? "Empty" : s.SelectedCrate.data.pearls + " pearls\nNeeds " + s.SelectedCrate.data.amount + " " + Products.TagName(s.SelectedCrate.Tag).ToLower();
        }
    }
}
