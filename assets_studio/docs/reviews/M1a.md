# M1a — review log

<!-- markdownlint-disable MD013 -->

The critic was a separate sub-agent; at most five proposal → critique → fix
iterations.

## Iteration 1

Verdict: **REVISE**, two blockers and thirteen recommendations.

### Iteration 1: blockers

1. **The checker did not enforce seed behavior.** The SDK writes `meta.seed`,
   so checking that field always passed, even for ignored seeds or one batch
   generator.
   *Fixed:* compare batch outputs with single requests at the same seed, and
   compare different seeds. Seed independence is an error (`--allow-seed-independent`
   for deterministic models). Batch/single differences are warnings unless
   `--strict-determinism`, since GPUs need not be byte-deterministic. Negative
   tests cover ignored seeds and one generator per batch. ADR-001 and SDK
   README document the rule with a diffusers example.
2. **Some errors violated the contract.** Nonserializable `meta`, non-`Output`
   returns and unknown routes produced `text/plain` or `{"detail": …}`.
   *Fixed:* serialize/validate outputs inside error handling; add `Exception`
   and Starlette HTTP error handlers. Each case tested.

### Iteration 1: recommendations

| # | Finding | Resolution |
| --- | --- | --- |
| 1 | SDK/checker disagreed on parameters (`default_factory`, aliases) | Shared `params_schema_problems` in `contract.py`: flat scalars/enums, defaults, no aliases |
| 2 | Different `load`/`generate` threads; `SystemExit` during loading left permanent `loading` | One dedicated model thread; load `BaseException` becomes `error`. Tests |
| 3 | Effective parameters missing | `GenerateResponse.params`, ADR-002 `effective_params` column |
| 4 | Checker traceback on disconnection; did not wait for unstarted servers | Transport errors become FAIL lines; `--wait` also covers unavailable servers |
| 5 | Missing JPEG/WebP/MP3/FLAC/OGG samples/signatures | Require canonical inputs (`image/png`, `audio/wav`, `model/gltf-binary`) so samples always exist; add signatures; warn on unknown output types |
| 6 | Error probes only covered first task; no violation-detection tests | Probe every task, extra/missing prompts and unknown routes; corrupting-proxy tests |
| 7 | Lock retention after client disconnect unverified | Live-server test |
| 8 | Per-tab dispatch raced on shared servers; `busy` consumed retries | ADR-002: one job per server address; unlimited `busy`; document three-retry behavior |
| 9 | Missing connection-loss/timeout semantics; plan/ADR retry disagreement | ADR-002 defines `connection_lost`, `timeout` (none by default), Studio codes; align plan |
| 10 | `job_dependencies` allowed two sources for a role | Key `(job_id, input_role)`; `job_inputs`/`job_dependencies` mutually exclusive; waiting jobs do not block independent ones |
| 11 | ADR task description differed from code | Align wording |
| 12 | Plan §5.2/§5.4 outdated | Shorten and reference ADRs |
| 13 | SDK version unchanged; Python 3.11 checked only by pyright | Version 1.1.0; `just check` runs SDK tests on Python 3.11 |

## Iteration 2

Verdict: **REVISE**, one blocker and five recommendations. All iteration 1
fixes confirmed; no regressions except the one noted below.

### Iteration 2: blocker

1. **Genuinely random servers without seeds passed.** Different-seed comparisons
   caught only deterministic seed-ignoring servers. An unseeded generator
   received only a batch-mismatch warning.
   *Fixed:* repeat the single request twice; differences are errors
   (`--allow-nondeterministic` for exceptions). Same batch size means GPU
   batch numerical differences are irrelevant. Negative unseeded-generator test.

### Iteration 2: recommendations

| # | Finding | Resolution |
| --- | --- | --- |
| 1 | Regression: `ThreadPoolExecutor` prevented Ctrl+C exit during `load` | Dedicated daemon thread/queue (`_ModelThread`); SIGINT test during 60-second load exits immediately |
| 2 | `generate` `BaseException` produced plain 500 | Catch as `internal`; `SystemExit` test |
| 3 | Batch warning could be clearer | Distinguish reproducible single requests from numerical/shared-generator mismatch; defer numerical PNG/WAV comparison to avoid decoder code in SDK |
| 4 | Lock leaked if queue submission failed | Release on submission failure; afterward use future completion callback |
| 5 | “Future exception was never retrieved” after disconnect | Callback consumes exception |

## Iteration 3

Verdict: **ACCEPT**. No blockers. Critic experiments confirmed unseeded servers
FAIL, `generate` `SystemExit` returns a contract error, SIGINT during loading
exits within fractions of a second, and locks do not leak.

Final recommendation: if identical requests differ under
`--allow-nondeterministic`, mark seed dependence “not verifiable” with a warning,
and do not claim single requests repeat in the batch warning. Numerical PNG/WAV
comparison deferred with critic agreement.
