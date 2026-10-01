using TMPro;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class HudView : BoundView<HudViewModel>
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text health, fragments, checkpoint, abilities;
        [SerializeField] private HudIconGraphic[] hearts;
        [SerializeField] private CanvasGroup formCard, tearCard, recoverCard;
        [SerializeField] private TMP_Text fragmentCount;
        public void Configure(CanvasGroup g, TMP_Text h, TMP_Text f, TMP_Text c, TMP_Text a) { group = g; health = h; fragments = f; checkpoint = c; abilities = a; }
        public void ConfigureIcons(CanvasGroup g, HudIconGraphic[] heartIcons, TMP_Text count, CanvasGroup form, CanvasGroup tear, CanvasGroup recover)
        { group = g; hearts = heartIcons; fragmentCount = count; formCard = form; tearCard = tear; recoverCard = recover; }
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return;
            // Compatibility until the next Editor builder run replaces the old serialized HUD.
            if (health != null) health.text = Model.Health;
            if (fragments != null) fragments.text = Model.Fragments;
            if (checkpoint != null) checkpoint.text = Model.Checkpoint;
            if (abilities != null) abilities.text = Model.Abilities;
            if (fragmentCount != null) fragmentCount.text = Model.FragmentCount;
            if (hearts != null) for (int i = 0; i < hearts.Length && i < 3; i++)
                if (hearts[i] != null) hearts[i].FillAmount = Model.HeartFill(i);
            if (formCard != null) formCard.alpha = Model.FormUnlocked ? 1 : .3f;
            if (tearCard != null) tearCard.alpha = Model.TearUnlocked ? 1 : .3f;
            if (recoverCard != null) recoverCard.alpha = Model.RecoverUnlocked ? 1 : .3f;
        }
    }
}
