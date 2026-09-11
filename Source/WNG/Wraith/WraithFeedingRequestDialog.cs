using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Stage one of the strategic faction-hunger request. This window selects only the biological
    /// feeding subject. It deliberately contains no Wraith-selection control and remains force-paused
    /// until Submit advances to the involved-Wraith confirmation or Cancel refuses the request.
    /// </summary>
    public sealed class Dialog_WraithFeedingSubjectSelection : Window
    {
        private readonly string factionName;
        private readonly List<Pawn> subjects;
        private readonly int feedingAgeYears;
        private readonly int strategicHungerPercent;
        private readonly Action<Pawn> submitAction;
        private readonly Action cancelAction;
        private Pawn selected;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(660f, 520f);

        public Dialog_WraithFeedingSubjectSelection(
            string factionName,
            IEnumerable<Pawn> subjects,
            int feedingAgeYears,
            int strategicHungerPercent,
            Action<Pawn> submitAction,
            Action cancelAction)
        {
            this.factionName = factionName ?? "Wraith faction";
            this.subjects = subjects?
                .Where(p => p != null && !p.Dead)
                .Distinct()
                .OrderBy(p => p.LabelShort)
                .ToList() ?? new List<Pawn>();
            this.feedingAgeYears = Math.Max(0, feedingAgeYears);
            this.strategicHungerPercent = Math.Max(0, Math.Min(100, strategicHungerPercent));
            this.submitAction = submitAction;
            this.cancelAction = cancelAction;
            if (this.subjects.Count == 1)
                selected = this.subjects[0];

            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = false;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            closeOnAccept = false;
            onlyOneOfTypeAllowed = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 38f), factionName + " — feeding request");
            Text.Font = GameFont.Small;

            string explanation =
                factionName + " is suffering a genuine feeding shortage. Strategic hunger is at " + strategicHungerPercent + "%. " +
                "Select the biological prisoner or feeding-stock subject to place under the requested controlled feeding agreement. " +
                "This stage selects the subject only; individual Wraiths are not selected here. The selected subject will gain " +
                feedingAgeYears + " biological years and Life Drained if the final request is submitted. " +
                "Canceling/refusing this genuine request increases attack pressure according to the faction's hunger policy.";
            float explanationHeight = Text.CalcHeight(explanation, inRect.width);
            Widgets.Label(new Rect(0f, 44f, inRect.width, explanationHeight), explanation);

            float listTop = 54f + explanationHeight;
            float buttonAreaHeight = 45f;
            Rect outRect = new Rect(0f, listTop, inRect.width, inRect.height - listTop - buttonAreaHeight - 8f);
            float viewHeight = Math.Max(outRect.height, subjects.Count * 38f + 4f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            foreach (Pawn subject in subjects)
            {
                if (subject == null)
                    continue;
                bool isSelected = subject == selected;
                string label = (isSelected ? "✓ " : string.Empty) + subject.LabelShortCap;
                if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, 34f), label))
                    selected = subject;
                y += 38f;
            }
            Widgets.EndScrollView();

            float bottomY = inRect.height - 35f;
            if (Widgets.ButtonText(new Rect(0f, bottomY, 190f, 35f), "Cancel"))
            {
                CancelAndClose();
                return;
            }

            string submitLabel = selected == null ? "Submit — select a subject" : "Submit";
            if (Widgets.ButtonText(new Rect(inRect.width - 190f, bottomY, 190f, 35f), submitLabel) && selected != null)
                SubmitAndClose();
        }

        public override void OnCancelKeyPressed()
        {
            CancelAndClose();
            Event.current?.Use();
        }

        public override void OnAcceptKeyPressed()
        {
            if (selected != null)
            {
                SubmitAndClose();
                Event.current?.Use();
            }
        }

        private void SubmitAndClose()
        {
            Pawn subject = selected;
            Close(doCloseSound: true);
            submitAction?.Invoke(subject);
        }

        private void CancelAndClose()
        {
            Close(doCloseSound: true);
            cancelAction?.Invoke();
        }
    }
}
