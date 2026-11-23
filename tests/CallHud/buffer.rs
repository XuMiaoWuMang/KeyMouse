use super::*;

fn event_at(id: &str, scope: &str, kind: &str, status: &str, seq: u64, index: u64) -> String {
    format!("{EVENT_PREFIX}{}", serde_json::json!({
        "seq": seq, "id": id, "scopeId": scope, "kind": kind, "status": status,
        "origin": "1", "index": index, "total": null, "command": "调用 child.json"
    }))
}
fn event(id: &str, scope: &str, status: &str) -> String {
    event_at(id, scope, "call", status, if status == "current" { 1 } else { 2 }, 1)
}
fn snapshot(log: &HudLog) -> Vec<serde_json::Value> {
    log.snapshot().iter().map(|line| serde_json::from_str(line.strip_prefix(EVENT_PREFIX).unwrap()).unwrap()).collect()
}

#[test]
fn long_call_keeps_parent_and_latest_three_children_during_and_after_raw_overflow() {
    let log = HudLog::default();
    log.push(&event("call", "root", "current"));
    for index in 0..600 {
        log.push(&event_at(&format!("s{index}"), "call", "step", "done", index + 2, index + 1));
        log.push("ordinary output");
    }
    let rows = snapshot(&log);
    assert_eq!(rows.len(), 4);
    assert_eq!(rows[0]["status"], "current");
    assert_eq!(rows[1..].iter().map(|e| e["id"].as_str().unwrap()).collect::<Vec<_>>(), ["s597", "s598", "s599"]);
    log.push(&event_at("call", "root", "call", "done", 602, 1));
    assert_eq!(snapshot(&log)[0]["status"], "done");
    for index in 1..7 { log.push(&event(&format!("p{index}"), "root", "done")); }
    assert_eq!(log.snapshot().len(), 10);
    log.push(&event("p7", "root", "done"));
    assert_eq!(log.snapshot().len(), 7);
    assert!(!snapshot(&log).iter().any(|e| e["id"] == "call" || e["scopeId"] == "call"));
}

#[test]
fn evicting_nested_call_removes_descendants_and_rejects_orphans() {
    let log = HudLog::default();
    log.push(&event("a", "root", "current"));
    log.push(&event("b", "a", "done"));
    log.push(&event("c", "b", "done"));
    for index in 0..3 { log.push(&event(&format!("s{index}"), "a", "done")); }
    assert_eq!(log.snapshot().len(), 4);
    log.push(&event("orphan", "b", "done"));
    assert_eq!(log.snapshot().len(), 4);
}

#[test]
fn repeated_calls_keep_local_indices_and_terminal_states_separate() {
    let log = HudLog::default();
    for (id, status) in [("c1", "failed"), ("c2", "cancelled")] {
        log.push(&event(id, "root", "current"));
        log.push(&event_at(&format!("{id}s"), id, "step", status, 2, 1));
        log.push(&event_at(id, "root", "call", status, 3, 1));
    }
    let rows = snapshot(&log);
    assert_eq!(rows.len(), 4);
    assert_eq!(rows[0]["status"], "failed");
    assert_eq!(rows[2]["status"], "cancelled");
    assert_eq!(rows[1]["index"], 1);
    assert_eq!(rows[3]["index"], 1);
    log.clear();
    assert!(log.snapshot().is_empty());
}

#[test]
fn malformed_or_cyclic_protocol_cannot_poison_the_cache() {
    let log = HudLog::default();
    log.push("legacy");
    log.push(&format!("{EVENT_PREFIX}{{\"id\":\"bad\",\"scopeId\":\"root\"}}"));
    log.push(&event("root", "root", "done"));
    log.push(&event_at("bad-status", "root", "step", "unknown", 1, 1));
    assert_eq!(log.snapshot(), ["legacy"]);
    log.push(&event("a", "root", "current"));
    log.push(&event("b", "a", "current"));
    log.push(&event_at("a", "b", "call", "done", 3, 1));
    assert_eq!(snapshot(&log)[0]["scopeId"], "root");
}

#[test]
fn stale_updates_and_children_of_steps_are_ignored() {
    let log = HudLog::default();
    log.push(&event("a", "root", "current"));
    log.push(&event_at("a", "root", "call", "done", 4, 1));
    log.push(&event_at("a", "root", "call", "current", 2, 1));
    log.push(&event_at("step", "a", "step", "done", 5, 1));
    log.push(&event("invalid-child", "step", "done"));
    let rows = snapshot(&log);
    assert_eq!(rows.len(), 2);
    assert_eq!(rows[0]["status"], "done");
}
