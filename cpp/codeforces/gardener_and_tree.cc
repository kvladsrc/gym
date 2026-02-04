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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    graph g(n);

    for (int i = 1; i < n; ++i) {
      int a = 0;
      int b = 0;
      std::cin >> a >> b;
      a--;
      b--;
      g[a].insert(b);
      g[b].insert(a);
    }

    vector<int> leafs;
    vector<int> new_leafs;

    vector<int> nodes(n);
    std::iota(nodes.begin(), nodes.end(), 0);
    std::copy_if(nodes.begin(), nodes.end(), std::back_inserter(leafs),
                 [&](int i) { return g[i].size() <= 1; });

    int res = n;
    for (int i = 0; i < k; ++i) {
      if (leafs.empty()) {
        break;
      }

      res -= leafs.size();

      for (auto l : leafs) {
        if (g[l].empty()) {
          continue;
        }

        auto p = *g[l].begin();
        g[p].erase(l);
        if (g[p].size() == 1) {
          new_leafs.push_back(p);
        }
      }

      leafs = new_leafs;
      new_leafs.clear();
    }

    std::cout << res << "\n";
  }

  return 0;
}
