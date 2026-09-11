from pathlib import Path

p = Path('Source/WNG/Wraith/WraithFactionHunger.cs')
s = p.read_text(encoding='utf-8')

old_call = '                OpenFeedingRequest(faction, home, subjects.Count, record, ext, now);'
new_call = '                OpenFeedingRequest(faction, home, subjects, record, ext, now);'
if old_call in s:
    s = s.replace(old_call, new_call, 1)
elif new_call not in s:
    raise SystemExit('Strategic request call site not found')

start_marker = '        private void OpenFeedingRequest('
end_marker = '        private void AcceptRequest('
start = s.find(start_marker)
end = s.find(end_marker, start)
if start < 0 or end < 0:
    raise SystemExit('Strategic request method replacement boundaries not found')

replacement = r'''        private void OpenFeedingRequest(Faction faction, Map home, List<Pawn> subjects, WraithFactionHungerRecord record, WraithFactionHungerExtension ext, int now)
        {
            if (faction == null || home == null || record == null || ext == null || subjects == null || subjects.Count == 0)
                return;

            List<Pawn> involvedWraiths = ResolveInvolvedWraiths(faction, home);
            if (involvedWraiths.Count == 0)
            {
                record.nextRequestTick = SafeFutureTick(now, ext.requestRetryTicks);
                return;
            }

            int years = Math.Max(0, ext.feedingAgeYears);
            int hungerPercent = (int)Math.Round(Clamp01(record.hunger) * 100f);
            record.nextRequestTick = SafeFutureTick(now, ext.postRequestCooldownTicks);
            requestWindowOpen = true;

            try
            {
                Find.WindowStack.Add(new Dialog_WraithFeedingSubjectSelection(
                    faction.Name,
                    subjects,
                    years,
                    hungerPercent,
                    subject => OpenInvolvedWraithConfirmation(faction, home, subject, involvedWraiths, record, ext, years),
                    () => FinishRefusal(faction, record, ext)));
            }
            catch (Exception ex)
            {
                requestWindowOpen = false;
                record.nextRequestTick = SafeFutureTick(now, ext.requestRetryTicks);
                Log.Error("[WNG] Failed to open strategic Wraith feeding subject stage: " + ex);
            }
        }

        private void OpenInvolvedWraithConfirmation(
            Faction faction,
            Map home,
            Pawn subject,
            List<Pawn> involvedWraiths,
            WraithFactionHungerRecord record,
            WraithFactionHungerExtension ext,
            int years)
        {
            if (faction == null || home == null || record == null || ext == null)
            {
                requestWindowOpen = false;
                return;
            }

            if (subject == null || subject.Dead || !subject.Spawned || subject.Map != home || !IsEligibleFeedingSubject(subject))
            {
                FinishRefusal(faction, record, ext);
                return;
            }

            if (involvedWraiths == null || involvedWraiths.Count == 0 || involvedWraiths.Any(p => !IsValidInvolvedWraith(p, faction)))
            {
                requestWindowOpen = false;
                record.nextRequestTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, ext.requestRetryTicks);
                Messages.Message(faction.Name + " feeding request could not retain its exact Wraith roster and will be retried later.", MessageTypeDefOf.NeutralEvent, true);
                return;
            }

            string roster = string.Join("\n", involvedWraiths.Select(p => "- " + p.LabelShortCap));
            string text =
                "Selected feeding subject: " + subject.LabelShortCap + "\n\n" +
                "Involved Wraiths: " + involvedWraiths.Count + "\n" + roster + "\n\n" +
                "Submitting authorizes the controlled feeding agreement. " + subject.LabelShortCap +
                " will gain " + years + " biological years and Life Drained, and " + faction.Name +
                "'s strategic hunger will be relieved. Canceling/refusing invokes the faction's existing refusal/raid consequence.";

            try
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    text,
                    "Submit",
                    () => FinishAcceptance(faction, subject, years, record, ext),
                    "Cancel",
                    () => FinishRefusal(faction, record, ext),
                    faction.Name + " — involved Wraiths",
                    buttonADestructive: false,
                    acceptAction: () => FinishAcceptance(faction, subject, years, record, ext),
                    cancelAction: () => FinishRefusal(faction, record, ext)));
            }
            catch (Exception ex)
            {
                requestWindowOpen = false;
                record.nextRequestTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, ext.requestRetryTicks);
                Log.Error("[WNG] Failed to open strategic Wraith involved-roster stage: " + ex);
            }
        }

        private List<Pawn> ResolveInvolvedWraiths(Faction faction, Map home)
        {
            List<Pawn> present = home?.mapPawns?.AllPawnsSpawned?
                .Where(p => IsValidInvolvedWraith(p, faction))
                .Distinct()
                .OrderBy(p => p.LabelShort)
                .ThenBy(p => p.thingIDNumber)
                .ToList() ?? new List<Pawn>();
            if (present.Count > 0)
                return present;

            Pawn leader = faction?.leader;
            if (IsValidInvolvedWraith(leader, faction))
                present.Add(leader);
            return present;
        }

        private static bool IsValidInvolvedWraith(Pawn pawn, Faction faction)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Downed && pawn.Faction == faction && WraithLifeForceUtility.IsWraith(pawn);
        }

        private void FinishAcceptance(Faction faction, Pawn subject, int years, WraithFactionHungerRecord record, WraithFactionHungerExtension ext)
        {
            requestWindowOpen = false;
            AcceptRequest(faction, subject, years, record, ext);
        }

        private void FinishRefusal(Faction faction, WraithFactionHungerRecord record, WraithFactionHungerExtension ext)
        {
            requestWindowOpen = false;
            RefuseRequest(faction, record, ext);
        }

'''

s = s[:start] + replacement + s[end:]
p.write_text(s, encoding='utf-8')
