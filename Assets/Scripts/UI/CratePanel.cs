using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel for the selected pearl crate: what it needs, what it gives, and one button per product that can pay for it
/// (the buttons are real scene objects, built by BuildSim/Setup/8 Add Pearl Crates; add a product, rebuild).
/// </summary>
public class CratePanel : MonoBehaviour
{
    [System.Serializable]
    public class PayButton
    {
        public ResourceKind product;
        public Button button;
        public Text label;
    }

    public GameObject root;
    public Text title;
    public Text sub;
    public Text body;
    public PayButton[] buttons;

    private void Start() { if (root != null) root.SetActive(false); }

    private void Update()
    {
        var sel = SelectionController.Instance;
        var c = sel != null ? sel.SelectedCrate : null;
        if (c == null) { if (root.activeSelf) root.SetActive(false); return; }
        if (!root.activeSelf) root.SetActive(true);

        var rm = ResourceManager.Instance;
        title.text = c.Opened ? "Opened crate" : "Pearl crate";
        sub.text = c.Opened ? "Empty" : "Locked";
        if (c.Opened) body.text = "The pearls were taken.";
        else body.text = "Reward: " + c.data.pearls + " pearls\nNeeds " + c.data.amount + " x " + Products.TagName(c.Tag).ToLower() + "\n(any one product that counts as such)";

        foreach (var pb in buttons)
        {
            bool show = !c.Opened && Products.Has(pb.product, c.Tag);
            if (pb.button.gameObject.activeSelf != show) pb.button.gameObject.SetActive(show);
            if (!show) continue;
            int have = rm != null ? rm.Get(pb.product) : 0;
            pb.label.text = "Pay " + c.data.amount + " " + ResourceInfo.Name(pb.product) + "   (have " + have + ")";
            pb.button.interactable = have >= c.data.amount;
        }
    }

    /// <summary>Wired to each button's OnClick with the product as int argument.</summary>
    public void OnPay(int product)
    {
        var sel = SelectionController.Instance;
        if (sel == null || sel.SelectedCrate == null || CrateManager.Instance == null) return;
        CrateManager.Instance.TryOpen(sel.SelectedCrate, (ResourceKind)product);
    }
}
