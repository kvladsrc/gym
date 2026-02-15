document.addEventListener("DOMContentLoaded", () => {
  const buildsBody = document.getElementById("builds-body");
  const loadingIndicator = document.getElementById("loading-indicator");
  const refreshBtn = document.getElementById("refresh-btn");
  const statusDot = document.querySelector(".dot");
  const statusText = document.querySelector(".status-text");

  // API Endpoint
  const API_BASE = "/api";
  const TENANT = "your.domain";

  async function fetchBuilds() {
    try {
      setLoading(true);
      // Fetch last 20 builds
      const response = await fetch(
        `${API_BASE}/tenant/${TENANT}/builds?limit=20`,
      );

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const contentType = response.headers.get("content-type");
      if (!contentType || !contentType.includes("application/json")) {
        const text = await response.text();
        console.error("Received non-JSON response:", text.substring(0, 100));
        throw new Error("Received non-JSON response from API");
      }

      const data = await response.json();
      renderBuilds(data);
      updateConnectionStatus(true);
    } catch (error) {
      console.error("Error fetching builds:", error);
      renderError(error.message);
      updateConnectionStatus(false);
    } finally {
      setLoading(false);
    }
  }

  function renderBuilds(builds) {
    buildsBody.innerHTML = "";
    loadingIndicator.style.display = "none";

    if (builds.length === 0) {
      buildsBody.innerHTML = `
                <tr>
                    <td colspan="6" style="text-align: center; padding: 3rem; color: var(--text-secondary);">
                        No builds found.
                    </td>
                </tr>
            `;
      return;
    }

    builds.forEach((build) => {
      const row = document.createElement("tr");

      const statusClass = getStatusClass(build.result);
      const statusText =
        build.result || (build.end_time ? "UNKNOWN" : "RUNNING");

      const projectName = build.ref
        ? build.ref.project
        : build.job_name || "Unknown";
      const duration = build.duration
        ? formatDuration(build.duration * 1000)
        : "-";
      const startTime = new Date(build.start_time).toLocaleString();

      row.innerHTML = `
                <td><span class="project-name">${projectName}</span></td>
                <td><span class="mono">${build.job_name}</span></td>
                <td><span class="pipeline-badge">${build.pipeline}</span></td>
                <td><span class="status-badge ${statusClass}">${statusText}</span></td>
                <td class="mono">${duration}</td>
                <td class="mono" style="font-size: 0.75rem">${startTime}</td>
            `;
      buildsBody.appendChild(row);
    });
  }

  function getStatusClass(result) {
    if (!result) return "status-running"; // Assuming null result means running if end_time is null, or just unknown
    const res = result.toUpperCase();
    if (res === "SUCCESS") return "status-success";
    if (res === "FAILURE") return "status-failure";
    return "status-queued";
  }

  function formatDuration(ms) {
    const seconds = Math.floor(ms / 1000);
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}m ${s}s`;
  }

  function renderError(msg) {
    buildsBody.innerHTML = `
            <tr>
                <td colspan="6" style="text-align: center; padding: 2rem; color: var(--error);">
                    Failed to load builds: ${msg}
                </td>
            </tr>
        `;
    loadingIndicator.style.display = "none";
  }

  function updateConnectionStatus(connected) {
    if (connected) {
      statusDot.style.backgroundColor = "var(--success)";
      statusDot.style.boxShadow = "0 0 8px rgba(35, 134, 54, 0.4)";
      statusText.textContent = "Connected";
    } else {
      statusDot.style.backgroundColor = "var(--error)";
      statusDot.style.boxShadow = "0 0 8px rgba(218, 54, 51, 0.4)";
      statusText.textContent = "Disconnected";
    }
  }

  function setLoading(isLoading) {
    if (isLoading) {
      refreshBtn.classList.add("spinning");
      if (buildsBody.children.length === 0) {
        loadingIndicator.style.display = "flex";
      }
    } else {
      refreshBtn.classList.remove("spinning");
    }
  }

  refreshBtn.addEventListener("click", fetchBuilds);

  // Initial fetch
  fetchBuilds();

  // Auto refresh every 30 seconds
  setInterval(fetchBuilds, 30000);
});
