"""Run-record helper for Hermes delegation (tools/delegation).

run.json is the durable launch record: model, effort, base, brief hash, timing,
exit, outcome and token usage. Commands:

  runrec.py set <run.json> key=value ...     merge keys (JSON values typed)
  runrec.py usage <run.json> <usage.json>    copy token totals from a receipt
  runrec.py ledger <run.json> <ledger.jsonl> append a compact record
  runrec.py show <run.json>                  print a one-line summary
"""
import json
import os
import sys


def load(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return {}


def save(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, sort_keys=True)
    os.replace(tmp, path)


def typed(value):
    try:
        return json.loads(value)
    except ValueError:
        return value


def cmd_set(path, pairs):
    data = load(path)
    for pair in pairs:
        key, _, value = pair.partition("=")
        data[key] = typed(value)
    save(path, data)


def cmd_usage(path, usage_path):
    data = load(path)
    if not os.path.exists(usage_path) or os.path.getsize(usage_path) == 0:
        data["usage"] = None
    else:
        u = load(usage_path)
        keys = ("model", "provider", "input_tokens", "output_tokens", "cache_read_tokens",
                "reasoning_tokens", "total_tokens", "api_calls", "completed", "interrupted",
                "failed", "turn_exit_reason")
        data["usage"] = {k: u.get(k) for k in keys}
        comp = (u.get("auxiliary") or {}).get("by_task", {}).get("compression")
        data["usage"]["compressions"] = comp.get("api_calls", 0) if comp else 0
        if data.get("model") and u.get("model") and data["model"] != u["model"]:
            data["receipt_model_mismatch"] = u["model"]
    save(path, data)


def cmd_ledger(path, ledger):
    d = load(path)
    u = d.get("usage") or {}
    rec = {k: d.get(k) for k in ("run", "model", "effort", "task_class", "base_sha", "brief_sha256",
                                 "started", "ended", "wall_minutes", "exit", "outcome", "commit", "plan")}
    rec["total_tokens"] = u.get("total_tokens")
    rec["api_calls"] = u.get("api_calls")
    rec["compressions"] = u.get("compressions")
    os.makedirs(os.path.dirname(ledger), exist_ok=True)
    with open(ledger, "a", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(rec, sort_keys=True) + "\n")


def cmd_show(path):
    d = load(path)
    u = d.get("usage") or {}
    print("{run} {model}/{effort} outcome={outcome} exit={exit} wall={wall}m tokens={tok} calls={calls}".format(
        run=d.get("run"), model=d.get("model"), effort=d.get("effort"), outcome=d.get("outcome"),
        exit=d.get("exit"), wall=d.get("wall_minutes"), tok=u.get("total_tokens"), calls=u.get("api_calls")))


def main(argv):
    if len(argv) < 3:
        sys.exit(__doc__)
    cmd, path = argv[1], argv[2]
    if cmd == "set":
        cmd_set(path, argv[3:])
    elif cmd == "usage":
        cmd_usage(path, argv[3])
    elif cmd == "ledger":
        cmd_ledger(path, argv[3])
    elif cmd == "show":
        cmd_show(path)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)
