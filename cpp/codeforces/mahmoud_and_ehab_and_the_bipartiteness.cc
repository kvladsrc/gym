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
#include <utility>
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

using graph = vector<vector<int>>;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  graph g(n--);
  while (n--) {
    int a, b;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  // Run DFS to count left and right set size. Then for each vertex in
  // left set add the number of opposite set size - existing edges.
  int left = 0, right = 0;

  // 0 = unvisited, 1 added to left, 2 added to right.
  map<int, int> visited;

  // p.first is for node, p.second is for set: true = left, false =
  // right.
  std::queue<pair<int, bool>> q;
  q.push({0, true});

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();

    if (visited[cur.first]) {
      continue;
    }

    if (cur.second) {
      left++;
      visited[cur.first] = 1;
    } else {
      right++;
      visited[cur.first] = 2;
    }

    for (auto v : g[cur.first]) {
      q.push({v, !cur.second});
    }
  }

  int64_t res = 0;
  for (auto p : visited) {
    if (p.second == 2) {
      continue;
    }

    res += right - g[p.first].size();
  }

  std::cout << res << "\n";

  return 0;
}
