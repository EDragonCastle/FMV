using System;
using System.Collections.Generic;
using System.Linq;

public enum IssueSeverity { Info = 0, Warning = 1, Error = 2 }

public sealed class ImportIssue
{
    public IssueSeverity Severity;
    public string Sheet;
    public int Row;
    public string Message;

    public override string ToString()
    {
        string where = string.IsNullOrEmpty(Sheet) ? "" : (Row > 0 ? Sheet + " " + Row + "행: " : Sheet + ": ");
        return where + Message;
    }
}

public sealed class ParsedVideo
{
    public string VideoUID;
    public string ClipKey;
    public float LoopStartTime;
    public float BranchTimer;
    public BranchType BranchType;
    public int SourceRow;

    public List<Script> Scripts = new List<Script>();
    public List<BranchOption> BranchOptions = new List<BranchOption>();
    public QTESetting QteSetting;    // BranchType == QTE일 때만 채워짐
    public string NextVideoUID;      // BranchType == Continue일 때만 사용
    public string EndingId;          // BranchType == Ending일 때만 사용 (없어도 됨)
}

public sealed class ParseResult
{
    public List<ParsedVideo> Videos = new List<ParsedVideo>();
    public List<ImportIssue> Issues = new List<ImportIssue>();

    public bool HasErrors { get { return Issues.Any(i => i.Severity == IssueSeverity.Error); } }
    public int Count(IssueSeverity s) { return Issues.Count(i => i.Severity == s); }
}

/// <summary>
/// Videos / Scripts / BranchOptions / QTESteps 4개 시트 → 검증된 ParsedVideo 목록.
/// UnityEngine에 의존하지 않으므로 Unity 밖에서도 실행할 수 있다.
/// </summary>
public static class VideoImportParser
{
    public const string VideosSheet = "Videos";
    public const string ScriptsSheet = "Scripts";
    public const string BranchOptionsSheet = "BranchOptions";
    public const string QteStepsSheet = "QTESteps";

    public static ParseResult ParseFile(string path)
    {
        try
        {
            var sheets = XlsxReader.Read(path);
            return Parse(sheets);
        }
        catch (Exception e)
        {
            var r = new ParseResult();
            r.Issues.Add(new ImportIssue
            {
                Severity = IssueSeverity.Error,
                Message = "파일을 읽지 못했습니다: " + e.Message +
                          (e is System.IO.IOException ? " (다른 프로그램이 파일을 잠그고 있는지 확인하세요)" : "")
            });
            return r;
        }
    }

    public static ParseResult Parse(Dictionary<string, XlsxSheet> sheets)
    {
        var res = new ParseResult();
        var ctx = new Ctx { Res = res };

        var videos = new Dictionary<string, ParsedVideo>();
        var videoOrder = new List<string>();

        // ---------- Videos ----------
        XlsxSheet videosSheet;
        if (!sheets.TryGetValue(VideosSheet, out videosSheet))
        {
            ctx.Add(IssueSeverity.Error, VideosSheet, 0, "시트를 찾을 수 없습니다.");
            return res;
        }

        var vh = Header(videosSheet);
        if (vh == null) { ctx.Add(IssueSeverity.Error, VideosSheet, 0, "시트가 비어 있습니다."); return res; }

        int vUid = Col(vh, "VideoUID"), vClip = Col(vh, "ClipKey"), vLoop = Col(vh, "LoopStartTime"),
            vTimer = Col(vh, "BranchTimer"), vType = Col(vh, "BranchType"),
            vNext = Col(vh, "NextVideoUID"), vEnding = Col(vh, "EndingId");   // 선택적 열 (없어도 에러 아님)
        var missingVCols = MissingCols(("VideoUID", vUid), ("ClipKey", vClip), ("LoopStartTime", vLoop), ("BranchTimer", vTimer), ("BranchType", vType));
        if (missingVCols.Count > 0)
        {
            ctx.Add(IssueSeverity.Error, VideosSheet, vh.RowNumber, "헤더에 필요한 열이 없습니다: " + string.Join(", ", missingVCols.ToArray()));
            return res;
        }

        foreach (var row in videosSheet.Rows)
        {
            if (row.RowNumber <= vh.RowNumber) continue;
            string uid = Get(row, vUid);
            if (IsBlankRow(row)) continue;

            if (uid.Length == 0)
            {
                ctx.Add(IssueSeverity.Error, VideosSheet, row.RowNumber, "VideoUID가 비어 있습니다.");
                continue;
            }
            if (videos.ContainsKey(uid))
            {
                ctx.Add(IssueSeverity.Error, VideosSheet, row.RowNumber, "VideoUID '" + uid + "'가 중복됩니다. (처음 나온 위치: " + videos[uid].SourceRow + "행)");
                continue;
            }

            string clip = Get(row, vClip);
            if (clip.Length == 0) ctx.Add(IssueSeverity.Error, VideosSheet, row.RowNumber, "ClipKey가 비어 있습니다.");

            float loop = ParseFloat(ctx, VideosSheet, row.RowNumber, "LoopStartTime", Get(row, vLoop), 0f, allowNegative: false);
            float timer = ParseFloat(ctx, VideosSheet, row.RowNumber, "BranchTimer", Get(row, vTimer), 0f, allowNegative: false);

            BranchType type;
            string typeStr = Get(row, vType);
            if (!TryParseBranchType(typeStr, out type))
            {
                ctx.Add(IssueSeverity.Error, VideosSheet, row.RowNumber, "알 수 없는 BranchType '" + typeStr + "'입니다. (Select / QTE / Continue / Ending)");
                continue;
            }

            var pv = new ParsedVideo
            {
                VideoUID = uid,
                ClipKey = clip,
                LoopStartTime = loop,
                BranchTimer = timer,
                BranchType = type,
                NextVideoUID = vNext >= 0 ? Get(row, vNext) : "",
                EndingId = vEnding >= 0 ? Get(row, vEnding) : "",
                SourceRow = row.RowNumber
            };
            videos[uid] = pv;
            videoOrder.Add(uid);
        }

        if (videos.Count == 0)
        {
            ctx.Add(IssueSeverity.Error, VideosSheet, 0, "읽을 수 있는 영상이 하나도 없습니다.");
            return res;
        }

        // ---------- Continue / Ending 검증 ----------
        foreach (var pv in videos.Values)
        {
            if (pv.BranchType == BranchType.Continue)
            {
                if (pv.NextVideoUID.Length == 0)
                    ctx.Add(IssueSeverity.Error, VideosSheet, pv.SourceRow, "영상 '" + pv.VideoUID + "'는 Continue인데 NextVideoUID가 비어 있습니다.");
                else if (!videos.ContainsKey(pv.NextVideoUID))
                    ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "NextVideoUID '" + pv.NextVideoUID + "'가 아직 Videos 시트에 없습니다. (작성 중이면 정상)");
                if (pv.LoopStartTime != 0f)
                    ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "Continue 타입은 선택을 기다리지 않아 LoopStartTime이 사용되지 않습니다.");
            }
            else if (pv.BranchType == BranchType.Ending)
            {
                if (pv.NextVideoUID.Length > 0)
                    ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "Ending 타입은 NextVideoUID를 사용하지 않습니다. ('" + pv.NextVideoUID + "'는 무시됩니다)");
                if (pv.LoopStartTime != 0f)
                    ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "Ending 타입은 LoopStartTime을 사용하지 않습니다.");
                if (pv.BranchTimer != 0f)
                    ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "Ending 타입은 BranchTimer를 사용하지 않습니다.");
            }
            else if (pv.NextVideoUID.Length > 0)
            {
                ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "BranchType이 " + pv.BranchType + "라 NextVideoUID('" + pv.NextVideoUID + "')는 사용되지 않습니다.");
            }
        }

        // ---------- Scripts ----------
        XlsxSheet scriptsSheet;
        if (sheets.TryGetValue(ScriptsSheet, out scriptsSheet))
        {
            var sh = Header(scriptsSheet);
            if (sh != null)
            {
                int sUid = Col(sh, "VideoUID"), sStart = Col(sh, "StartTime"), sDur = Col(sh, "Duration"), sText = Col(sh, "ScriptText");
                var missing = MissingCols(("VideoUID", sUid), ("StartTime", sStart), ("Duration", sDur), ("ScriptText", sText));
                if (missing.Count > 0)
                    ctx.Add(IssueSeverity.Error, ScriptsSheet, sh.RowNumber, "헤더에 필요한 열이 없습니다: " + string.Join(", ", missing.ToArray()));
                else
                {
                    foreach (var row in scriptsSheet.Rows)
                    {
                        if (row.RowNumber <= sh.RowNumber || IsBlankRow(row)) continue;
                        string uid = Get(row, sUid);
                        if (uid.Length == 0) { ctx.Add(IssueSeverity.Error, ScriptsSheet, row.RowNumber, "VideoUID가 비어 있습니다."); continue; }

                        ParsedVideo pv;
                        if (!videos.TryGetValue(uid, out pv))
                        {
                            ctx.Add(IssueSeverity.Error, ScriptsSheet, row.RowNumber, "VideoUID '" + uid + "'가 Videos 시트에 없습니다.");
                            continue;
                        }

                        float start = ParseFloat(ctx, ScriptsSheet, row.RowNumber, "StartTime", Get(row, sStart), 0f, allowNegative: false);
                        float dur = ParseFloat(ctx, ScriptsSheet, row.RowNumber, "Duration", Get(row, sDur), 0f, allowNegative: false);
                        string text = Get(row, sText);
                        if (text.Length == 0) ctx.Add(IssueSeverity.Warning, ScriptsSheet, row.RowNumber, "ScriptText가 비어 있습니다.");

                        pv.Scripts.Add(new Script { startTime = start, duration = dur, scriptText = text });
                    }
                }
            }
        }
        else ctx.Add(IssueSeverity.Warning, ScriptsSheet, 0, "시트를 찾을 수 없습니다. (대사 없는 영상으로 처리)");

        foreach (var pv in videos.Values)
        {
            pv.Scripts.Sort((a, b) => a.startTime.CompareTo(b.startTime));
            for (int i = 1; i < pv.Scripts.Count; i++)
            {
                var prev = pv.Scripts[i - 1];
                if (pv.Scripts[i].startTime < prev.startTime + prev.duration)
                    ctx.Add(IssueSeverity.Warning, ScriptsSheet, pv.SourceRow,
                        "영상 '" + pv.VideoUID + "'에서 대사 시간이 겹칩니다. (" + prev.startTime + "s 대사가 끝나기 전에 다음 대사 시작)");
            }
            float lastEnd = pv.Scripts.Count > 0 ? pv.Scripts[pv.Scripts.Count - 1].startTime + pv.Scripts[pv.Scripts.Count - 1].duration : 0f;
            if (lastEnd > pv.BranchTimer)
                ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow,
                    "영상 '" + pv.VideoUID + "'의 BranchTimer(" + pv.BranchTimer + "s)가 마지막 대사 종료 시각(" + lastEnd + "s)보다 빠릅니다.");
        }

        // ---------- BranchOptions ----------
        XlsxSheet branchSheet;
        var usedForBranch = new HashSet<string>();
        if (sheets.TryGetValue(BranchOptionsSheet, out branchSheet))
        {
            var bh = Header(branchSheet);
            if (bh != null)
            {
                int bUid = Col(bh, "VideoUID"), bText = Col(bh, "BranchText"), bTarget = Col(bh, "TargetVideoUID");
                var missing = MissingCols(("VideoUID", bUid), ("BranchText", bText), ("TargetVideoUID", bTarget));
                if (missing.Count > 0)
                    ctx.Add(IssueSeverity.Error, BranchOptionsSheet, bh.RowNumber, "헤더에 필요한 열이 없습니다: " + string.Join(", ", missing.ToArray()));
                else
                {
                    foreach (var row in branchSheet.Rows)
                    {
                        if (row.RowNumber <= bh.RowNumber || IsBlankRow(row)) continue;
                        string uid = Get(row, bUid);
                        if (uid.Length == 0) { ctx.Add(IssueSeverity.Error, BranchOptionsSheet, row.RowNumber, "VideoUID가 비어 있습니다."); continue; }

                        ParsedVideo pv;
                        if (!videos.TryGetValue(uid, out pv))
                        {
                            ctx.Add(IssueSeverity.Error, BranchOptionsSheet, row.RowNumber, "VideoUID '" + uid + "'가 Videos 시트에 없습니다.");
                            continue;
                        }
                        if (pv.BranchType != BranchType.Select)
                        {
                            ctx.Add(IssueSeverity.Error, BranchOptionsSheet, row.RowNumber,
                                "영상 '" + uid + "'의 BranchType은 " + pv.BranchType + "인데 BranchOptions에 행이 있습니다. (Select 전용 시트)");
                            continue;
                        }

                        string text = Get(row, bText), target = Get(row, bTarget);
                        if (text.Length == 0) ctx.Add(IssueSeverity.Error, BranchOptionsSheet, row.RowNumber, "BranchText가 비어 있습니다.");
                        if (target.Length > 0 && !videos.ContainsKey(target))
                            ctx.Add(IssueSeverity.Warning, BranchOptionsSheet, row.RowNumber,
                                "TargetVideoUID '" + target + "'가 아직 Videos 시트에 없습니다. (작성 중이면 정상)");
                        else if (target.Length == 0)
                            ctx.Add(IssueSeverity.Error, BranchOptionsSheet, row.RowNumber, "TargetVideoUID가 비어 있습니다.");

                        pv.BranchOptions.Add(new BranchOption { branchText = text, targetVideoUID = target });
                        usedForBranch.Add(uid);
                    }
                }
            }
        }
        else ctx.Add(IssueSeverity.Warning, BranchOptionsSheet, 0, "시트를 찾을 수 없습니다. (Select 영상이 있다면 선택지가 비어 있게 됩니다)");

        foreach (var pv in videos.Values)
        {
            if (pv.BranchType != BranchType.Select) continue;
            if (pv.BranchOptions.Count == 0)
                ctx.Add(IssueSeverity.Error, VideosSheet, pv.SourceRow, "영상 '" + pv.VideoUID + "'는 Select인데 BranchOptions에 선택지가 없습니다.");
            else if (pv.BranchOptions.Count == 1)
                ctx.Add(IssueSeverity.Warning, VideosSheet, pv.SourceRow, "영상 '" + pv.VideoUID + "'의 선택지가 1개뿐입니다.");
        }

        // ---------- QTESteps ----------
        XlsxSheet qteSheet;
        if (sheets.TryGetValue(QteStepsSheet, out qteSheet))
        {
            var qh = Header(qteSheet);
            if (qh != null)
            {
                int qUid = Col(qh, "VideoUID"), qCond = Col(qh, "PassCondition"), qOrder = Col(qh, "StepOrder"),
                    qKeys = Col(qh, "AcceptedKeys"), qTime = Col(qh, "TimeLimit"), qSucc = Col(qh, "SuccessTarget"), qFail = Col(qh, "FailTarget");
                var missing = MissingCols(("VideoUID", qUid), ("PassCondition", qCond), ("StepOrder", qOrder),
                    ("AcceptedKeys", qKeys), ("TimeLimit", qTime), ("SuccessTarget", qSucc), ("FailTarget", qFail));
                if (missing.Count > 0)
                    ctx.Add(IssueSeverity.Error, QteStepsSheet, qh.RowNumber, "헤더에 필요한 열이 없습니다: " + string.Join(", ", missing.ToArray()));
                else
                {
                    // 영상별로 (StepOrder, 원본 행)을 모아뒀다가 한꺼번에 정리한다. (같은 VideoUID 값 일치 검사 위해)
                    var byVideo = new Dictionary<string, List<(int order, int row, string keys, float time, string cond, string succ, string fail)>>();

                    foreach (var row in qteSheet.Rows)
                    {
                        if (row.RowNumber <= qh.RowNumber || IsBlankRow(row)) continue;
                        string uid = Get(row, qUid);
                        if (uid.Length == 0) { ctx.Add(IssueSeverity.Error, QteStepsSheet, row.RowNumber, "VideoUID가 비어 있습니다."); continue; }

                        ParsedVideo pv;
                        if (!videos.TryGetValue(uid, out pv))
                        {
                            ctx.Add(IssueSeverity.Error, QteStepsSheet, row.RowNumber, "VideoUID '" + uid + "'가 Videos 시트에 없습니다.");
                            continue;
                        }
                        if (pv.BranchType != BranchType.QTE)
                        {
                            ctx.Add(IssueSeverity.Error, QteStepsSheet, row.RowNumber,
                                "영상 '" + uid + "'의 BranchType은 " + pv.BranchType + "인데 QTESteps에 행이 있습니다. (QTE 전용 시트)");
                            continue;
                        }

                        int order = (int)ParseFloat(ctx, QteStepsSheet, row.RowNumber, "StepOrder", Get(row, qOrder), 0f, allowNegative: false);
                        string keys = Get(row, qKeys);
                        float time = ParseFloat(ctx, QteStepsSheet, row.RowNumber, "TimeLimit", Get(row, qTime), 0f, allowNegative: false);
                        if (time <= 0f) ctx.Add(IssueSeverity.Error, QteStepsSheet, row.RowNumber, "TimeLimit은 0보다 커야 합니다.");
                        if (keys.Length == 0) ctx.Add(IssueSeverity.Error, QteStepsSheet, row.RowNumber, "AcceptedKeys가 비어 있습니다.");

                        string condStr = Get(row, qCond);
                        string succ = Get(row, qSucc), fail = Get(row, qFail);

                        List<(int, int, string, float, string, string, string)> list;
                        if (!byVideo.TryGetValue(uid, out list)) { list = new List<(int, int, string, float, string, string, string)>(); byVideo[uid] = list; }
                        list.Add((order, row.RowNumber, keys, time, condStr, succ, fail));
                        usedForBranch.Add(uid);
                    }

                    foreach (var kv in byVideo)
                    {
                        var pv = videos[kv.Key];
                        var list = kv.Value.OrderBy(x => x.order).ToList();

                        for (int i = 0; i < list.Count; i++)
                            if (list[i].order != i)
                            {
                                ctx.Add(IssueSeverity.Error, QteStepsSheet, list[i].row,
                                    "영상 '" + kv.Key + "'의 StepOrder가 0부터 연속되지 않습니다. (기대값 " + i + ", 실제 " + list[i].order + ")");
                                break;
                            }

                        var condSet = list.Select(x => x.cond).Distinct().ToList();
                        var succSet = list.Select(x => x.succ).Distinct().ToList();
                        var failSet = list.Select(x => x.fail).Distinct().ToList();
                        if (condSet.Count > 1) ctx.Add(IssueSeverity.Warning, QteStepsSheet, list[0].row, "영상 '" + kv.Key + "'의 PassCondition이 단계마다 다릅니다. 첫 값을 사용합니다.");
                        if (succSet.Count > 1) ctx.Add(IssueSeverity.Warning, QteStepsSheet, list[0].row, "영상 '" + kv.Key + "'의 SuccessTarget이 단계마다 다릅니다. 첫 값을 사용합니다.");
                        if (failSet.Count > 1) ctx.Add(IssueSeverity.Warning, QteStepsSheet, list[0].row, "영상 '" + kv.Key + "'의 FailTarget이 단계마다 다릅니다. 첫 값을 사용합니다.");

                        QTEPassCondition cond;
                        if (!TryParsePassCondition(condSet[0], out cond))
                        {
                            ctx.Add(IssueSeverity.Error, QteStepsSheet, list[0].row, "알 수 없는 PassCondition '" + condSet[0] + "'입니다. (Sequential / AnyStep)");
                            cond = QTEPassCondition.Sequence;
                        }

                        string succTarget = succSet[0], failTarget = failSet[0];
                        if (succTarget.Length > 0 && !videos.ContainsKey(succTarget))
                            ctx.Add(IssueSeverity.Warning, QteStepsSheet, list[0].row, "SuccessTarget '" + succTarget + "'가 아직 Videos 시트에 없습니다.");
                        else if (succTarget.Length == 0)
                            ctx.Add(IssueSeverity.Error, QteStepsSheet, list[0].row, "SuccessTarget이 비어 있습니다.");
                        if (failTarget.Length > 0 && !videos.ContainsKey(failTarget))
                            ctx.Add(IssueSeverity.Warning, QteStepsSheet, list[0].row, "FailTarget '" + failTarget + "'가 아직 Videos 시트에 없습니다.");
                        else if (failTarget.Length == 0)
                            ctx.Add(IssueSeverity.Error, QteStepsSheet, list[0].row, "FailTarget이 비어 있습니다.");

                        var qte = new QTESetting { passCondition = cond, successTargetVideoUID = succTarget, failTargetVideoUID = failTarget };
                        foreach (var s in list)
                            qte.steps.Add(new QTEStep { acceptedKeys = s.keys.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList(), timeLimit = s.time });
                        pv.QteSetting = qte;
                    }
                }
            }
        }
        else ctx.Add(IssueSeverity.Warning, QteStepsSheet, 0, "시트를 찾을 수 없습니다. (QTE 영상이 있다면 단계가 비어 있게 됩니다)");

        foreach (var pv in videos.Values)
            if (pv.BranchType == BranchType.QTE && pv.QteSetting == null)
                ctx.Add(IssueSeverity.Error, VideosSheet, pv.SourceRow, "영상 '" + pv.VideoUID + "'는 QTE인데 QTESteps에 단계가 없습니다.");

        res.Videos = videoOrder.Select(id => videos[id]).ToList();
        res.Issues = res.Issues.OrderByDescending(i => (int)i.Severity).ThenBy(i => i.Row).ToList();
        return res;
    }

    // ---------- 도우미 ----------

    sealed class Ctx
    {
        public ParseResult Res;
        public void Add(IssueSeverity sev, string sheet, int row, string msg)
        {
            Res.Issues.Add(new ImportIssue { Severity = sev, Sheet = sheet, Row = row, Message = msg });
        }
    }

    static float ParseFloat(Ctx ctx, string sheet, int row, string field, string raw, float fallback, bool allowNegative)
    {
        if (raw.Length == 0) return fallback;
        float v;
        if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
        {
            ctx.Add(IssueSeverity.Error, sheet, row, field + " '" + raw + "'을(를) 숫자로 읽을 수 없습니다.");
            return fallback;
        }
        if (!allowNegative && v < 0f)
        {
            ctx.Add(IssueSeverity.Error, sheet, row, field + "은(는) 음수일 수 없습니다. (" + v + ")");
            return fallback;
        }
        return v;
    }

    static bool TryParseBranchType(string s, out BranchType t)
    {
        switch ((s ?? "").Trim().ToLowerInvariant())
        {
            case "select": t = BranchType.Select; return true;
            case "qte": t = BranchType.QTE; return true;
            case "continue": t = BranchType.Continue; return true;
            case "ending": t = BranchType.Ending; return true;
            default: t = BranchType.Select; return false;
        }
    }

    static bool TryParsePassCondition(string s, out QTEPassCondition c)
    {
        switch ((s ?? "").Trim().ToLowerInvariant())
        {
            case "sequential": c = QTEPassCondition.Sequence; return true;
            case "anystep": c = QTEPassCondition.AnyStep; return true;
            default: c = QTEPassCondition.Sequence; return false;
        }
    }

    static XlsxRow Header(XlsxSheet sheet)
    {
        return sheet.Rows.FirstOrDefault(r => r.Cells.Any(c => Clean(c).Length > 0));
    }

    static int Col(XlsxRow header, string name)
    {
        for (int i = 0; i < header.Cells.Length; i++)
            if (string.Equals(Clean(header.Cells[i]), name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    static List<string> MissingCols(params (string name, int index)[] pairs)
    {
        var missing = new List<string>();
        foreach (var p in pairs)
            if (p.index < 0) missing.Add(p.name);
        return missing;
    }

    static string Get(XlsxRow row, int col)
    {
        return col >= 0 && col < row.Cells.Length ? Clean(row.Cells[col]) : "";
    }

    static bool IsBlankRow(XlsxRow row)
    {
        return row.Cells.All(c => Clean(c).Length == 0);
    }

    static string Clean(string s)
    {
        return (s ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
    }
}