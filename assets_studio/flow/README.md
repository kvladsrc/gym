# Studio flow

A separate local service and editor for saved job graphs.

```sh
cd assets_studio
just sync
just web-build
just studio                 # library and queue, :9000
just launcher               # starts models for queued jobs
just flow                   # graph editor, http://127.0.0.1:9001
```

`just flow --studio http://127.0.0.1:9000 --port 9001` changes the addresses;
`--database PATH` changes graph storage. The default is
`$XDG_DATA_HOME/assets-studio/flow.sqlite3` (or `~/.local/share/...`), outside
Dropbox. Models are still configured in `studio.toml`.

## Working with a graph

1. Add library inputs and the required tasks from the left panel.
2. Select a node and assign a source node to each input role. Choices are
   filtered by compatible MIME types; multi-output results allow choosing a
   zero-based output index.
3. Fill in prompts and parameters. Drag cards by their top bar; “Arrange graph”
   lays them out by dependency.
4. Save the graph and click “Run”. A run creates an immutable snapshot; later
   edits do not change an already submitted chain.
5. Select a run node to inspect errors or download results. Return to editing
   to run the same graph again.

Graphs export and import as JSON. The
[`examples/character.json`](examples/character.json) example connects two
branches: mesh → MIA and text → Kimodo, then transfers Kimodo motion onto MIA's
result. After importing, replace the placeholder with an actual mesh asset.

The editor uses server capabilities discovered by Studio. Start a new server
once, then refresh the model list. Servers may be stopped when a graph is
submitted: jobs wait for them, and the launcher starts them.

## Responsibilities

- `studio` stores assets, provenance, jobs and dependencies, and executes the queue.
- `launcher` sees runnable jobs and starts the required servers.
- `studio flow` stores graph definitions, versions and submission receipts,
  creating ordinary jobs through Studio's public REST API. It does not launch
  models or processes or maintain a second execution queue.

Nodes are library inputs or jobs. Edges represent job input roles: direct
`asset_id` or `dependencies[role]={job_id,output_index}`. Branching and merging
are supported. Cycles, missing sources and invalid indices are rejected on
save. Before running, tasks, parameters, required inputs and format compatibility
are also validated.

Each run/node pair has a stable idempotency key. If a connection breaks after
Studio accepts a job, or Flow restarts, resubmission returns the same job.
Recorded receipts are skipped. Stopping Flow does not stop jobs already
accepted by Studio. Cancelling a run requests cancellation of its jobs; if
Studio is temporarily unavailable, cancellation resumes when connectivity returns.

A permanent request error stops submission of remaining nodes and is saved
alongside already created jobs. A corrected graph starts a new run. Completed
results remain in the library. Run snapshots survive deletion of graph definitions.

Snapshots fix parameters and edges; Studio records model revisions in jobs.
This does not pin future model versions for reruns.

## Checks

```sh
just flow-test               # graph, SQLite, recovery and HTTP errors
just test flow/tests/test_ui.py  # browser walkthrough if Chrome is available
```

The browser check uses a separate Studio with a fake model: node creation,
connection and dragging, saving, reloading, two runs through the real queue
and result download. For UI development: `cd web && npm run dev`, then
`/flow.html`; the Flow API must listen on :9001.
