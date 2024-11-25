#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct edge {
  int end, weight;
};

struct path {
  int start, end, dist;
};

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;

  vector<vector<edge>> g(n);

  for (int i = 0; i < m; ++i) {
    int a, b, w;
    std::cin >> a >> b >> w;

    g[a - 1].push_back({b - 1, w});
    g[b - 1].push_back({a - 1, w});
  }

  vector<int> dist(n, -1);
  vector<int> prev(n, -1);

  auto comp = [](const path &a, const path &b) { return a.dist > b.dist; };
  priority_queue<path, vector<path>, decltype(comp)> heap(comp);
  heap.push({0, 0, 1});

  while (!heap.empty()) {
    auto p = heap.top();
    heap.pop();

    if (dist[p.end] != -1) continue;

    dist[p.end] = p.dist;
    prev[p.end] = p.start;

    for (auto e : g[p.end]) {
      heap.push({p.end, e.end, e.weight + dist[p.end]});
    }
  }

  if (dist[n - 1] == -1) {
    std::cout << -1 << "\n";
  } else {
    vector<int> p;
    int pos = n - 1;
    while (pos != 0) {
      p.push_back(pos);
      pos = prev[pos];
    }
    p.push_back(0);

    std::reverse(p.begin(), p.end());
    for (auto v : p) {
      std::cout << v + 1 << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
