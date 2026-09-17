/** Serializes rendering and publishes only the most recently requested view. */
export class RenderQueue {
  constructor(render, commit, onError) {
    this.render = render;
    this.commit = commit;
    this.onError = onError;
    this.version = 0;
    this.pending = null;
    this.busy = false;
  }

  invalidate() {
    this.version += 1;
    this.pending = null;
  }

  request(view) {
    this.pending = { view, version: ++this.version };
    if (!this.busy) this.idle = this.drain();
    return this.idle;
  }

  async drain() {
    this.busy = true;
    try {
      while (this.pending) {
        const { view, version } = this.pending;
        this.pending = null;
        try {
          const result = await this.render(view);
          if (version === this.version) this.commit(result, view);
        } catch (error) {
          if (version === this.version) this.onError(error, view);
        }
      }
    } finally {
      this.busy = false;
    }
  }
}
