#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using graph = vector<vector<pair<int, int>>>;

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

  dijkstra_heap.push({0, start});

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
      dijkstra_heap.push({v.first + e.second, e.first});
    }
  }

  return INT_MAX;
}

int main(int argc, char *argv[]) {
  int v, e;
  std::cin >> v >> e;

  graph g(v);

  while (e--) {
    int a, b, d;
    std::cin >> a >> b >> d;
    g[a].push_back({b, d});
    g[b].push_back({a, d});
  }

  int a, b;
  std::cin >> a >> b;
  std::cout << dijkstra(g, a, b) << "\n";

  return 0;
}
