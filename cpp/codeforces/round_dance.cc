#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

using graph = vector<set<int>>;

void dfs(graph& g, int start, vector<int>& cc_table, int cc) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;

  for (auto n : g[start]) {
    dfs(g, n, cc_table, cc);
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    graph g(n);

    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
      buf--;

      g[i].insert(buf);
      g[buf].insert(i);
    }

    vector<int> cc_table(n, 0);
    int cc = 0;
    for (int i = 0; i < n; ++i) {
      if (cc_table[i] != 0) {
        continue;
      }

      dfs(g, i, cc_table, ++cc);
    }

    vector<bool> open(cc, false);
    for (int i = 0; i < n; ++i) {
      if (g[i].size() == 1) {
        auto local_cc = cc_table[i] - 1;
        open[local_cc] = true;
      }
    }

    int opened_cc = 0;
    int closed_cc = 0;

    for (auto c : open) {
      if (c) {
        opened_cc++;
      } else {
        closed_cc++;
      }
    }

    int maximal = closed_cc + opened_cc;
    int minimal = closed_cc + (opened_cc != 0 ? 1 : 0);

    std::cout << minimal << " " << maximal << "\n";
  }

  return 0;
}
