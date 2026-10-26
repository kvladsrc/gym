# M0b (TripoSR) and M1b — review log

<!-- markdownlint-disable MD013 -->

Combined review to save tokens: TripoSR server and Studio core.

## Iteration 1

Verdict: **REVISE**, two blockers and ten recommendations.

### Iteration 1: blockers

1. **Exceptions left jobs permanently `running`.** A failing event subscriber
   (such as a closed SSE stream in M2), disk error or SQLite error between
   `start()` and `succeed()` left a job impossible to cancel or retry.
   *Fixed:* isolate subscribers in `Studio._job_changed`; wrap everything after
   `start()` so any exception becomes `failed/internal`. Tests: failing subscriber,
   disk write failure.
2. **Dependent job creation race.** A predecessor failing between `submit`
   validation and insertion left the new job permanently `queued`.
   *Fixed:* `add_job` rechecks predecessors in the same `BEGIN IMMEDIATE`
   transaction as `fail`/`cancel`, inserting `failed/dependency_failed` immediately.
   Tested.

### Iteration 1: recommendations

| # | Finding | Resolution |
| --- | --- | --- |
| 1 | `succeed` did not require `running`; two processes sharing a database broke recovery | Check `rowcount` with rollback (`JobNotRunning`); exclusive data-directory lock (`DataDirectoryBusy`). Tests |
| 2 | Missing events for `waiting_model` and dependent failures | Transitions return changed IDs (`RETURNING`); emit each. Test |
| 3 | Server stayed ready during long generation | Report `busy` during generation. Test |
| 4 | Chain retry impossible with failed dependency | `retry` uses predecessor's latest retry, automatically retrying failed predecessors. Test |
| 5 | `ConnectTimeout` treated as generation timeout | `ConnectTimeout`/`PoolTimeout` mean unsent; wait for server |
| 6 | FIFO depended on wall clock | Order by strictly increasing IDs only |
| 7 | TripoSR RGBA without background removal became black rather than grey | Composite transparency over training grey. Test |
| 8 | TripoSR cuBLAS/cuDNN OOM classified as `internal` | Recognize message and return retryable error. Test |
| 9 | `git fetch … \|\| true` hid errors | Fetch only when revision is absent locally; expose failures |
| 10 | Test gaps | Test every fix above |

## Iteration 2

Verdict: **REVISE**, one blocker and three recommendations. All iteration 1
fixes confirmed.

### Iteration 2: blocker

1. **Unexpected input preparation errors blocked a server queue.** Missing
   asset files or oversized images (`DecompressionBombError`) left jobs queued,
   selected every poll and blocking later jobs.
   *Fixed:* every preparation failure becomes `failed/internal` with a message.
   Test: missing file followed by a successful independent job.

### Iteration 2: recommendations

| # | Finding | Resolution |
| --- | --- | --- |
| 1 | Chain retry revived user-cancelled predecessors | Retry only failed predecessors; cancelled ones cause an explanatory error. Test |
| 2 | Creation can immediately return a failed job due to predecessor race | Documented in ADR-002 for API/MCP clients |
| 3 | Data-directory lock leaked when database opening failed | Release on constructor failure |

## Iteration 3

Verdict: **ACCEPT**. The critic experimentally confirmed iteration 2 fixes.
Final recommendation implemented: validate the entire predecessor chain before
retrying, so a cancelled-predecessor rejection queues nothing. Tested.
