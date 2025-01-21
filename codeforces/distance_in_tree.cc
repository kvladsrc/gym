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

using graph = vector<vector<int>>;

vector<int> solve(graph &g, int start, int p, int k, int &acc) {
  vector<int> res(k + 1, 0);
  res[0] = 1;

  for (auto n : g[start]) {
    if (n == p) {
      continue;
    }

    auto ntable = solve(g, n, start, k, acc);
    for (int i = 0; i < k; ++i) {
      acc += res[k - (i + 1)] * ntable[i];
    }
    for (int i = 0; i < k; ++i) {
      res[i + 1] += ntable[i];
    }
  }

  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

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
    g[a].push_back(b);
    g[b].push_back(a);
  }

  int res = 0;
  auto t = solve(g, 0, 0, k, res);
  std::cout << res << "\n";

  return 0;
}
