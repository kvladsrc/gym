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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using graph = vector<vector<bool>>;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  graph railways(n, vector<bool>(n, false));
  for (int i = 0; i < m; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    railways[a][b] = true;
    railways[b][a] = true;
  }

  int res = -1;
  bool connected = !railways[0][n - 1];
  queue<pair<int, int>> q;
  q.push({0, 0});
  map<int, bool> visited;
  while (!q.empty()) {
    auto cur = q.front();
    q.pop();
    if (visited[cur.second]) {
      continue;
    }
    if (cur.second == n - 1) {
      res = cur.first;
      break;
    }
    visited[cur.second] = true;
    for (int i = 0; i < n; ++i) {
      if (railways[cur.second][i] == connected) {
        q.push({cur.first + 1, i});
      }
    }
  }

  std::cout << res << "\n";

  return 0;
}
