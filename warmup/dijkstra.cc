#include "cpp/warmup/dijkstra.hpp"

#include <climits>
#include <iostream>
#include <queue>
#include <string>
#include <utility>
#include <vector>

using std::pair;
using std::priority_queue;
using std::string;
using std::vector;

namespace warmup {

class CompareDistance {
 public:
  bool operator()(const pair<int, int> &p1, const pair<int, int> &p2) const {
    return p1.first > p2.first;
  }
};

int dijkstra(graph &g, int start, int end) {
  vector<bool> visited(g.size(), false);

  priority_queue<pair<int, int>, vector<pair<int, int>>, CompareDistance>
      dijkstra_heap;

  dijkstra_heap.emplace(0, start);

  while (!dijkstra_heap.empty()) {
    // Taking a shortest path.
    auto v = dijkstra_heap.top();
    dijkstra_heap.pop();

    if (v.second == end) {
      return v.first;
    }

    if (visited[v.second]) {
      continue;
    }
    visited[v.second] = true;

    for (auto e : g[v.second]) {
      // Adding dist to curent V to each weight with connected vertex.
      dijkstra_heap.emplace(v.first + e.second, e.first);
    }
  }

  return INT_MAX;
}

}  // namespace warmup
